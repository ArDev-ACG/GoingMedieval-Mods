using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using NSMedieval.Manager;
using NSMedieval.State;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// El minimapa solo, para publicarlo aparte (Nexus). Compila los mismos
    /// Minimap.cs y Heartbeat.cs que el plugin grande - ver el .csproj - y aqui
    /// solo esta lo poco que esos dos le piden al resto: la config, el log, el
    /// reloj y la lista de criaturas.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class GMPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "aldrich.minimap";
        public const string PluginName = "Aldrich Minimap";
        public const string PluginVersion = "1.0.1";

        internal static ManualLogSource Log;

        internal static BepInEx.Configuration.ConfigEntry<bool> MinimapEnabled;
        internal static BepInEx.Configuration.ConfigEntry<BepInEx.Configuration.KeyboardShortcut> MinimapToggle;
        internal static BepInEx.Configuration.ConfigEntry<int> MinimapSize;
        internal static BepInEx.Configuration.ConfigEntry<int> MinimapOffsetX;
        internal static BepInEx.Configuration.ConfigEntry<int> MinimapOffsetY;
        internal static BepInEx.Configuration.ConfigEntry<int> MinimapZoom;
        internal static BepInEx.Configuration.ConfigEntry<string> MinimapCorner;
        internal static BepInEx.Configuration.ConfigEntry<int> MinimapTopOffsetX;
        internal static BepInEx.Configuration.ConfigEntry<int> MinimapTopOffsetY;
        internal static bool MinimapVisible = true;

        private void Awake()
        {
            Log = Logger;

            MinimapEnabled = Config.Bind("Minimap", "Enabled", true,
                "A minimap of the surface. Click or drag on it to move the camera there.");
            MinimapToggle = Config.Bind("Minimap", "ToggleKey",
                new BepInEx.Configuration.KeyboardShortcut(UnityEngine.KeyCode.M, UnityEngine.KeyCode.LeftControl),
                "Shows and hides the minimap. Written as 'M + LeftControl'.");
            MinimapSize = Config.Bind("Minimap", "Size", 240,
                "Side of the minimap, in pixels of a 1080p screen. It scales with the resolution.");
            MinimapCorner = Config.Bind("Minimap", "Corner", "TopRight",
                "TopRight (under the top bar, left of the notifications) or BottomRight.");
            MinimapTopOffsetX = Config.Bind("Minimap", "TopOffsetX", 565,
                "With Corner = TopRight: distance from the right edge of the screen, in pixels of a 1080p screen.");
            MinimapTopOffsetY = Config.Bind("Minimap", "TopOffsetY", 6,
                "With Corner = TopRight: distance from the top edge.");
            MinimapOffsetX = Config.Bind("Minimap", "OffsetX", 16,
                "With Corner = BottomRight: distance from the right edge.");
            MinimapOffsetY = Config.Bind("Minimap", "OffsetY", 32,
                "With Corner = BottomRight: distance from the bottom edge.");
            MinimapZoom = Config.Bind("Minimap", "Zoom", 1,
                "How many times the map is magnified (1 to 6). The + and - buttons write it here.");

            Minimap.Start();
            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        internal static void Every(string name, float seconds, Action action)
        {
            Heartbeat.Live()?.Repeat(name, seconds, action);
        }
    }

    /// <summary>Todo lo vivo del mapa: colonos, NPC y animales.</summary>
    internal static class RisenHunt
    {
        internal static IEnumerable<CreatureBase> Everything()
        {
            if (NSEipix.Base.MonoSingleton<WorkerManager>.IsInstantiated())
            {
                foreach (var settler in WorkerManager.WorkersHere) yield return settler;
            }

            var npcs = NSMedieval.GlobalSaveController.CurrentVillageData?.NPCs;
            if (npcs != null)
            {
                for (var i = 0; i < npcs.Count; i++) yield return npcs[i];
            }

            if (NSEipix.Base.MonoSingleton<AnimalManager>.IsInstantiated())
            {
                var animals = NSEipix.Base.MonoSingleton<AnimalManager>.Instance?.Animals;
                if (animals != null)
                {
                    foreach (var pair in animals)
                    {
                        var view = pair.Value;
                        if (view != null && view.AnimalInstance != null) yield return view.AnimalInstance;
                    }
                }
            }
        }
    }

    /// <summary>Sin la horda no hay no-muertos: todos los enemigos salen en rojo.</summary>
    internal static class RisenPallor
    {
        internal static bool IsUndead(HumanoidInstance humanoid) => false;
    }
}
