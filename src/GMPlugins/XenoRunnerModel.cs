using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NSEipix.Base;
using NSEipix.Repository;
using NSMedieval.CombatAi;
using NSMedieval.Model;
using NSMedieval.Repository;
using NSMedieval.State;
using NSMedieval.View.Animals;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// The Runner wears its own body.
    ///
    /// <b>Why the wolf is still underneath.</b> An animal prefab is not a
    /// model: it carries <c>AnimalView</c> and the rest of the game's
    /// components, and a bundle built in our Unity project cannot reference
    /// those - <c>Assembly-CSharp</c> is a reserved name there. So the wolf
    /// stays as the carrier of the logic - pathing, selection, combat - and
    /// is made invisible, and the Xeno rides on it as a model of its own,
    /// with its own skeleton and its own five clips, loaded from the
    /// <c>aldrich_xeno_runner</c> prefab of the XenomorphRunner bundle
    /// (<c>tools/models/build_xeno_runner.py</c> ->
    /// <c>BuildModBundles</c>).
    ///
    /// <b>Why this method.</b> <c>AnimalView.SetMaterialBasedOnType</c> runs
    /// at the end of <c>Setup</c> and again whenever the animal changes type,
    /// and it writes <c>_Diffuse</c> - the wolf's fur - into the property
    /// block of every renderer under the view, ours included. Dressing here
    /// means the first dressing and every repaint after it put our texture
    /// back last.
    ///
    /// <b>Hidden, not disabled.</b> <c>forceRenderingOff</c> rather than
    /// <c>enabled = false</c>: the game toggles renderers for its own reasons
    /// (layers, x-ray), and a flag it never touches survives that.
    /// </summary>
    [HarmonyPatch]
    internal static class XenoRunnerModel
    {
        internal const string AnimalId = "xeno_runner";
        private const string PrefabAddress = "aldrich_xeno_runner";
        private const string TextureFile = "aldrich_xeno_runner.png";
        private static readonly int Diffuse = Shader.PropertyToID("_Diffuse");

        private static Texture2D texture;
        private static bool warnedPrefab;
        private static bool describedShader;

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
                if (animal.Id != AnimalId && animal.Blueprint?.GetID() != AnimalId) return;

                var body = __instance.GetComponent<XenoBody>() ?? Dress(__instance);
                body?.Repaint();
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[xeno] could not dress a runner: {e}");
            }
        }

        private static XenoBody Dress(AnimalView view)
        {
            // MeshRepository, not PrefabRepository: the game's mod loader
            // collects the Prefab label and then never loads it, so the bundle
            // ships the model under Mesh, which keeps the whole GameObject.
            var prefab = MonoRepository<MeshRepository, KeyGameObjectPair>.Instance?.GetByAddress(PrefabAddress);
            if (prefab == null || prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
            {
                if (!warnedPrefab)
                {
                    warnedPrefab = true;
                    GMPlugin.Log?.LogWarning(
                        $"[xeno] model '{PrefabAddress}' is not in MeshRepository - "
                        + "the runner stays a wolf. Is the XenomorphRunner bundle published?");
                }
                return null;
            }

            var wolf = view.GetComponentsInChildren<Renderer>(true);
            Renderer skin = null;
            foreach (var r in wolf)
            {
                if (r is SkinnedMeshRenderer) { skin = r; break; }
            }
            if (skin == null && wolf.Length > 0) skin = wolf[0];
            if (skin == null) return null;

            var unit = Mathf.Max(0.0001f, view.transform.lossyScale.y);
            var wolfHeight = skin.bounds.size.y / unit;

            var model = UnityEngine.Object.Instantiate(prefab, view.transform, false);
            model.name = "AldrichXenoBody";
            SetLayer(model.transform, skin.gameObject.layer);

            var ours = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (ours == null)
            {
                UnityEngine.Object.Destroy(model);
                GMPlugin.Log?.LogError("[xeno] the prefab has no SkinnedMeshRenderer");
                return null;
            }

            ours.sharedMaterial = skin.sharedMaterial;
            ours.shadowCastingMode = skin.shadowCastingMode;
            ours.receiveShadows = skin.receiveShadows;
            ours.updateWhenOffscreen = true;

            // Height against height: the wolf's is the one number that does not
            // change with its yaw. Then the tail is kept from running off with
            // the whole thing - the runner is long, and the grid is not.
            model.transform.localRotation = Quaternion.Euler(0f, GMPlugin.XenoYaw.Value, 0f);
            var size = ours.bounds.size / unit;
            var scale = wolfHeight / Mathf.Max(0.0001f, size.y) * GMPlugin.XenoScale.Value;
            var length = Mathf.Max(size.x, size.z) * scale;
            if (length > GMPlugin.XenoMaxLength.Value) scale *= GMPlugin.XenoMaxLength.Value / length;
            model.transform.localScale = Vector3.one * scale;

            foreach (var r in wolf) r.forceRenderingOff = true;

            // An Animator whose renderers are all off stops under the default
            // culling - and the wolf's Animator is what fires the "shoot"
            // event that makes a bite land. Hidden or not, it has to keep
            // playing, or the runner charges its attack and never hits.
            foreach (var animator in view.GetComponentsInChildren<Animator>(true))
            {
                if (animator.transform.IsChildOf(model.transform)) continue;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            if (!describedShader)
            {
                describedShader = true;
                var shader = skin.sharedMaterial != null ? skin.sharedMaterial.shader : null;
                GMPlugin.Log?.LogInfo(
                    $"[xeno] dressing runners: wolf renderer '{skin.name}' shader "
                    + $"'{(shader != null ? shader.name : "none")}', wolf height {wolfHeight:0.00}, "
                    + $"model {size.x:0.00} x {size.y:0.00} x {size.z:0.00}, scale {scale:0.000}");
            }

            var body = view.gameObject.AddComponent<XenoBody>();
            body.Setup(view, ours, skin, model.GetComponent<Animation>(), scale);
            return body;
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (var i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        /// <summary>
        /// The PNG the Blender build leaves in the mod's Data/Textures. Point
        /// filtered: it is a 128 px palette, and bilinear smears the swatches
        /// into each other.
        /// </summary>
        internal static Texture2D Texture()
        {
            if (texture != null) return texture;

            foreach (var mod in GameAccess.AllMods())
            {
                if (mod == null || string.IsNullOrEmpty(mod.DataPath)) continue;
                var path = Path.Combine(Path.Combine(mod.DataPath, "Textures"), TextureFile);
                if (!File.Exists(path)) continue;

                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "aldrich_xeno_runner",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                if (t.LoadImage(File.ReadAllBytes(path)))
                {
                    texture = t;
                    GMPlugin.Log?.LogInfo($"[xeno] texture {path} ({t.width}x{t.height})");
                    break;
                }
            }

            return texture;
        }

        internal static void Paint(Renderer ours, Renderer wolf)
        {
            // Start from the wolf's block: it carries the x-ray value and the
            // carapace colour the JSON asks for. Only the texture is ours.
            var block = new MaterialPropertyBlock();
            wolf.GetPropertyBlock(block);
            var tex = Texture();
            if (tex != null) block.SetTexture(Diffuse, tex);
            ours.SetPropertyBlock(block);
        }
    }

    /// <summary>
    /// Plays the right clip for what the carrier underneath is doing.
    ///
    /// The game drives the wolf's Animator, which is invisible now; all this
    /// sees is how fast the view moves and when it last attacked. Standing is
    /// idle - with idle2 now and then -, walking pace is running, anything
    /// faster is sprinting, a bite is a short roar with a lunge forward, and
    /// it roars once when it first appears. On death it goes down on its side
    /// over a few frames instead of snapping there.
    ///
    /// <b>Tuned on the 21st.</b> Running and sprinting had one threshold
    /// between them and a runner near it flickered between the two clips;
    /// now it takes more speed to start sprinting than to keep doing it. And
    /// the stride rate is divided by the model's scale: a bigger body covers
    /// more ground per step, so at the same speed its legs move slower -
    /// otherwise a larger runner looks like it is skating.
    /// </summary>
    internal sealed class XenoBody : MonoBehaviour
    {
        private const float SprintEnter = 4.6f;
        private const float SprintExit = 3.8f;
        private const float MoveAbove = 0.25f;

        /// <summary>The scale the clip speeds were first judged at.</summary>
        private const float ReferenceScale = 0.627f;

        private const float LungeSeconds = 0.35f;
        private const float LungeDistance = 0.35f;
        private const float FallSeconds = 0.45f;

        private AnimalView view;
        private Renderer ours;
        private Renderer wolf;
        private Animation clips;
        private Transform model;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float stride = 1f;
        private Vector3 last;
        private float speed;
        private bool sprinting;
        private float nextIdleVariant;
        private object lastAttack;
        private float lungeStarted = -1f;
        private float fallStarted = -1f;

        // La trepada. Ver XenoClimb para cuando y a donde.
        private Vector3[] climbPath;
        private float[] climbAt;
        private Vector3 climbFace;
        private float climbStarted = -1f;
        private float climbSeconds;

        internal bool Climbing => climbStarted >= 0f;

        internal void Setup(AnimalView owner, Renderer body, Renderer carrier, Animation animation, float scale)
        {
            view = owner;
            ours = body;
            wolf = carrier;
            clips = animation;
            model = clips != null ? clips.transform : body.transform;
            restPosition = model.localPosition;
            restRotation = model.localRotation;
            stride = Mathf.Max(0.2f, scale / ReferenceScale);
            last = transform.position;
            nextIdleVariant = Time.time + UnityEngine.Random.Range(8f, 20f);
            lastAttack = LastAttack(view.AnimalInstance);

            if (clips != null && clips["roar"] != null)
            {
                clips.Play("roar");
                clips.CrossFadeQueued("idle", 0.3f);
            }
        }

        internal void Repaint()
        {
            if (ours != null && wolf != null) XenoRunnerModel.Paint(ours, wolf);
        }

        // ------------------------------------------------------------------
        // La cola. Ver XenoTail para el golpe; esto es solo el latigazo.

        /// <summary>Lo que dura un coletazo, en segundos.</summary>
        private const float LashSeconds = 0.34f;

        /// <summary>Cuanto gira cada hueso de la cola en el punto alto.</summary>
        private const float LashDegrees = 26f;

        private Transform[] tail;
        private Quaternion[] tailRest;
        private float lashStarted = -1f;

        /// <summary>Lo llama <see cref="XenoTail"/> cuando el bicho sacude.</summary>
        internal void Lash()
        {
            if (tail == null) FindTail();
            if (tail.Length == 0) return;

            lashStarted = Time.time;
        }

        /// <summary>
        /// Los huesos de la cola, por nombre y una sola vez. El rig se fabrico
        /// de piezas rigidas y sus huesos se llaman <c>b000</c>..<c>b156</c>,
        /// asi que la lista vive en <see cref="XenoTail"/>, que es donde se
        /// explica de donde salio.
        /// </summary>
        private void FindTail()
        {
            var found = new List<Transform>();
            var rest = new List<Quaternion>();

            foreach (var name in XenoTail.Bones)
            {
                var bone = Find(model, name);
                if (bone == null) continue;

                found.Add(bone);
                rest.Add(bone.localRotation);
            }

            tail = found.ToArray();
            tailRest = rest.ToArray();

            if (tail.Length == 0)
            {
                GMPlugin.Log?.LogWarning(
                    "[xeno] no se encontro la cola en el modelo: el coletazo pega pero no se ve");
            }
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var hit = Find(root.GetChild(i), name);
                if (hit != null) return hit;
            }

            return null;
        }

        /// <summary>
        /// El latigazo se escribe **despues** del clip. El <c>Animation</c>
        /// legacy pone los huesos en Update; lo que se escriba antes que el se
        /// pierde en el mismo fotograma.
        ///
        /// Cada hueso gira un poco y el de mas adelante se suma al de atras,
        /// que es lo que curva una cadena: nueve huesos a 26 grados no son 26
        /// grados, son una C que barre de un lado al otro. Va y vuelve en un
        /// tercio de segundo.
        /// </summary>
        private void LateUpdate()
        {
            if (Climbing) ClimbStep();

            if (lashStarted < 0f || tail == null || tail.Length == 0) return;

            var t = (Time.time - lashStarted) / LashSeconds;
            if (t >= 1f)
            {
                for (var i = 0; i < tail.Length; i++)
                {
                    if (tail[i] != null) tail[i].localRotation = tailRest[i];
                }

                lashStarted = -1f;
                return;
            }

            // Fuera rapido y vuelta mas lenta, como el mordisco.
            var swing = t < 0.4f
                ? Mathf.Sin(t / 0.4f * Mathf.PI * 0.5f)
                : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.4f) / 0.6f);

            for (var i = 0; i < tail.Length; i++)
            {
                if (tail[i] == null) continue;

                var reach = LashDegrees * swing * (0.4f + 0.6f * i / tail.Length);
                tail[i].localRotation = tailRest[i] * Quaternion.Euler(0f, reach, 0f);
            }
        }

        private static object LastAttack(AnimalInstance animal)
        {
            var ai = animal?.CombatAi;
            return ai == null ? null : ai.GetState<object>(CombatAiState.LastAttackTime);
        }

        private void Update()
        {
            if (clips == null || view == null) return;

            var animal = view.AnimalInstance;
            if (animal == null || animal.HasDisposed) return;

            if (animal.HasDied)
            {
                if (Climbing) EndClimb();
                Fall();
                return;
            }

            if (Climbing)
            {
                Loop("running", 1.2f);
                return;
            }

            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            var now = transform.position;
            var step = new Vector2(now.x - last.x, now.z - last.z).magnitude / dt;
            last = now;
            speed = Mathf.Lerp(speed, step, Mathf.Clamp01(dt * 6f));

            Lunge(animal);

            if (clips.IsPlaying("roar")) return;

            sprinting = sprinting ? speed > SprintExit : speed > SprintEnter;

            if (sprinting)
            {
                Loop("sprinting", Mathf.Clamp(speed / 7f / stride, 0.6f, 1.8f));
            }
            else if (speed > MoveAbove)
            {
                Loop("running", Mathf.Clamp(speed / 3f / stride, 0.45f, 1.5f));
            }
            else if (Time.time >= nextIdleVariant && !clips.IsPlaying("idle2"))
            {
                nextIdleVariant = Time.time + UnityEngine.Random.Range(12f, 30f);
                clips.CrossFade("idle2", 0.25f);
                clips.CrossFadeQueued("idle", 0.25f);
            }
            else if (!clips.IsPlaying("idle2"))
            {
                Loop("idle", 1f);
            }
        }

        /// <summary>
        /// A bite: the game moves <c>LastAttackTime</c> when the attack lands,
        /// so a change there is a bite. There is no bite clip, so it is the
        /// roar played fast, with the body thrown forward and back along its
        /// own length.
        /// </summary>
        private void Lunge(AnimalInstance animal)
        {
            var struck = LastAttack(animal);
            if (struck != null && !Equals(struck, lastAttack))
            {
                lastAttack = struck;
                lungeStarted = Time.time;
                XenoNoise.Bite(transform.position);

                var roar = clips["roar"];
                if (roar != null)
                {
                    roar.speed = 2.2f;
                    clips.CrossFade("roar", 0.08f);
                    clips.CrossFadeQueued(speed > MoveAbove ? "running" : "idle", 0.15f);
                }
            }

            if (lungeStarted < 0f) return;

            var t = (Time.time - lungeStarted) / LungeSeconds;
            if (t >= 1f)
            {
                lungeStarted = -1f;
                model.localPosition = restPosition;
                return;
            }

            // Out fast, back slower.
            var reach = t < 0.35f
                ? Mathf.Sin(t / 0.35f * Mathf.PI * 0.5f)
                : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.35f) / 0.65f);
            var forward = restRotation * Vector3.forward;
            var unit = Mathf.Max(0.0001f, transform.lossyScale.z);
            model.localPosition = restPosition + forward * (LungeDistance * reach / unit);
        }

        private void Fall()
        {
            if (fallStarted < 0f)
            {
                fallStarted = Time.time;
                clips.Stop();
                model.localPosition = restPosition;
            }

            var down = restRotation * Quaternion.Euler(0f, 0f, 90f);
            var t = Mathf.Clamp01((Time.time - fallStarted) / FallSeconds);
            var eased = t * t;
            model.localRotation = Quaternion.Slerp(restRotation, down, eased);
            model.localPosition = restPosition + Vector3.up * (0.1f * eased);

            if (t >= 1f) enabled = false;
        }

        /// <summary>
        /// Recorre la pared: hasta el pie del muro, arriba por su cara con la
        /// cabeza al cielo y la tripa contra la piedra, por encima, y abajo al
        /// sitio de llegada con la cabeza por delante. El cuerpo ya esta en la
        /// llegada; el modelo se pinta en coordenadas de mundo y al acabar
        /// vuelve a su sitio. Devuelve lo que dura, en segundos.
        /// </summary>
        internal float Climb(Vector3 from, Vector3 crest, Vector3 to, Vector3 face)
        {
            var reach = new Vector2(crest.x - from.x, crest.z - from.z).magnitude;
            var foot = from + face * (reach * 0.45f);
            var lip = new Vector3(foot.x, crest.y, foot.z);

            climbPath = new[] { from, foot, lip, crest, to };
            climbAt = new float[climbPath.Length];
            for (var i = 1; i < climbPath.Length; i++)
            {
                climbAt[i] = climbAt[i - 1] + Vector3.Distance(climbPath[i - 1], climbPath[i]);
            }

            var up = Mathf.Max(0f, crest.y - from.y);
            var down = Mathf.Max(0f, crest.y - to.y);
            climbSeconds = Mathf.Clamp(0.8f + up * 0.3f + down * 0.12f, 1.2f, 7f);
            climbFace = face;
            climbStarted = Time.time;
            lungeStarted = -1f;
            return climbSeconds;
        }

        private void ClimbStep()
        {
            var t = (Time.time - climbStarted) / climbSeconds;
            if (t >= 1f)
            {
                EndClimb();
                return;
            }

            var total = climbAt[climbAt.Length - 1];
            var along = Mathf.SmoothStep(0f, 1f, t) * total;
            var leg = 1;
            while (leg < climbAt.Length - 1 && along > climbAt[leg]) leg++;

            var span = Mathf.Max(0.0001f, climbAt[leg] - climbAt[leg - 1]);
            var a = climbPath[leg - 1];
            var b = climbPath[leg];
            model.position = Vector3.Lerp(a, b, (along - climbAt[leg - 1]) / span);

            // Tramo 2 por la cara del muro; el ultimo, si cae, de cabeza.
            Quaternion facing;
            if (leg == 2) facing = Quaternion.LookRotation(Vector3.up, -climbFace);
            else if (leg == 4 && a.y - b.y > 0.3f) facing = Quaternion.LookRotation((b - a).normalized, climbFace);
            else facing = Quaternion.LookRotation(climbFace);

            var wanted = facing * restRotation;
            model.rotation = Quaternion.Slerp(model.rotation, wanted, Mathf.Clamp01(Time.deltaTime * 12f));
        }

        private void EndClimb()
        {
            climbStarted = -1f;
            climbPath = null;
            model.localPosition = restPosition;
            model.localRotation = restRotation;
        }

        private void Loop(string name, float rate)
        {
            var state = clips[name];
            if (state == null) return;
            state.speed = rate;
            if (!clips.IsPlaying(name)) clips.CrossFade(name, 0.2f);
        }
    }
}
