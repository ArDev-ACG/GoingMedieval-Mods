using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>Lo que el codigo de Gravedigger necesita del plugin: su config, sus parches y sus barridos.</summary>
    public partial class GMPlugin
    {
        private void BindGravedigger()
        {

        }

        private static void PatchGravedigger(Harmony harmony)
        {
            PatchOne(harmony, typeof(MassGraveCapacity));
            PatchOne(harmony, typeof(MassGraveStaysOpen));
        }

        private static void StartGravedigger()
        {
        }
    }
}
