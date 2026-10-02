using NSMedieval.Manager;
using NSMedieval.State;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Tener uno cerca se nota en la moral, y tenerlo encima se nota mas.
    ///
    /// Dos anillos, como el empalado (<see cref="StakeDread"/>): dentro de
    /// <c>[Xeno] DreadRadius</c> el colono lleva <c>XenoDread</c> - la cosa esa
    /// que anda por ahi -, y dentro de <c>PanicRadius</c> lo cambia por
    /// <c>XenoPanic</c>, que ademas le tiembla el pulso. Salir del anillo lo
    /// quita: el miedo dura lo que dura tenerlo delante.
    ///
    /// <b>Solo colonos.</b> Un enemigo tiene el stat Mood y nadie se lo suma
    /// nunca - lo dice la nota de <c>MoodModify</c> en PENDIENTES -, asi que
    /// ponerle un pensamiento a un saqueador seria un numero sin lector.
    /// </summary>
    internal static class XenoDread
    {
        private const string DreadId = "XenoDread";
        private const string PanicId = "XenoPanic";
        private const float SweepSeconds = 4f;

        internal static void Start()
        {
            GMPlugin.Every("xeno dread", SweepSeconds, Sweep);
        }

        private static void Sweep()
        {
            var dread = GMPlugin.XenoDreadRadius?.Value ?? 14;
            if (dread <= 0) return;

            if (!NSEipix.Base.MonoSingleton<WorkerManager>.IsInstantiated()) return;

            var panic = Mathf.Min(GMPlugin.XenoPanicRadius?.Value ?? 6, dread);

            foreach (var settler in WorkerManager.WorkersHere)
            {
                if (settler == null || settler.HasDisposed || settler.HasDiedOrFainted) continue;

                CreatureBase xeno;
                var gap = Nearest(settler, out xeno);
                var wants = gap <= panic ? PanicId : (gap <= dread ? DreadId : null);

                Sync(settler, wants);
                if (wants == PanicId) Flee(settler, xeno);
            }
        }

        /// <summary>Al Corredor vivo mas cercano, en casillas. Infinito si no hay.</summary>
        private static float Nearest(HumanoidInstance settler, out CreatureBase closest)
        {
            var here = settler.GetPosition();
            var best = float.MaxValue;
            closest = null;

            foreach (var body in Census.Everything())
            {
                if (body == null || body.HasDisposed || body.HasDiedOrFainted) continue;
                if (!XenoAcid.IsXeno(body)) continue;

                var gap = Vector3.Distance(here, body.GetPosition());
                if (gap >= best) continue;

                best = gap;
                closest = body;
            }

            return best;
        }

        /// <summary>Cuanto dura una huida, y cuanto hasta poder huir otra vez.</summary>
        private const float FleeSeconds = 8f;
        private const float FleeCooldown = 20f;

        private static readonly System.Collections.Generic.Dictionary<int, float> Fled =
            new System.Collections.Generic.Dictionary<int, float>();

        /// <summary>
        /// El miedo que se ve: el colono suelta lo que hace y corre.
        ///
        /// <b>Por que hacia falta.</b> El 24 el log dice que el efector entro
        /// ("Harold Woods ve lo que hay ahi fuera (XenoPanic)") y lo que se vio
        /// en partida fue "sin efecto, no da miedo": un -14 de moral y un 20%
        /// menos de ritmo de trabajo no se notan con el bicho delante. Esto es
        /// la huida del propio juego - <c>FleeGoal</c>, que los colonos ya
        /// tienen en su horario, con su "huyendo" encima de la cabeza -, y se
        /// abre poniendo <c>IsFleeing</c>, que es lo que mira su CanStart.
        ///
        /// A un reclutado no se le toca: ese lo manda el jugador. Y la bandera
        /// se baja a los ocho segundos, porque en un colono nadie mas la baja
        /// y se quedaria huyendo para siempre.
        /// </summary>
        private static void Flee(HumanoidInstance settler, CreatureBase xeno)
        {
            if (!(GMPlugin.XenoPanicFlee?.Value ?? true)) return;
            if (settler.IDraftableBehaviour != null && settler.IDraftableBehaviour.IsDrafting) return;

            var ai = settler.CombatAi;
            if (ai == null || ai.HasDisposed) return;

            var now = Heartbeat.Now;
            float last;
            if (Fled.TryGetValue(settler.UniqueId, out last) && now - last < FleeCooldown) return;
            Fled[settler.UniqueId] = now;

            try
            {
                // De quien huye: sin esto el juego huye de lo que perciba como
                // hostil, y un xeno salvaje no siempre lo esta.
                if (xeno is NSMedieval.Goap.IDamageDealAgent from)
                {
                    RisenGoalState.Set(ai, NSMedieval.CombatAi.CombatAiState.LastMissFrom, from);
                }

                RisenGoalState.Set(ai, NSMedieval.CombatAi.CombatAiState.IsFleeing, true);
                settler.GetGoapAgent()?.ForceNextGoal("FleeGoal");

                GMPlugin.RunAfter(FleeSeconds, () =>
                {
                    if (settler.HasDisposed || ai.HasDisposed) return;
                    RisenGoalState.Set(ai, NSMedieval.CombatAi.CombatAiState.IsFleeing, false);
                });

                GMPlugin.Log?.LogInfo($"[xeno] {GameAccess.Name(settler)} sale corriendo");
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[xeno] flee failed on {GameAccess.Name(settler)}: {e.Message}");
            }
        }

        private static void Sync(HumanoidInstance settler, string wants)
        {
            try
            {
                var stats = GameAccess.Stats(settler);
                if (stats == null) return;

                var hasDread = stats.IsEffectorActive(DreadId);
                var hasPanic = stats.IsEffectorActive(PanicId);

                if (wants == DreadId && hasPanic) GameAccess.ForceEndEffector(settler, PanicId);
                if (wants == PanicId && hasDread) GameAccess.ForceEndEffector(settler, DreadId);

                if (wants == null)
                {
                    if (hasDread) GameAccess.ForceEndEffector(settler, DreadId);
                    if (hasPanic) GameAccess.ForceEndEffector(settler, PanicId);
                    return;
                }

                if (stats.IsEffectorActive(wants)) return;

                if (stats.StartEffector(wants, 1f, false, -1, null))
                {
                    GMPlugin.Log?.LogInfo(
                        $"[xeno] {GameAccess.Name(settler)} ve lo que hay ahi fuera ({wants})");
                }
                else
                {
                    GMPlugin.Log?.LogWarning(
                        $"[xeno] '{wants}' refused on {GameAccess.Name(settler)} "
                        + "- missing from Effectors.json, or banned on that settler");
                }
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[xeno] dread failed on {GameAccess.Name(settler)}: {e}");
            }
        }
    }

}
