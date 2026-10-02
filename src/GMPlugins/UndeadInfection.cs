using System.Reflection;
using HarmonyLib;
using NSMedieval.State;
using NSMedieval.StatsSystem;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// What the Risen leave behind sometimes gets up again.
    ///
    /// The bite is already there: `WoundsUndeadClaws` rolls `undead_infection`
    /// on a hit, and the wound runs its course like any other. What was missing
    /// is the ending. This hooks the same place `PlagueImmunity` does -
    /// `StatsInstance.EndEffector`, which fires once when a wound finally
    /// closes - and rolls for what the settler gets up as.
    ///
    /// Most survive it. One in three does not: the perk goes on and
    /// `BecomeAggressive()` swaps them onto EnemyBehaviour, so the settler the
    /// player has been feeding all winter turns on the settlement in the middle
    /// of it. That is the mirror of the vampire's bite, which takes an enemy
    /// and hands the player a villager.
    ///
    /// The injected parameter must be called activeEffectorInfo. Harmony binds
    /// by name against EndEffector(ref ActiveEffectorInfo activeEffectorInfo,
    /// int indexOfEffector), and getting that wrong is what took the whole
    /// plugin down on 1 September.
    /// </summary>
    [HarmonyPatch]
    internal static class UndeadInfection
    {
        internal const string InfectionEffectorId = "undead_infection";

        /// <summary>
        /// The badge, not the wound. See <see cref="UndeadInfectionMark"/>.
        /// </summary>
        internal const string InfectedMarkId = "undead_infected";
        private const string RisenPerk = "Risen";

        private const int TurnOneInEvery = 3;

        /// <summary>
        /// Whether this settler has the horde in their blood right now - the
        /// festering wound itself, or the badge that goes with it.
        ///
        /// Either will do, and asking for both is the point: the wound is what
        /// the claws hand out and what closes on its own, the badge is what the
        /// player can see, and the two go on and come off at slightly different
        /// moments. Anything that wants to know "is this one going to get back
        /// up" has to answer yes throughout, not only in the overlap.
        /// </summary>
        internal static bool IsInfected(HumanoidInstance humanoid)
        {
            var stats = GameAccess.Stats(humanoid);
            if (stats == null) return false;

            return stats.IsEffectorActive(InfectionEffectorId)
                   || stats.IsEffectorActive(InfectedMarkId);
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(StatsInstance),
                "EndEffector",
                new[] { typeof(ActiveEffectorInfo).MakeByRefType(), typeof(int) });
        }

        private static void Postfix(StatsInstance __instance, ref ActiveEffectorInfo activeEffectorInfo)
        {
            if (__instance == null || activeEffectorInfo.Name != InfectionEffectorId) return;

            var humanoid = GameAccess.StatsOwner(__instance) as HumanoidInstance;
            if (humanoid == null || humanoid.HasDied) return;
            if (GameAccess.HasPerk(humanoid, RisenPerk)) return;

            if (UnityEngine.Random.Range(0, TurnOneInEvery) != 0)
            {
                // The badge goes with the wound. Whoever is still standing when
                // the festering closes is not infected any more, and the panel
                // has to say so or "Infectado" becomes a mark nobody can lose.
                __instance.EndEffector(InfectedMarkId);

                GMPlugin.Log?.LogInfo($"[risen] {GameAccess.Name(humanoid)} shook off the infection");
                return;
            }

            // TryAddNewPerk devuelve void, asi que hay que volver a preguntar.
            // Esta linea llevaba semanas escribiendose sin que el perk entrara:
            // `hideInGame: true` lo borraba del repositorio y aqui nadie se
            // enteraba. Si vuelve a faltar, que lo diga el log y que el colono
            // no se vuelva hostil por un cambio que no ha ocurrido.
            humanoid.TryAddNewPerk(RisenPerk);

            if (!GameAccess.HasPerk(humanoid, RisenPerk))
            {
                GMPlugin.Log?.LogWarning(
                    $"[risen] '{RisenPerk}' no entro en {GameAccess.Name(humanoid)} "
                    + "- revisar que el perk exista en el repositorio");
                return;
            }

            // Not BecomeAggressive(): its first line is
            // `if (activeBehaviourType & 556) return;` and 556 includes Worker,
            // so for the only kind of creature this method is ever handed it
            // did nothing at all. What came out was a settler wearing the Risen
            // perk, grey, and still on the payroll.
            RisenTurn.Turn(humanoid, null, "got back up as one of them");
        }
    }

    /// <summary>
    /// The infection eats at whoever is carrying it - and stops short of
    /// killing them.
    ///
    /// <b>What was missing.</b> The badge went on, the panel said
    /// <em>Infectado</em>, and that was the whole of it: a word, a small mood
    /// penalty, and a settler who worked a full day and went to bed. Being one
    /// bad roll from getting up as a corpse has to cost something while it
    /// lasts, or the mark is decoration.
    ///
    /// <b>Why the floor, and why it is not negotiable.</b> What was asked for
    /// is "que le haga daño, no que lo mate" - and a drain with no floor is a
    /// death sentence on a timer, which is a different mechanic and a worse
    /// one: the player would lose the settler to arithmetic rather than to the
    /// dice, and the one-in-three roll at the end of the wound would never be
    /// reached. So the bite stops at
    /// <c>Undead/InfectionDamageFloor</c> of maximum health. Below that line
    /// the fever does nothing at all, and what finishes an infected settler is
    /// a walker, a fall or the wound closing badly - never this.
    ///
    /// The JSON side of the same mark carries the standing penalties (pain, a
    /// slower body, a duller head); this is the part that has to be a clock,
    /// because a wound in this game is a modifier and not a drain.
    /// </summary>
    internal static class UndeadInfectionFever
    {
        /// <summary>
        /// How often the fever is checked, in real seconds. The bite itself is
        /// on its own clock in the config; this only has to be fine enough that
        /// the clock is honoured.
        /// </summary>
        private const float SweepSeconds = 4f;

        /// <summary>When each settler last took a bite, by unique id.</summary>
        private static readonly System.Collections.Generic.Dictionary<int, float> Bitten =
            new System.Collections.Generic.Dictionary<int, float>();

        internal static void Start()
        {
            GMPlugin.Every("undead infection fever", SweepSeconds, Sweep);
        }

        private static void Sweep()
        {
            var every = GMPlugin.InfectionDamageSeconds?.Value ?? 0f;
            var bite = GMPlugin.InfectionDamagePercent?.Value ?? 0f;
            var floor = GMPlugin.InfectionDamageFloor?.Value ?? 0.25f;

            if (every <= 0f || bite <= 0f) return;
            if (!NSEipix.Base.MonoSingleton<NSMedieval.Manager.WorkerManager>.IsInstantiated()) return;

            var now = UnityEngine.Time.time;

            foreach (var settler in NSMedieval.Manager.WorkerManager.WorkersHere)
            {
                if (settler == null || settler.HasDisposed || settler.HasDied) continue;
                if (!UndeadInfection.IsInfected(settler)) continue;

                float last;
                if (Bitten.TryGetValue(settler.UniqueId, out last) && now - last < every) continue;

                Bitten[settler.UniqueId] = now;
                Bite(settler, bite, floor);
            }
        }

        private static void Bite(HumanoidInstance settler, float fraction, float floor)
        {
            var health = GameAccess.Stats(settler)?.GetStat(StatType.Health);
            if (health == null || health.Max <= 0f) return;

            var lowest = health.Max * UnityEngine.Mathf.Clamp01(floor);
            if (health.Current <= lowest) return;

            // Never past the floor, however big the bite is set to.
            var wanted = health.Max * fraction;
            var room = health.Current - lowest;
            var taken = UnityEngine.Mathf.Min(wanted, room);

            health.AddCurrent(-taken);

            GMPlugin.Log?.LogInfo(
                $"[risen] the infection took {taken:0.0} off {GameAccess.Name(settler)} "
                + $"({health.Current:0} of {health.Max:0} left, floor {lowest:0})");
        }
    }

    /// <summary>
    /// Says out loud who is carrying it.
    ///
    /// `undead_infection` is the wound - "Herida purulenta" - and it reads like
    /// any other injury, which is the problem: the settler who is one bad roll
    /// away from getting up as one of them looks exactly like the settler who
    /// tripped over a fence. There was no way to tell in the panel, and no way
    /// to decide who to keep away from the beds.
    ///
    /// So the wound now brings a badge with it: `undead_infected`, which the
    /// panel shows as <b>Infectado</b> and which does almost nothing except sit
    /// there being visible. It goes on when the wound starts, comes off in
    /// <see cref="UndeadInfection"/> when the wound closes without turning
    /// anybody, and goes with them if it does.
    ///
    /// Hooked on StartEffector rather than on the claws, because the wound has
    /// more than one way in - the claws roll it, the dev console fires it - and
    /// this is the one place all of them pass through.
    /// </summary>
    [HarmonyPatch]
    internal static class UndeadInfectionMark
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(StatsInstance),
                "StartEffector",
                new[] { typeof(string), typeof(float), typeof(bool), typeof(int), typeof(string) });
        }

        // Positional on purpose: only the first argument is wanted, and a
        // wrong parameter name is an IL compile error at startup rather than a
        // warning.
        private static void Postfix(StatsInstance __instance, string __0, bool __result)
        {
            if (!__result || __instance == null) return;
            if (__0 != UndeadInfection.InfectionEffectorId) return;

            var humanoid = GameAccess.StatsOwner(__instance) as HumanoidInstance;
            if (humanoid == null || humanoid.HasDied) return;
            if (GameAccess.HasPerk(humanoid, RisenPallor.RisenPerk)) return;
            if (__instance.IsEffectorActive(UndeadInfection.InfectedMarkId)) return;

            // Not here, a frame from now.
            //
            // This is a postfix on StartEffector, so starting a second effector
            // from inside it is a re-entrant call into a method that is still
            // half way through its own bookkeeping - and the game notices:
            //
            //   [ERR] [StatsInstance] Effector undead_infected threw exception
            //   on start. Stat's owner is
            //     ... UndeadInfectionMark:Postfix (at UndeadInfection.cs:241)
            //
            // one NullReferenceException, swallowed by vanilla's own try/catch,
            // with the infection effector left half started. Deferring costs a
            // frame and there is nothing in the game that reads the mark inside
            // that frame.
            var mark = __instance;

            GMPlugin.RunAfter(0f, () => Mark(mark, humanoid));
        }

        /// <summary>Puts the visible mark up, off the re-entrant path.</summary>
        private static void Mark(StatsInstance stats, HumanoidInstance humanoid)
        {
            try
            {
                if (stats == null || humanoid == null || humanoid.HasDisposed || humanoid.HasDied) return;
                if (stats.IsEffectorActive(UndeadInfection.InfectedMarkId)) return;

                if (stats.StartEffector(UndeadInfection.InfectedMarkId, 1f, false, -1, null))
                {
                    GMPlugin.Log?.LogInfo($"[risen] {GameAccess.Name(humanoid)} is carrying it");
                }
                else
                {
                    GMPlugin.Log?.LogWarning(
                        $"[risen] '{UndeadInfection.InfectedMarkId}' refused on "
                        + $"{GameAccess.Name(humanoid)} - missing from Wounds.json");
                }
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[risen] could not mark the infected: {e}");
            }
        }
    }
}
