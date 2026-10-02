using BepInEx;
using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>La DLL de Vampire Court: el nucleo comun mas lo suyo, y nada de los otros mods.</summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public partial class GMPlugin
    {
        public const string PluginGuid = "aldrich.vampire_court";
        public const string PluginName = "Aldrich - Vampire Court";
        /// <summary>La carpeta del mod en Documentos\...\Mods; ver ModDataSync.</summary>
        internal const string ModFolder = "VampireCourt";
        public const string PluginVersion = "1.0.0";

        partial void Load(Harmony harmony)
        {
            BindVampire();
            BindKin();
            PatchKin(harmony);
            PatchVampire(harmony);
            StartVampire();
        }
    }
}
