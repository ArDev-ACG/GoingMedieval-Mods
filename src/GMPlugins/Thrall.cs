using NSMedieval.State;
using NSMedieval.StatsSystem;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// El thrall: un colono de la propia colonia marcado por los dientes de un
    /// vampiro. Lo decidio quien manda el 26: trabaja sin quejarse, pierde
    /// sangre poco a poco, y a cambio lleva +40 de animo.
    ///
    /// <b>Como se hace uno.</b> Con el mordisco ordenado (el menu "Morder")
    /// sobre un colono que no es prisionero: el prisionero sigue siendo la via
    /// del Ghoul, y el colono libre la del thrall. Basta un mordisco.
    ///
    /// <b>Las tres partes y donde vive cada una.</b> "Sin quejarse" es el perk
    /// <c>Thrall</c> de VampireCourt/Perk.json, que prohibe las burbujas de
    /// queja y la crisis. El +40 es el efector <c>ThrallDevotion</c>, puesto
    /// desde aqui igual que el aura del Conde y no desde el campo
    /// <c>effector</c> del perk, que en vanilla solo se usa para prioridades.
    /// Y la sangre se la quita este barrido hasta un suelo que no mata.
    /// </summary>
    internal static class Thrall
    {
        internal const string PerkId = "Thrall";
        private const string DevotionEffector = "ThrallDevotion";
        private const float SweepSeconds = 20f;

        internal static void Start()
        {
            GMPlugin.Every("thrall", SweepSeconds, Sweep);
        }

        /// <summary>Lo que llama el mordisco ordenado.</summary>
        internal static void Mark(HumanoidInstance human)
        {
            if (human == null || GameAccess.IsCaptive(human) || !human.IsWorker()) return;
            if (GameAccess.HasPerk(human, PerkId)) return;

            human.TryAddNewPerk(PerkId);
            GMPlugin.Log?.LogInfo($"[thrall] {GameAccess.Name(human)} lleva ya la marca del Conde");
        }

        private static void Sweep()
        {
            var floor = GMPlugin.ThrallBloodFloor?.Value ?? 60f;
            var bleed = GMPlugin.ThrallBleedPercent?.Value ?? 0.016f;

            foreach (var body in Census.Everything())
            {
                var human = body as HumanoidInstance;
                if (human == null || human.HasDisposed || human.HasDied) continue;
                if (!GameAccess.HasPerk(human, PerkId)) continue;

                var stats = GameAccess.Stats(human);
                if (stats == null) continue;

                if (!stats.IsEffectorActive(DevotionEffector))
                {
                    stats.StartEffector(DevotionEffector, 1f, false, -1, null);
                }

                // ponytail: un tanto fijo por barrido contra la recuperacion
                // natural de la sangre; si en partida no baja o baja de golpe,
                // se toca ThrallBleedPercent en el cfg.
                var blood = stats.GetStat(StatType.Blood);
                if (blood != null && blood.Current > floor)
                {
                    GameAccess.DrainStat(human, StatType.Blood, bleed);
                }
            }
        }
    }
}
