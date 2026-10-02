using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NSEipix.Base;
using NSEipix.Repository;
using NSMedieval.Model;
using NSMedieval.Repository;
using NSMedieval.State;
using NSMedieval.View.Animals;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// El huevo y el abrazacaras llevan su propio cuerpo encima del lobo.
    ///
    /// <b>Por que no vale <see cref="XenoRunnerModel"/>.</b> Aquel pide un
    /// <c>SkinnedMeshRenderer</c> en el prefab porque el Corredor lleva
    /// esqueleto y cinco clips. Estos dos son mallas rigidas - un huevo no anda
    /// y el abrazacaras, mientras no tenga animacion propia, tampoco -, asi que
    /// llegan del bundle como <c>MeshFilter</c> + <c>MeshRenderer</c> y hay que
    /// vestirlos con otra mano. Lo demas es igual y por las mismas razones: se
    /// cuelga de <c>SetMaterialBasedOnType</c>, que corre al final de
    /// <c>Setup</c> y en cada repintado; la malla se pide al
    /// <c>MeshRepository</c> - la etiqueta <c>Prefab</c> el cargador del juego
    /// la recoge y luego no la usa -; y el lobo se esconde con
    /// <c>forceRenderingOff</c>, que es una bandera que el juego no toca.
    ///
    /// <b>El tamano se mide contra el lobo</b>, igual que el Corredor: se lleva
    /// la malla a los metros que dice la tabla. Un huevo de un metro y un
    /// abrazacaras de sesenta centimetros son los que hacen que uno se lea como
    /// huevo y el otro como algo que cabe en una cara.
    /// </summary>
    [HarmonyPatch]
    internal static class XenoHiveModel
    {
        private sealed class Skin
        {
            internal string Mesh;
            internal string Texture;
            internal float Metres;
        }

        private static readonly Dictionary<string, Skin> Wardrobe = new Dictionary<string, Skin>
        {
            {
                Facehugger.EggId,
                new Skin { Mesh = "aldrich_xeno_egg", Texture = "aldrich_xeno_egg.png", Metres = 0.55f }
            },
            {
                Facehugger.HuggerId,
                new Skin { Mesh = "aldrich_facehugger", Texture = "aldrich_facehugger.png", Metres = 1.8f }
            },
        };

        private static readonly int Diffuse = Shader.PropertyToID("_Diffuse");
        private static readonly Dictionary<string, Texture2D> Skins = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> Warned = new HashSet<string>();
        private static readonly HashSet<string> Said = new HashSet<string>();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(AnimalView), "SetMaterialBasedOnType");
        }

        private static void Postfix(AnimalView __instance)
        {
            try
            {
                if (!GMPlugin.XenoOwnModel.Value) return;

                var animal = __instance?.AnimalInstance;
                if (animal == null || animal.HasDisposed) return;

                var id = animal.Id;
                Skin skin;
                if (!Wardrobe.TryGetValue(id, out skin))
                {
                    id = animal.Blueprint?.GetID();
                    if (id == null || !Wardrobe.TryGetValue(id, out skin)) return;
                }

                if (__instance.GetComponent<HiveBody>() != null) return;
                Dress(__instance, skin, id);
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[hive] could not dress one: {e}");
            }
        }

        private static void Dress(AnimalView view, Skin skin, string id)
        {
            var prefab = MonoRepository<MeshRepository, KeyGameObjectPair>.Instance?
                .GetByAddress(skin.Mesh);
            if (prefab == null)
            {
                if (Warned.Add(id))
                {
                    GMPlugin.Log?.LogWarning(
                        $"[hive] '{skin.Mesh}' no esta en MeshRepository: '{id}' se queda de lobo. "
                        + "?Esta publicado el bundle de XenomorphRunner?");
                }
                return;
            }

            Renderer wolf = null;
            var renderers = view.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r is SkinnedMeshRenderer) { wolf = r; break; }
            }
            if (wolf == null && renderers.Length > 0) wolf = renderers[0];
            if (wolf == null) return;

            var model = UnityEngine.Object.Instantiate(prefab, view.transform, false);
            model.name = "AldrichHiveBody";
            SetLayer(model.transform, wolf.gameObject.layer);

            var ours = model.GetComponentInChildren<Renderer>(true);
            if (ours == null)
            {
                UnityEngine.Object.Destroy(model);
                GMPlugin.Log?.LogError($"[hive] '{skin.Mesh}' no trae renderer");
                return;
            }

            ours.sharedMaterial = wolf.sharedMaterial;
            ours.shadowCastingMode = wolf.shadowCastingMode;
            ours.receiveShadows = wolf.receiveShadows;

            var walker = id == Facehugger.HuggerId;
            if (walker)
            {
                // En el bundle la cola apunta a +Z, que es el frente del
                // animal: andaba de culo. Media vuelta sobre el eje vertical.
                model.transform.localRotation =
                    Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
            }

            var unit = Mathf.Max(0.0001f, view.transform.lossyScale.y);
            var size = ours.bounds.size / unit;
            // El huevo se mide por el alto; el abrazacaras, por lo largo: es
            // plano (6 veces mas ancho que alto) y a 0,6 m de alto salia de
            // cuatro metros de envergadura, una torta en el suelo.
            var measure = Mathf.Max(0.0001f, walker ? Mathf.Max(size.x, size.z) : size.y);
            // Multiplicar, no sustituir: el nodo del FBX trae escala 100 y el
            // tamano se ha medido con ella puesta. Sustituirla dejaba al
            // abrazacaras en 6 mm y al huevo en 1 cm - "solo se ve el nombre".
            model.transform.localScale *= skin.Metres / measure / PhaseScale(view.AnimalInstance);

            foreach (var r in renderers) r.forceRenderingOff = true;

            var body = view.gameObject.AddComponent<HiveBody>();
            body.Setup(ours, wolf, Texture(skin.Texture), model.transform, walker);

            if (Said.Add(id))
            {
                GMPlugin.Log?.LogInfo(
                    $"[hive] '{id}' lleva ya '{skin.Mesh}' ({size.x:0.00} x {size.y:0.00} x "
                    + $"{size.z:0.00} -> {skin.Metres:0.00} m de {(walker ? "largo" : "alto")})");
            }
        }

        /// <summary>
        /// Lo que <c>AnimalView.SetScale</c> encoge la vista por la fase de vida
        /// (<c>scaleStart</c>/<c>scaleEnd</c> de AnimalBase.json). El modelo
        /// cuelga de esa vista, asi que sin deshacerlo el abrazacaras (fase a
        /// 0,3) media 1,8 x 0,3 = 0,54 m en el mundo, y el huevo (0,55) la mitad.
        /// </summary>
        private static float PhaseScale(AnimalInstance animal)
        {
            if (animal == null || !animal.HasLifePhases() || animal.LifePhase == null) return 1f;
            var phase = animal.LifePhase;
            return Mathf.Max(0.05f, Mathf.Lerp(phase.ScaleStart, phase.ScaleEnd, animal.GetLifePhasePercent()));
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (var i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        /// <summary>
        /// El PNG que deja la compilacion de Blender en Data/Textures del mod.
        /// Igual que el del Corredor: filtro de punto y no bilineal, que son
        /// paletas pequenas y el filtro las emborrona.
        /// </summary>
        internal static Texture2D Texture(string file)
        {
            Texture2D known;
            if (Skins.TryGetValue(file, out known) && known != null) return known;

            foreach (var mod in GameAccess.AllMods())
            {
                if (mod == null || string.IsNullOrEmpty(mod.DataPath)) continue;

                var path = Path.Combine(Path.Combine(mod.DataPath, "Textures"), file);
                if (!File.Exists(path)) continue;

                var made = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = Path.GetFileNameWithoutExtension(file),
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };

                if (!made.LoadImage(File.ReadAllBytes(path))) continue;

                Skins[file] = made;
                GMPlugin.Log?.LogInfo($"[hive] textura {path} ({made.width}x{made.height})");
                return made;
            }

            return null;
        }

        internal static void Paint(Renderer ours, Renderer wolf, Texture2D texture)
        {
            var block = new MaterialPropertyBlock();
            wolf.GetPropertyBlock(block);
            if (texture != null) block.SetTexture(Diffuse, texture);
            ours.SetPropertyBlock(block);
        }
    }

    /// <summary>
    /// Lo que mantiene puesta la textura. El juego repinta el bloque de
    /// propiedades del lobo cuando le parece - x-ray, capas, cambio de tipo - y
    /// cada repintado se lleva por delante el nuestro si no se vuelve a poner.
    /// </summary>
    internal sealed class HiveBody : MonoBehaviour
    {
        private Renderer ours;
        private Renderer wolf;
        private Texture2D skin;
        private float next;

        private Transform model;
        private bool walks;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 restScale;
        private Vector3 last;
        private float speed;
        private float phase;
        private float hatchUntil;
        private float hatchLength;

        /// <summary>El meneo del huevo mientras sale el abrazacaras.</summary>
        internal void Hatch(float seconds)
        {
            hatchLength = Mathf.Max(0.1f, seconds);
            hatchUntil = Time.time + hatchLength;
        }

        internal void Setup(Renderer mine, Renderer carrier, Texture2D texture,
            Transform body, bool scuttles)
        {
            ours = mine;
            wolf = carrier;
            skin = texture;
            model = body;
            walks = scuttles;

            restPosition = model.localPosition;
            restRotation = model.localRotation;
            restScale = model.localScale;
            last = transform.position;
            phase = UnityEngine.Random.Range(0f, 10f);

            if (walks) Rig(mine.GetComponent<MeshFilter>());

            XenoHiveModel.Paint(ours, wolf, skin);
        }

        // Las patas y la cola, sin huesos: cada vertice sabe de que pata es y
        // cada frame se reescribe. La malla es de 418 vertices; es mas barato
        // que un SkinnedMeshRenderer. Coordenadas de la malla (antes del giro
        // del nodo): x ancho, y largo (+y cabeza, -y cola), z arriba.
        private static readonly Vector2 Hub = new Vector2(0f, 0.12f);
        private const float TailStart = 0.06f;
        private const float TailHalfWidth = 0.035f;
        private const float BodyRadius = 0.045f;
        private static bool saidStiff;

        private Mesh flesh;
        private Vector3[] rest;
        private Vector3[] moved;
        private float[] leg;   // 0 cuerpo; >0 cuanto de pata es (0..1 hacia la punta)
        private bool[] first;  // grupo de marcha: la mitad de las patas va con la otra mitad parada
        private float[] tail;  // 0..1 a lo largo de la cola

        private void Rig(MeshFilter filter)
        {
            if (filter == null || filter.sharedMesh == null) return;
            if (!filter.sharedMesh.isReadable)
            {
                if (!saidStiff)
                {
                    saidStiff = true;
                    GMPlugin.Log?.LogWarning(
                        "[hive] la malla del abrazacaras no es legible: anda sin mover patas. "
                        + "?Esta publicado el bundle nuevo de XenomorphRunner?");
                }
                return;
            }

            flesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            flesh.MarkDynamic();
            filter.sharedMesh = flesh;

            rest = flesh.vertices;
            moved = new Vector3[rest.Length];
            leg = new float[rest.Length];
            first = new bool[rest.Length];
            tail = new float[rest.Length];

            for (var i = 0; i < rest.Length; i++)
            {
                var v = rest[i];
                if (v.y < TailStart && Mathf.Abs(v.x) < TailHalfWidth)
                {
                    tail[i] = Mathf.Clamp01((TailStart - v.y) / 0.36f);
                    continue;
                }

                var d = new Vector2(v.x, v.y) - Hub;
                if (d.magnitude < BodyRadius) continue;

                leg[i] = Mathf.Clamp01((d.magnitude - BodyRadius) / 0.15f);
                // Cuatro patas por lado, de delante (0) a atras (3), y se
                // alternan como en una arana: 1a y 3a izquierda con 2a y 4a
                // derecha.
                // Los cortes salen de medir las puntas en el bundle: las patas
                // apuntan a ~30, ~50, ~80 y ~110 grados desde el frente.
                var fromFront = Mathf.Atan2(Mathf.Abs(d.x), d.y) * Mathf.Rad2Deg;
                var index = fromFront < 38f ? 0 : fromFront < 65f ? 1 : fromFront < 95f ? 2 : 3;
                first[i] = (index + (v.x > 0f ? 1 : 0)) % 2 == 0;
            }
        }

        private void Scuttle(float quick)
        {
            if (flesh == null) return;

            // Parado no se queda muerto: las patas tiemblan un poco.
            var stride = Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(quick / 1.5f));
            var swingA = Mathf.Sin(phase) * 16f * stride;
            var liftA = Mathf.Max(0f, Mathf.Cos(phase)) * 0.025f * stride;
            var swingB = -swingA;
            var liftB = Mathf.Max(0f, -Mathf.Cos(phase)) * 0.025f * stride;
            var whip = Time.time * 5f + phase * 0.5f;

            for (var i = 0; i < rest.Length; i++)
            {
                var v = rest[i];
                if (leg[i] > 0f)
                {
                    var swing = (first[i] ? swingA : swingB) * leg[i];
                    var d = new Vector2(v.x, v.y) - Hub;
                    var turned = (Vector2)(Quaternion.Euler(0f, 0f, swing) * d) + Hub;
                    v.x = turned.x;
                    v.y = turned.y;
                    v.z += (first[i] ? liftA : liftB) * leg[i];
                }
                else if (tail[i] > 0f)
                {
                    // Latigo: la onda baja por la cola, mas amplia en la punta.
                    var t = tail[i];
                    v.x += Mathf.Sin(whip - t * 4f) * 0.05f * t * t * (0.5f + stride);
                    v.z += Mathf.Sin(whip * 0.7f - t * 3f) * 0.012f * t;
                }

                moved[i] = v;
            }

            flesh.vertices = moved;
        }

        private void OnDestroy()
        {
            if (flesh != null) UnityEngine.Object.Destroy(flesh);
        }

        private void Update()
        {
            if (ours == null || wolf == null) return;
            if (Time.unscaledTime < next) return;

            next = Time.unscaledTime + 1f;
            XenoHiveModel.Paint(ours, wolf, skin);
        }

        /// <summary>
        /// El meneo, y es a proposito lo mas barato que se lee bien.
        ///
        /// <b>Por que no lleva esqueleto.</b> El Corredor venia en 89 piezas y
        /// de cada nodo salio un hueso; el abrazacaras viene ya como **una**
        /// malla de 238 vertices con su UV, asi que darle huesos no es derivar
        /// nada, es rigear a mano un bicho que en pantalla mide sesenta
        /// centimetros y se pasa la vida pegado a una cara. Por el precio de
        /// eso se pueden tener las dos cosas que de verdad se notan a esa
        /// escala: que **corretee** en vez de deslizarse, y que **respire**.
        ///
        /// Correteo: un bote corto y un balanceo, los dos al mismo compas y
        /// ese compas marcado por lo rapido que va - parado no hay bote -. Va
        /// en <c>LateUpdate</c> por lo mismo que la cola del Corredor: el juego
        /// escribe las transformaciones en Update.
        ///
        /// El huevo no respira: se pidio que solo se mueva mientras sale el
        /// abrazacaras. Ese meneo lo arranca <see cref="Hatch"/>, que llama
        /// <c>Facehugger.Open</c> al abrirlo.
        /// </summary>
        private void LateUpdate()
        {
            if (model == null) return;

            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            var now = transform.position;
            var step = new Vector2(now.x - last.x, now.z - last.z).magnitude / dt;
            last = now;
            speed = Mathf.Lerp(speed, step, Mathf.Clamp01(dt * 6f));

            if (!walks)
            {
                // El huevo respira despacio (se pidio de vuelta el 26) hasta
                // que se abre; entonces el meneo manda.
                if (hatchUntil <= 0f)
                {
                    var swell = Mathf.Sin(Time.time * 0.9f + phase);
                    model.localScale = Vector3.Scale(restScale,
                        new Vector3(1f + 0.03f * swell, 1f + 0.015f * swell, 1f + 0.03f * swell));
                    return;
                }

                var left = hatchUntil - Time.time;
                if (left <= 0f)
                {
                    hatchUntil = 0f;
                    model.localPosition = restPosition;
                    model.localRotation = restRotation;
                    model.localScale = restScale;
                    return;
                }

                // Sacudidas que crecen hacia el final, y el huevo que se
                // ensancha arriba justo cuando sale.
                var done = 1f - left / Mathf.Max(0.01f, hatchLength);
                var shake = Mathf.Sin(Time.time * 38f) * (4f + 10f * done);
                var twist = Mathf.Sin(Time.time * 23f + 1.3f) * (2f + 6f * done);
                model.localRotation = restRotation * Quaternion.Euler(shake, 0f, twist);
                model.localScale = Vector3.Scale(restScale,
                    new Vector3(1f + 0.18f * done, 1f - 0.08f * done, 1f + 0.18f * done));
                return;
            }

            var quick = Mathf.Clamp(speed, 0f, 6f);
            phase += dt * (6f + quick * 3.5f);

            // Con patas que se mueven, el bote y el balanceo solo acompanan.
            var hop = Mathf.Abs(Mathf.Sin(phase)) * 0.02f * Mathf.Clamp01(quick / 1.5f);
            var sway = Mathf.Sin(phase) * 3f * Mathf.Clamp01(quick / 2f);
            var breathe = 1f + 0.02f * Mathf.Sin(Time.time * 2.2f);

            model.localPosition = restPosition + Vector3.up * hop;
            model.localRotation = Quaternion.Euler(0f, sway, 0f) * restRotation;
            model.localScale = restScale * breathe;
            Scuttle(quick);
        }
    }
}
