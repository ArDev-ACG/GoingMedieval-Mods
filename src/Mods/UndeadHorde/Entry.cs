using BepInEx;
using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>La DLL de Undead Horde: el nucleo comun mas lo suyo, y nada de los otros mods.</summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public partial class GMPlugin
    {
        public const string PluginGuid = "aldrich.undead_horde";
        public const string PluginName = "Aldrich - Undead Horde";
        /// <summary>La carpeta del mod en Documentos\...\Mods; ver ModDataSync.</summary>
        internal const string ModFolder = "UndeadHorde";
        public const string PluginVersion = "1.0.0";

        partial void Load(Harmony harmony)
        {
            BindUndead();
            BindKin();
            PatchKin(harmony);
            PatchUndead(harmony);
            StartUndead();
        }
    }
}
