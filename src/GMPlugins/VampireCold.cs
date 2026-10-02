using NSMedieval.Manager;
using NSMedieval.State;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Un vampiro esta a gusto donde un vivo se congela.
    ///
    /// Los cuatro efectores de frio ya no le entran - eso son los
    /// <c>bannedEffector</c> del perk -, pero "no sentir el frio" y "preferirlo"
    /// no son lo mismo, y lo que se pidio es lo segundo: bajo
    /// <c>[Vampire] ColdComfortBelow</c> grados en la casilla donde esta, el
    /// vampiro o el ghoul lleva <c>VampireColdComfort</c> y eso le sube la
    /// moral mientras dure. Se va solo al entrar en calor.
    /// </summary>
    internal static class VampireCold
    {
        private const string ComfortId = "VampireColdComfort";
        private const float SweepSeconds = 10f;

        private static bool announced;

        internal static void Start()
        {
            GMPlugin.Every("vampire cold comfort", SweepSeconds, Sweep);
        }

        private static void Sweep()
        {
            if (!(GMPlugin.VampireColdComfort?.Value ?? true)) return;
            if (!NSEipix.Base.MonoSingleton<WorkerManager>.IsInstantiated()) return;
            var below = GMPlugin.VampireColdComfortBelow?.Value ?? 2f;

            foreach (var settler in WorkerManager.WorkersHere)
            {
                if (settler == null || settler.HasDisposed || settler.HasDiedOrFainted) continue;
                if (!GameAccess.HasPerk(settler, VampireSunlight.VampirePerk)
                    && !GameAccess.HasPerk(settler, VampireSunlight.GhoulPerk)) continue;

                try
                {
                    // La temperatura es del mapa, no un singleton: cada mapa
                    // lleva la suya en `Map.TemperatureManager`.
                    var temperatures = settler.Map?.TemperatureManager;
                    if (temperatures == null) continue;

                    var here = settler.GetGridPosition();
                    var degrees = temperatures.GetTemperature(here);
                    var cold = degrees <= below;

                    var stats = GameAccess.Stats(settler);
                    if (stats == null) continue;

                    if (cold == stats.IsEffectorActive(ComfortId)) continue;

                    if (!cold)
                    {
                        GameAccess.ForceEndEffector(settler, ComfortId);
                        continue;
                    }

                    if (stats.StartEffector(ComfortId, 1f, false, -1, null) && !announced)
                    {
                        announced = true;
                        GMPlugin.Log?.LogInfo(
                            $"[sun] {GameAccess.Name(settler)} esta a gusto en el frio "
                            + $"({degrees:0.#} grados)");
                    }
                }
                catch (System.Exception e)
                {
                    GMPlugin.Log?.LogError($"[sun] cold comfort failed: {e}");
                }
            }
        }
    }
}
