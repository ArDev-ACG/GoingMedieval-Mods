using System;
using System.Collections.Generic;
using NSEipix.Base;
using NSMedieval;
using NSMedieval.Controllers;
using NSMedieval.Map;
using NSMedieval.State;
using NSMedieval.Types;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// A minimap under the top bar (or bottom-right, see Corner), in the manner of RimWorld's
    /// "Minimap" mod.
    ///
    /// <b>All of it is ours: the game has none.</b> There is no minimap type
    /// in the assembly to switch on. What there is, is the raw material:
    /// <c>Heightmap</c> answers the surface height of every column, and the
    /// living things come from the same three lists <see cref="RisenHunt"/>
    /// already sweeps.
    ///
    /// <b>Decided on the 20th, before a line was written.</b> Surface only -
    /// no underground floors -, the bottom-right corner, and a click or drag
    /// on the map moves the camera there.
    ///
    /// <b>Why uGUI and not OnGUI.</b> The click. An IMGUI rectangle is
    /// invisible to the game's <c>EventSystem</c>, so a click on the map would
    /// also land in the world behind it and select or deselect whatever is
    /// there. A canvas with a <c>GraphicRaycaster</c> is what the game's own
    /// panels are, and the world already ignores clicks that hit one.
    ///
    /// <b>Three layers.</b> The terrain is one texture painted from the
    /// heightmap and repainted only when <c>HeightChangedAtEvent</c> says
    /// something dug or built, at most every couple of seconds. The camera's
    /// view is a second texture of the same size with nothing but the outline
    /// on it. The creatures are small UI squares from a pool, so they stay
    /// crisp however large the map is next to the panel.
    /// </summary>
    internal sealed class Minimap : MonoBehaviour
    {
        private const float TerrainRepaintSeconds = 3f;
        private const float MarkersSeconds = 0.25f;
        private const int Border = 3;

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 ViewLine = new Color32(255, 255, 255, 230);

        private static readonly Color Settler = new Color(0.30f, 0.88f, 0.42f);
        private static readonly Color Visitor = new Color(0.35f, 0.63f, 1f);
        private static readonly Color Prisoner = new Color(0.75f, 0.75f, 0.75f);
        private static readonly Color Enemy = new Color(1f, 0.23f, 0.19f);
        private static readonly Color Undead = new Color(0.70f, 0.36f, 1f);
        private static readonly Color Predator = new Color(1f, 0.60f, 0.18f);
        private static readonly Color Livestock = new Color(0.95f, 0.89f, 0.55f);
        private static readonly Color Wildlife = new Color(0.72f, 0.64f, 0.50f);

        private static Minimap live;

        private RectTransform panel;
        private RectTransform mapRect;
        private RawImage terrainImage;
        private RawImage viewImage;
        private Texture2D terrain;
        private Texture2D view;
        private Color32[] viewPixels;
        private readonly List<Image> markers = new List<Image>();

        private Heightmap heightmap;
        private int sizeX;
        private int sizeZ;

        /// <summary>
        /// How many times the map is magnified. 1 is the whole world, which is
        /// what it always was; anything more is a window of <c>size/zoom</c>
        /// tiles kept centred on the camera. The terrain and the view outline
        /// are textures of the whole world, so the window costs nothing: it is
        /// a <c>uvRect</c>. The markers are UI children and get the same
        /// window applied by hand in <see cref="PlaceMarkers"/>.
        /// </summary>
        private int zoom = 1;

        /// <summary>The window in world tiles: x, z, width, height.</summary>
        private Rect window;
        private bool terrainDirty;
        private float nextTerrain;
        private float nextMarkers;
        private bool announced;

        /// <summary>Segundos despues de enganchar un mapa en que se vuelve a pintar.</summary>
        private static readonly float[] SettleAfter = { 1f, 3f, 6f, 12f, 25f, 45f };
        private readonly Queue<float> settleRepaints = new Queue<float>();

        /// <summary>El raton esta encima del mapa (lo pone MinimapClick).</summary>
        internal bool Hovering;

        private RectTransform tipRect;
        private TMPro.TextMeshProUGUI tipText;
        private bool tipUnavailable;
        private int tipColumn = -1;

        /// <remarks>
        /// <b>Why the third build never showed.</b> The host was
        /// <c>DontDestroyOnLoad</c> and nothing else, and in this game that is
        /// not enough: the plugin's own coroutines died the same silent death
        /// (see <see cref="Heartbeat"/>), and the log of the 20th has
        /// "started, waiting for a map" and then not one line of the minimap,
        /// not even the "hidden: ..." that its Update writes on its first
        /// frame. So the host gets the same <c>HideAndDontSave</c> the
        /// heartbeat has, and the heartbeat checks every few seconds that it
        /// is still there and builds it again if it is not - saying so.
        /// </remarks>
        internal static void Start()
        {
            if (!GMPlugin.MinimapEnabled.Value) return;

            Create("started, waiting for a map");
            GMPlugin.Every("minimap watch", 5f, () =>
            {
                if (live == null) Create("host was destroyed; built it again");
            });
        }

        private static void Create(string what)
        {
            if (live != null) return;

            try
            {
                var host = new GameObject("Aldrich_Minimap");
                DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.HideAndDontSave;
                live = host.AddComponent<Minimap>();
                live.Build();
                GMPlugin.Log?.LogInfo($"[minimap] {what}");
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[minimap] could not start: {e}");
            }
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            gameObject.AddComponent<GraphicRaycaster>();

            var size = Mathf.Max(80, GMPlugin.MinimapSize.Value);

            panel = NewChild("Frame", transform);
            panel.sizeDelta = new Vector2(size + 2 * Border, size + 2 * Border);
            if (GMPlugin.MinimapCorner?.Value == "BottomRight")
            {
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 0f);
                panel.anchoredPosition = new Vector2(-GMPlugin.MinimapOffsetX.Value, GMPlugin.MinimapOffsetY.Value);
            }
            else
            {
                // La zona roja: pegado arriba, a la izquierda de los avisos.
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
                panel.anchoredPosition = new Vector2(-GMPlugin.MinimapTopOffsetX.Value, -GMPlugin.MinimapTopOffsetY.Value);
            }
            var frame = panel.gameObject.AddComponent<Image>();
            frame.color = new Color(0.08f, 0.07f, 0.06f, 0.85f);

            mapRect = NewChild("Terrain", panel);
            mapRect.anchorMin = Vector2.zero;
            mapRect.anchorMax = Vector2.one;
            mapRect.offsetMin = new Vector2(Border, Border);
            mapRect.offsetMax = new Vector2(-Border, -Border);
            terrainImage = mapRect.gameObject.AddComponent<RawImage>();
            terrainImage.color = Color.white;
            mapRect.gameObject.AddComponent<MinimapClick>().Map = this;

            var viewRect = NewChild("View", mapRect);
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = viewRect.offsetMax = Vector2.zero;
            viewImage = viewRect.gameObject.AddComponent<RawImage>();
            viewImage.raycastTarget = false;

            zoom = Mathf.Clamp(GMPlugin.MinimapZoom.Value, 1, MaxZoom);
            ZoomButton("Zoom in", 0f, true);
            ZoomButton("Zoom out", 22f, false);

            panel.gameObject.SetActive(false);
        }

        private const int MaxZoom = 6;
        private const float ButtonSide = 20f;

        /// <summary>
        /// A square button in the top-right corner of the frame, drawn out of
        /// plain rectangles and not out of text: a uGUI <c>Text</c> needs a
        /// font, and which builtin font this Unity answers to - Arial or
        /// LegacyRuntime - is not the sort of thing to find out in a player's
        /// log.
        /// </summary>
        private void ZoomButton(string name, float down, bool plus)
        {
            var rect = NewChild(name, panel);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(ButtonSide, ButtonSide);
            rect.anchoredPosition = new Vector2(-Border - 1f, -Border - 1f - down);

            var face = rect.gameObject.AddComponent<Image>();
            face.color = new Color(0.08f, 0.07f, 0.06f, 0.80f);

            Bar(rect, new Vector2(11f, 2.5f));
            if (plus) Bar(rect, new Vector2(2.5f, 11f));

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            var step = plus ? 1 : -1;
            button.onClick.AddListener(() => Zoom(step));
        }

        private static void Bar(RectTransform parent, Vector2 size)
        {
            var rect = NewChild("Bar", parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.92f, 0.90f, 0.86f, 0.95f);
            image.raycastTarget = false;
        }

        private void Zoom(int step)
        {
            var wanted = Mathf.Clamp(zoom + step, 1, MaxZoom);
            if (wanted == zoom) return;
            zoom = wanted;
            GMPlugin.MinimapZoom.Value = wanted;
            nextMarkers = 0f;
            GMPlugin.Log?.LogInfo($"[minimap] zoom x{zoom}");
        }

        /// <summary>
        /// The window of world tiles on show, centred on the camera and kept
        /// inside the map, and the same window handed to the two textures.
        /// </summary>
        private void UpdateWindow()
        {
            var w = sizeX / (float)zoom;
            var h = sizeZ / (float)zoom;

            var centre = new Vector2(sizeX * 0.5f, sizeZ * 0.5f);
            if (zoom > 1 && MonoSingleton<RtsCamera>.IsInstantiated())
            {
                var at = MonoSingleton<RtsCamera>.Instance.CurrentPosition;
                centre = new Vector2(at.x, at.z);
            }

            var x = Mathf.Clamp(centre.x - w * 0.5f, 0f, Mathf.Max(0f, sizeX - w));
            var z = Mathf.Clamp(centre.y - h * 0.5f, 0f, Mathf.Max(0f, sizeZ - h));
            window = new Rect(x, z, w, h);

            var uv = new Rect(x / sizeX, z / sizeZ, w / sizeX, h / sizeZ);
            terrainImage.uvRect = uv;
            viewImage.uvRect = uv;
        }

        private static RectTransform NewChild(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private void Update()
        {
            try
            {
                if (GMPlugin.MinimapToggle.Value.IsDown())
                {
                    GMPlugin.MinimapVisible = !GMPlugin.MinimapVisible;
                }

                var map = CurrentHeightmap(out var why);
                if (map == null) Explain(why);
                else if (!GMPlugin.MinimapVisible) Explain("toggled off with " + GMPlugin.MinimapToggle.Value);
                else Explain(null);
                var show = map != null && GMPlugin.MinimapVisible;
                if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
                if (!show)
                {
                    HideTip();
                    return;
                }

                if (!ReferenceEquals(map, heightmap)) Attach(map);

                var now = Time.unscaledTime;
                if (settleRepaints.Count > 0 && now >= settleRepaints.Peek())
                {
                    settleRepaints.Dequeue();
                    terrainDirty = true;
                    wholeMap = true;
                }

                if (terrainDirty && now >= nextTerrain)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    PaintTerrain();
                    if (watch.ElapsedMilliseconds >= 20)
                    {
                        GMPlugin.Log?.LogInfo($"[fps] minimapa: repintar el terreno costo {watch.ElapsedMilliseconds} ms");
                    }
                    nextTerrain = now + TerrainRepaintSeconds;
                }

                if (now >= nextMarkers)
                {
                    // The window moves with the camera, and it moves here and
                    // not in every frame so that the terrain, the outline and
                    // the markers are always looking at the same rectangle.
                    UpdateWindow();
                    PaintView();
                    PlaceMarkers();
                    nextMarkers = now + MarkersSeconds;
                }

                Hover();
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[minimap] update failed, hiding it: {e}");
                GMPlugin.MinimapVisible = false;
            }
        }

        /// <summary>
        /// The heightmap of the map being played, or null anywhere else - the
        /// main menu, the world map, a load in progress.
        /// </summary>
        /// <remarks>
        /// Not <c>Heightmap.IsReady</c>. That flag is only raised by
        /// <c>RefreshWholeHeightmap</c>, which a saved game loaded from its
        /// cache never runs - the first build waited on it for a whole session
        /// and the map never appeared. The loading screen being over, and a
        /// height in the middle of the map, is what "playing" looks like.
        /// </remarks>
        private static Heightmap CurrentHeightmap(out string why)
        {
            why = null;
            // The loading flags are reported, not obeyed: the second build
            // waited on IsLoadingComplete and still never showed, and what
            // the map needs is only a heightmap with heights in it.
            string Flags() => $" (loadingComplete={LoadingController.IsLoadingComplete}, "
                              + $"sceneTransition={LoadingController.IsSceneTransition})";
            if (LoadingController.IsLeavingMainScene) { why = "leaving main scene" + Flags(); return null; }
            if (!MonoSingleton<Heightmap>.IsInstantiated()) { why = "no Heightmap" + Flags(); return null; }
            if (!MonoSingleton<World>.IsInstantiated()) { why = "no World" + Flags(); return null; }

            var map = MonoSingleton<Heightmap>.Instance;
            var world = MonoSingleton<World>.Instance;
            if (map == null || world == null) { why = "Heightmap/World null" + Flags(); return null; }
            if (world.SizeX <= 0 || world.SizeZ <= 0) { why = $"world size {world.SizeX}x{world.SizeZ}" + Flags(); return null; }

            var h = map.GetHeightAt(world.SizeX / 2, world.SizeZ / 2);
            if (h < 0) { why = $"height at centre {h}" + Flags(); return null; }
            return map;
        }

        private string lastWhy;

        /// <summary>
        /// Why the map is not on screen, said once each time the reason
        /// changes. The first two builds never showed and never said why;
        /// this is so the third one cannot.
        /// </summary>
        private void Explain(string why)
        {
            if (why == lastWhy) return;
            lastWhy = why;
            GMPlugin.Log?.LogInfo(why == null ? "[minimap] visible" : $"[minimap] hidden: {why}");
        }

        /// <summary>
        /// A new map: a new heightmap to listen to and textures of its size.
        /// The old subscription goes with the old instance.
        /// </summary>
        private void Attach(Heightmap map)
        {
            if (heightmap != null) heightmap.HeightChangedAtEvent -= OnHeightChanged;

            heightmap = map;
            heightmap.HeightChangedAtEvent += OnHeightChanged;

            var world = MonoSingleton<World>.Instance;
            sizeX = world.SizeX;
            sizeZ = world.SizeZ;

            if (terrain != null) Destroy(terrain);
            if (view != null) Destroy(view);

            terrain = new Texture2D(sizeX, sizeZ, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            view = new Texture2D(sizeX, sizeZ, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            viewPixels = new Color32[sizeX * sizeZ];
            drawn.Clear();
            viewPainted = false;
            soilCache.Clear();
            window = new Rect(0f, 0f, sizeX, sizeZ);

            terrainImage.texture = terrain;
            viewImage.texture = view;

            terrainDirty = true;
            wholeMap = true;
            nextTerrain = 0f;

            // "A veces carga muy bien y a veces no": se pintaba una vez, al
            // enganchar, y otra solo si alguien cavaba. Si en ese momento el
            // agua o parte de los nodos aun no estaban, se quedaba asi toda la
            // partida. Se repinta unas cuantas veces mas mientras termina de
            // cargar, que es barato y no depende de adivinar cuando acaba.
            var now = Time.unscaledTime;
            settleRepaints.Clear();
            foreach (var after in SettleAfter) settleRepaints.Enqueue(now + after);

            if (!announced)
            {
                announced = true;
                GMPlugin.Log?.LogInfo($"[minimap] showing a {sizeX} x {sizeZ} map "
                    + $"({GMPlugin.MinimapToggle.Value} hides it)");
            }
        }

        private void OnHeightChanged(int x, int z, int newHeight)
        {
            terrainDirty = true;
            if (x < dirtyX0) dirtyX0 = x;
            if (x > dirtyX1) dirtyX1 = x;
            if (z < dirtyZ0) dirtyZ0 = z;
            if (z > dirtyZ1) dirtyZ1 = z;
        }

        /// <summary>
        /// Lo que ha cambiado desde el ultimo repintado. Repintar el mapa
        /// entero costaba ~70 ms (medido el 25 con `[fps] minimapa:`), y lo
        /// pedia cualquier obra o excavacion; ahora solo el rectangulo tocado.
        /// </summary>
        private int dirtyX0 = int.MaxValue, dirtyZ0 = int.MaxValue, dirtyX1 = -1, dirtyZ1 = -1;
        private bool wholeMap = true;
        private Color32[] terrainPixels;
        private int paintedMin, paintedMax;

        /// <summary>
        /// Lo que hay encima de cada columna, con su color, y el relieve
        /// encima como sombra desde el noroeste.
        ///
        /// <b>Por que cambio el 24.</b> Solo pintaba altura - verde abajo,
        /// marron en medio, gris arriba -, y lo que se pidio es lo de
        /// "ComoSeDeberiaVerElMapa.png": hierba verde, agua azul, arena, roca,
        /// y el nombre del suelo al pasar el raton. El tipo de suelo es el
        /// <c>VoxelType</c> del nodo de arriba de la columna - el mismo que
        /// el juego nombra abajo a la izquierda, "Arcilla 100%" -, y el agua
        /// la da el <c>WaterManager</c> del mapa, con su profundidad.
        ///
        /// <c>VoxelType.ColorForMapMask</c> no sirve de paleta: es la mascara
        /// que el juego le pasa a su shader de terreno, canales y no colores.
        /// Los colores son nuestros y se eligen por el id del suelo; los que
        /// no se reconocen salen con un tono propio y se dicen en el log la
        /// primera vez, para poder afinarlos.
        /// </summary>
        private void PaintTerrain()
        {
            terrainDirty = false;

            var whole = wholeMap || terrainPixels == null || terrainPixels.Length != sizeX * sizeZ;
            int x0 = 0, z0 = 0, x1 = sizeX - 1, z1 = sizeZ - 1;
            if (!whole)
            {
                if (dirtyX1 < 0) return;
                // Uno mas por cada lado: la sombra lee a los vecinos.
                x0 = Mathf.Max(0, dirtyX0 - 1); x1 = Mathf.Min(sizeX - 1, dirtyX1 + 1);
                z0 = Mathf.Max(0, dirtyZ0 - 1); z1 = Mathf.Min(sizeZ - 1, dirtyZ1 + 1);
            }

            dirtyX0 = dirtyZ0 = int.MaxValue;
            dirtyX1 = dirtyZ1 = -1;

            if (whole)
            {
                var lowest = int.MaxValue;
                var highest = int.MinValue;
                for (var z = 0; z < sizeZ; z++)
                {
                    for (var x = 0; x < sizeX; x++)
                    {
                        var h = heightmap.GetHeightAt(x, z);
                        if (h < 0) continue;
                        if (h < lowest) lowest = h;
                        if (h > highest) highest = h;
                    }
                }

                if (lowest > highest) return;
                paintedMin = lowest;
                paintedMax = highest;
                terrainPixels = new Color32[sizeX * sizeZ];
                wholeMap = false;
            }

            var min = paintedMin;
            var max = paintedMax;
            var pixels = terrainPixels;

            var village = NSMedieval.Village.VillageManager.ActiveVillage?.Map;
            var sizeY = MonoSingleton<World>.IsInstantiated() ? MonoSingleton<World>.Instance.SizeY : 0;

            var range = Mathf.Max(1, max - min);
            var low = new Color(0.27f, 0.40f, 0.20f);
            var mid = new Color(0.52f, 0.49f, 0.33f);
            var high = new Color(0.62f, 0.60f, 0.57f);

            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var h = heightmap.GetHeightAt(x, z);
                    if (h < 0)
                    {
                        pixels[z * sizeX + x] = new Color32(0, 0, 0, 255);
                        continue;
                    }

                    var t = Mathf.Clamp01((h - min) / (float)range);
                    var colour = t < 0.5f
                        ? Color.Lerp(low, mid, t * 2f)
                        : Color.Lerp(mid, high, (t - 0.5f) * 2f);

                    Color surface;
                    if (Surface(village, sizeY, x, h, z, out surface))
                    {
                        // Un poco de la altura se queda: dos prados a
                        // distinta cota no deben leerse como uno.
                        colour = Color.Lerp(surface, colour, 0.12f);
                    }

                    var nw = Height(x - 1, z + 1, h);
                    var se = Height(x + 1, z - 1, h);
                    var shade = Mathf.Clamp(1f + 0.18f * (nw - se), 0.55f, 1.35f);

                    colour *= shade;
                    colour.a = 1f;
                    pixels[z * sizeX + x] = colour;
                }
            }

            terrain.SetPixels32(pixels);
            terrain.Apply(false);

            if (!soilsSaid && soilsSeen.Count > 0)
            {
                soilsSaid = true;
                var parts = new List<string>();
                foreach (var pair in soilsSeen) parts.Add(pair.Key + "=" + pair.Value);
                GMPlugin.Log?.LogInfo($"[minimap] suelos: {string.Join(", ", parts.ToArray())}");
            }
        }

        private static readonly Color DeepWater = new Color(0.13f, 0.30f, 0.62f);
        private static readonly Color MidWater = new Color(0.20f, 0.42f, 0.76f);
        private static readonly Color ShallowWater = new Color(0.36f, 0.62f, 0.86f);
        private static readonly Color Roofed = new Color(0.55f, 0.46f, 0.38f);

        private readonly Dictionary<string, string> soilsSeen = new Dictionary<string, string>();
        private bool soilsSaid;

        /// <summary>El color de lo que hay arriba de la columna, si se sabe.</summary>
        private bool Surface(NSMedieval.Village.Map.VillageMap village, int sizeY, int x, int h, int z,
            out Color colour)
        {
            colour = default(Color);
            if (village == null || h <= 0) return false;

            try
            {
                var depth = WaterDepth(village, sizeY, x, h, z);
                if (depth != NSMedieval.Water.WaterDepthLevel.None)
                {
                    colour = depth == NSMedieval.Water.WaterDepthLevel.Low ? ShallowWater
                        : depth == NSMedieval.Water.WaterDepthLevel.Medium ? MidWater
                        : DeepWater;
                    return true;
                }

                if (h < sizeY)
                {
                    var above = village.GetNode(x, h, z);
                    if (above != null && (above.DataType & GridDataType.Roof) != 0)
                    {
                        colour = Roofed;
                        return true;
                    }
                }

                var voxel = village.GetNode(x, h - 1, z)?.VoxelType;
                if (voxel == null) return false;

                colour = SoilColour(voxel);
                return true;
            }
            catch (Exception)
            {
                // Un nodo fuera de rango o un mapa a medio cargar: se queda la
                // altura, y el repintado siguiente lo vuelve a intentar.
                return false;
            }
        }

        private static NSMedieval.Water.WaterDepthLevel WaterDepth(NSMedieval.Village.Map.VillageMap village,
            int sizeY, int x, int h, int z)
        {
            var water = village.WaterManager;
            if (water == null) return NSMedieval.Water.WaterDepthLevel.None;

            var below = water.GetWaterDepthLevel(x, h - 1, z);
            var above = h < sizeY ? water.GetWaterDepthLevel(x, h, z) : NSMedieval.Water.WaterDepthLevel.None;
            return above > below ? above : below;
        }

        /// <summary>
        /// Un color por tipo de suelo, calculado una vez. Sin esto cada repintado
        /// armaba un string, lo pasaba a minusculas y le hacia diez Contains por
        /// cada una de las 262.144 columnas - y repinta cada vez que alguien cava
        /// o construye: el tiron de FPS del minimapa.
        /// </summary>
        private readonly Dictionary<NSMedieval.Model.MapNew.VoxelType, Color> soilCache =
            new Dictionary<NSMedieval.Model.MapNew.VoxelType, Color>();

        private Color SoilColour(NSMedieval.Model.MapNew.VoxelType voxel)
        {
            if (!soilCache.TryGetValue(voxel, out var cached))
            {
                cached = SoilColourUncached(voxel);
                soilCache[voxel] = cached;
            }

            return cached;
        }

        private Color SoilColourUncached(NSMedieval.Model.MapNew.VoxelType voxel)
        {
            var id = voxel.GetID() ?? "?";
            var key = (id + " " + voxel.TextKey).ToLowerInvariant();

            Color colour;
            string name;
            if (Has(key, "grass", "hierba")) { colour = new Color(0.42f, 0.60f, 0.27f); name = "hierba"; }
            else if (Has(key, "sand", "arena")) { colour = new Color(0.82f, 0.74f, 0.52f); name = "arena"; }
            else if (Has(key, "clay", "arcilla")) { colour = new Color(0.68f, 0.52f, 0.38f); name = "arcilla"; }
            else if (Has(key, "gravel", "grava")) { colour = new Color(0.63f, 0.61f, 0.57f); name = "grava"; }
            else if (Has(key, "snow", "nieve")) { colour = new Color(0.92f, 0.93f, 0.95f); name = "nieve"; }
            else if (Has(key, "ice", "hielo")) { colour = new Color(0.76f, 0.86f, 0.93f); name = "hielo"; }
            else if (Has(key, "till", "farm", "field", "crop")) { colour = new Color(0.80f, 0.55f, 0.22f); name = "cultivo"; }
            else if (Has(key, "stone", "rock", "lime", "marble", "granite", "slate", "basalt", "piedra", "roca"))
            {
                colour = new Color(0.56f, 0.56f, 0.54f); name = "roca";
            }
            else if (Has(key, "soil", "dirt", "earth", "mud", "tierra", "barro") || voxel.CanGrowGrass)
            {
                // La tierra donde crece la hierba se ve verde en la partida.
                colour = new Color(0.46f, 0.56f, 0.30f); name = "tierra";
            }
            else
            {
                // Uno que no se conoce: un tono apagado fijo por id.
                var hash = id.GetHashCode();
                colour = Color.HSVToRGB((hash & 0xFF) / 255f, 0.25f, 0.55f);
                name = "sin color propio";
            }

            if (!soilsSaid && !soilsSeen.ContainsKey(id)) soilsSeen[id] = name;
            return colour;
        }

        private static bool Has(string text, params string[] words)
        {
            foreach (var word in words)
            {
                if (text.Contains(word)) return true;
            }

            return false;
        }

        private int Height(int x, int z, int fallback)
        {
            var h = heightmap.GetHeightAt(x, z);
            return h < 0 ? fallback : h;
        }

        /// <summary>
        /// The outline of what the camera sees: the four corners of the screen
        /// cast onto the ground plane under the camera's focus.
        /// </summary>
        private static readonly Vector3[] Viewport =
        {
            new Vector3(0f, 0f), new Vector3(1f, 0f), new Vector3(1f, 1f), new Vector3(0f, 1f),
        };

        private readonly Vector2[] corners = new Vector2[4];
        private readonly Vector2[] lastCorners = new Vector2[4];
        private readonly List<int> drawn = new List<int>();
        private bool viewPainted;

        /// <summary>
        /// Solo cuando la camara se ha movido, y borrando solo lo que se pinto la
        /// vez anterior: antes eran 262.144 pixeles limpiados y 1 MB subido a la
        /// grafica cuatro veces por segundo, con la camara quieta o no.
        /// </summary>
        private void PaintView()
        {
            var camera = Camera.main;
            var has = camera != null && MonoSingleton<RtsCamera>.IsInstantiated();
            if (has)
            {
                var ground = new Plane(Vector3.up,
                    new Vector3(0f, MonoSingleton<RtsCamera>.Instance.CurrentPosition.y, 0f));

                for (var i = 0; i < 4; i++)
                {
                    var ray = camera.ViewportPointToRay(Viewport[i]);
                    var point = ground.Raycast(ray, out var distance) && distance < 400f
                        ? ray.GetPoint(distance)
                        : ray.GetPoint(400f);
                    corners[i] = new Vector2(point.x, point.z);
                }
            }

            var moved = !viewPainted;
            for (var i = 0; i < 4 && !moved; i++) moved = (corners[i] - lastCorners[i]).sqrMagnitude > 0.25f;
            if (!moved) return;

            foreach (var index in drawn) viewPixels[index] = Clear;
            drawn.Clear();

            if (has)
            {
                for (var i = 0; i < 4; i++) Line(corners[i], corners[(i + 1) % 4]);
                for (var i = 0; i < 4; i++) lastCorners[i] = corners[i];
            }

            viewPainted = true;
            view.SetPixels32(viewPixels);
            view.Apply(false);
        }

        private void Line(Vector2 from, Vector2 to)
        {
            var steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y)));
            steps = Mathf.Clamp(steps, 1, 4 * (sizeX + sizeZ));

            for (var s = 0; s <= steps; s++)
            {
                var p = Vector2.Lerp(from, to, s / (float)steps);
                var x = Mathf.RoundToInt(p.x);
                var z = Mathf.RoundToInt(p.y);
                if (x < 0 || z < 0 || x >= sizeX || z >= sizeZ) continue;
                viewPixels[z * sizeX + x] = ViewLine;
                drawn.Add(z * sizeX + x);
            }
        }

        private void PlaceMarkers()
        {
            var used = 0;
            var width = mapRect.rect.width;
            var height = mapRect.rect.height;

            foreach (var body in RisenHunt.Everything())
            {
                if (body == null || body.HasDisposed || body.HasDied) continue;

                var colour = ColourOf(body, out var size);
                var at = body.GetPosition();
                var u = (at.x - window.x) / window.width;
                var v = (at.z - window.y) / window.height;
                if (u < 0f || v < 0f || u > 1f || v > 1f) continue;

                var marker = Marker(used++);
                marker.color = colour;
                marker.rectTransform.sizeDelta = new Vector2(size, size);
                marker.rectTransform.anchoredPosition = new Vector2(u * width, v * height);
            }

            for (var i = used; i < markers.Count; i++)
            {
                if (markers[i].gameObject.activeSelf) markers[i].gameObject.SetActive(false);
            }
        }

        private static Color ColourOf(CreatureBase body, out float size)
        {
            size = 5f;

            if (body is HumanoidInstance human)
            {
                if (RisenPallor.IsUndead(human)) { size = 6f; return Undead; }
                if (human.IsEnemy()) { size = 6f; return Enemy; }
                if (human.IsPrisoner()) return Prisoner;
                if (human.IsWorker()) return Settler;
                return Visitor;
            }

            if (body is AnimalInstance animal)
            {
                switch (animal.AnimalType)
                {
                    case AnimalType.WildAggressive:
                        return Predator;
                    case AnimalType.Wild:
                        size = 3f;
                        return Wildlife;
                    default:
                        size = 4f;
                        return Livestock;
                }
            }

            size = 3f;
            return Wildlife;
        }

        private Image Marker(int index)
        {
            while (markers.Count <= index)
            {
                var rect = NewChild("Marker", mapRect);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                var image = rect.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                markers.Add(image);
            }

            var marker = markers[index];
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            return marker;
        }

        /// <summary>
        /// El nombre de lo que hay bajo el raton, como en
        /// "ComoSeDeberiaVerElMapa.png" ("Hierba"): el mismo texto que el
        /// juego pone abajo a la izquierda, sacado de la misma clave.
        /// </summary>
        private void Hover()
        {
            if (!Hovering || heightmap == null)
            {
                HideTip();
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mapRect, Input.mousePosition, null, out var local))
            {
                HideTip();
                return;
            }

            var r = mapRect.rect;
            var u = Mathf.Clamp01((local.x - r.xMin) / r.width);
            var v = Mathf.Clamp01((local.y - r.yMin) / r.height);
            var x = Mathf.Clamp(Mathf.FloorToInt(window.x + u * window.width), 0, sizeX - 1);
            var z = Mathf.Clamp(Mathf.FloorToInt(window.y + v * window.height), 0, sizeZ - 1);

            if (!Tip()) return;

            var column = z * sizeX + x;
            if (column != tipColumn)
            {
                tipColumn = column;
                var name = Describe(x, z);
                if (string.IsNullOrEmpty(name))
                {
                    HideTip();
                    return;
                }

                tipText.text = name;
                var wanted = tipText.GetPreferredValues(name);
                tipRect.sizeDelta = new Vector2(wanted.x + 14f, wanted.y + 6f);
            }

            if (!tipRect.gameObject.activeSelf) tipRect.gameObject.SetActive(true);

            var canvas = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas, Input.mousePosition, null, out var at))
            {
                tipRect.anchoredPosition = at + new Vector2(14f, -14f);
            }
        }

        private void HideTip()
        {
            tipColumn = -1;
            if (tipRect != null && tipRect.gameObject.activeSelf) tipRect.gameObject.SetActive(false);
        }

        /// <summary>
        /// La etiqueta, hecha la primera vez que hace falta y con la fuente
        /// de un texto del propio juego: con TextMeshPro no se adivina la
        /// fuente, se copia de uno que ya este en pantalla.
        /// </summary>
        private bool Tip()
        {
            if (tipText != null) return true;
            if (tipUnavailable) return false;

            var source = FindObjectOfType<TMPro.TextMeshProUGUI>();
            if (source == null || source.font == null)
            {
                // Aun no hay ninguno cargado; se vuelve a mirar la proxima vez.
                return false;
            }

            try
            {
                tipRect = NewChild("Tooltip", transform);
                tipRect.anchorMin = tipRect.anchorMax = new Vector2(0.5f, 0.5f);
                tipRect.pivot = new Vector2(0f, 1f);
                var back = tipRect.gameObject.AddComponent<Image>();
                back.color = new Color(0.11f, 0.11f, 0.10f, 0.92f);
                back.raycastTarget = false;

                var textRect = NewChild("Text", tipRect);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(7f, 3f);
                textRect.offsetMax = new Vector2(-7f, -3f);

                tipText = textRect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
                tipText.font = source.font;
                tipText.fontSize = 17f;
                tipText.color = new Color(0.95f, 0.93f, 0.88f);
                tipText.raycastTarget = false;
                tipText.enableWordWrapping = false;
                tipText.alignment = TMPro.TextAlignmentOptions.MidlineLeft;

                tipRect.gameObject.SetActive(false);
                return true;
            }
            catch (Exception e)
            {
                tipUnavailable = true;
                GMPlugin.Log?.LogWarning($"[minimap] sin tooltip: {e.Message}");
                return false;
            }
        }

        /// <summary>Agua (con su profundidad) o el nombre del suelo, en el idioma del juego.</summary>
        private string Describe(int x, int z)
        {
            var village = NSMedieval.Village.VillageManager.ActiveVillage?.Map;
            if (village == null || !MonoSingleton<LocalizationController>.IsInstantiated()) return null;

            var h = heightmap.GetHeightAt(x, z);
            if (h <= 0) return null;

            try
            {
                var words = MonoSingleton<LocalizationController>.Instance;
                var sizeY = MonoSingleton<World>.Instance.SizeY;

                var depth = WaterDepth(village, sizeY, x, h, z);
                if (depth != NSMedieval.Water.WaterDepthLevel.None)
                {
                    return words.GetText(village.WaterManager.GetTextKeyForWaterLevel(depth));
                }

                var voxel = village.GetNode(x, h - 1, z)?.VoxelType;
                return voxel == null ? null : words.GetText(voxel.TextKey);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// A click or drag on the map: the camera goes to that column, at the
        /// height of its surface.
        /// </summary>
        internal void JumpTo(Vector2 screenPoint, Camera eventCamera)
        {
            if (heightmap == null || !MonoSingleton<RtsCamera>.IsInstantiated()) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mapRect, screenPoint, eventCamera, out var local)) return;

            var r = mapRect.rect;
            var u = Mathf.Clamp01((local.x - r.xMin) / r.width);
            var v = Mathf.Clamp01((local.y - r.yMin) / r.height);

            var x = Mathf.Clamp(Mathf.RoundToInt(window.x + u * window.width), 0, sizeX - 1);
            var z = Mathf.Clamp(Mathf.RoundToInt(window.y + v * window.height), 0, sizeZ - 1);
            var y = Mathf.Max(0, heightmap.GetHeightAt(x, z)) * World.MapBlockHeight;

            var camera = MonoSingleton<RtsCamera>.Instance;
            camera.CancelFollow();
            camera.JumpTo(new Vector3(x, y, z));
            nextMarkers = 0f;
        }

        private void OnDestroy()
        {
            if (heightmap != null) heightmap.HeightChangedAtEvent -= OnHeightChanged;
            if (live == this)
            {
                live = null;
                GMPlugin.Log?.LogWarning("[minimap] host destroyed");
            }
        }
    }

    /// <summary>
    /// Left button only: the right one stays free for whatever the game does
    /// with it, and a drag keeps the camera following the pointer.
    /// </summary>
    internal sealed class MinimapClick : MonoBehaviour, IPointerDownHandler, IDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        internal Minimap Map;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Map != null) Map.Hovering = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Map != null) Map.Hovering = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Map?.JumpTo(eventData.position, eventData.pressEventCamera);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Map?.JumpTo(eventData.position, eventData.pressEventCamera);
            }
        }
    }
}
