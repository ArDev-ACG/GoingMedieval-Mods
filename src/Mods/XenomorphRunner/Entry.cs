using BepInEx;
using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>La DLL de The Runner - Alien Spiders: el nucleo comun mas lo suyo, y nada de los otros mods.</summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public partial class GMPlugin
    {
        public const string PluginGuid = "aldrich.xenomorph_runner";
        public const string PluginName = "Aldrich - The Runner";
        /// <summary>La carpeta del mod en Documentos\...\Mods; ver ModDataSync.</summary>
        internal const string ModFolder = "XenomorphRunner";
        public const string PluginVersion = "1.0.0";

        partial void Load(Harmony harmony)
        {
            BindRunner();
            PatchRunner(harmony);
            StartRunner();
        }
    }
}
