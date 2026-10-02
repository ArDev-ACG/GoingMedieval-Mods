using BepInEx;
using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>La DLL de Carrion and Plague: el nucleo comun mas lo suyo, y nada de los otros mods.</summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public partial class GMPlugin
    {
        public const string PluginGuid = "aldrich.carrion_and_plague";
        public const string PluginName = "Aldrich - Carrion and Plague";
        /// <summary>La carpeta del mod en Documentos\...\Mods; ver ModDataSync.</summary>
        internal const string ModFolder = "CarrionAndPlague";
        public const string PluginVersion = "1.0.0";

        partial void Load(Harmony harmony)
        {
            BindCarrion();
            PatchCarrion(harmony);
            StartCarrion();
        }
    }
}
