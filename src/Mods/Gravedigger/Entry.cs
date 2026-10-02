using BepInEx;
using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>La DLL de Gravedigger - Mass Graves and Pyres: el nucleo comun mas lo suyo, y nada de los otros mods.</summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public partial class GMPlugin
    {
        public const string PluginGuid = "aldrich.gravedigger";
        public const string PluginName = "Aldrich - Gravedigger";
        /// <summary>La carpeta del mod en Documentos\...\Mods; ver ModDataSync.</summary>
        internal const string ModFolder = "Gravedigger";
        public const string PluginVersion = "1.0.0";

        partial void Load(Harmony harmony)
        {
            BindGravedigger();
            PatchGravedigger(harmony);
            StartGravedigger();
        }
    }
}
