using HarmonyLib;
using NSEipix.Base;
using NSMedieval.Manager;
using NSMedieval.State;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Makes daylight mean something different for each kind of undead.
    ///
    /// CreatureBase.GetSunlightLossMultiplier() is just
    ///
    ///     Mathf.Lerp(30f, -10f, GetReceivingLightAmount())
    ///
    /// so the number is positive in the dark (the SunLight stat recovers) and
    /// negative in the open (it drains, and below 15 the creature gets
    /// heatstroke). Two things follow. First, only the negative half is worth
    /// touching - scaling the recovery would punish standing in the shade.
    /// Second, because the light amount is measured where the creature actually
    /// is, this half was always the "outdoors" rule.
    ///
    /// <b>And it was only ever half.</b> What the player sees is not the
    /// SunLight stat, it is the mood entry - and that came from
    /// <c>DateTimeSettings.dayEffectors</c>, which is the game clock and
    /// nothing else. Noon in a cellar and noon in a field were the same entry,
    /// because the clock does not know where anybody is standing. That is the
    /// whole of "no hay diferencia si esta a plena luz del dia o debajo de un
    /// edificio o de la tierra". <see cref="VampireDaylight"/> is what replaced
    /// the clock; this class keeps the stat side, and now agrees with it.
    ///
    /// The ranking the mod wants: a Ghoul burns worst, since it took the blood
    /// without the grave's protection; a Vampire burns, but less; and a Count
    /// buys that back with rank, walking out at noon untouched at the third.
    /// </summary>
    [HarmonyPatch(typeof(CreatureBase), nameof(CreatureBase.GetSunlightLossMultiplier))]
    internal static class VampireSunlight
    {
        internal const string VampirePerk = "Vampire";
        internal const string GhoulPerk = "Ghoul";
        private const string CountRole = "count";

        private const float GhoulBurn = 2.0f;    // the sun hurts a ghoul twice over
        private const float VampireBurn = 1.35f;

        /// <summary>
        /// Leaves vanilla's sunlight need exactly as it was.
        ///
        /// <b>This method used to be the bug.</b> The class was written on a
        /// reading of <c>SunLight</c> that is backwards.
        /// <c>StatsModelRepository.json</c> settles it: the stat starts at 50,
        /// its floor is <c>20 * SunlightMax</c>, and crossing <b>below 15</b>
        /// fires <c>heatstroke</c>. It is a <em>need</em> - the game's vitamin
        /// D - and the settler who never sees the sun is the one who suffers.
        ///
        /// Two consequences, both of them live in the last session:
        ///
        ///   - the Vampire perk carries <c>SunlightMax: 0.35</c>, so a
        ///     vampire's floor is 7, far under the 15 that fires heatstroke:
        ///     left alone, every vampire ends up permanently heatstruck;
        ///   - and this postfix answered <c>0</c> for anyone sheltered, which
        ///     does not mean "safe" - it <b>freezes the stat</b>. A vampire
        ///     that had already fallen under 15 stayed under 15 for good:
        ///     indoors, in the cellar, asleep in its coffin, still losing
        ///     health, with the shelter rule itself holding it down there.
        ///     That is the report word for word - "durmiendo bajo techo y en el
        ///     ataud aun asi les bajo la vida".
        ///
        /// So the multiplier is handed back untouched, and the vampire's
        /// quarrel with the sun is expressed the one way that matches the
        /// fiction: <see cref="VampireDaylight"/> keeps the need topped up so it
        /// can never bite, and burns them when the sky can actually see them.
        /// One rule, in one place, and shelter means shelter.
        ///
        /// The patch is kept rather than deleted because the method is the
        /// documented seam for anything that ever does want to bend the need,
        /// and because this comment is the only place the mistake is written
        /// down.
        /// </summary>
        [HarmonyPostfix]
        private static void Postfix(CreatureBase __instance, ref float __result)
        {
        }

        /// <summary>Level of the Count role, or 0 when the settler does not hold it.</summary>
        internal static int CountRank(HumanoidInstance humanoid)
        {
            var roleOwner = humanoid?.WorkerBehaviour?.HumanoidRoleOwner;
            if (roleOwner == null || !roleOwner.HasRole(CountRole)) return 0;

            return roleOwner.RoleLevel;
        }
    }

    /// <summary>
    /// Where the vampire is standing, not what hour it is.
    ///
    /// <b>The bug this replaces.</b> `VampireDaylightEffector` and
    /// `GhoulDaylightEffector` were appended to
    /// <c>DateTimeSettings.dayEffectors</c>, which is the list the game fires
    /// on everyone holding the perk the moment the sun comes up and ends when
    /// it goes down. It has no idea where anybody is. A vampire asleep in a
    /// sarcophagus three levels under the hill carried exactly the same entry,
    /// with exactly the same numbers, as one standing in an open field at noon
    /// - and both carried it for the same twelve hours.
    ///
    /// So the clock is out of it. The two day effectors are gone from
    /// `DateTimeSettings.json` and are started here instead, off the one number
    /// that already knows about roofs, walls and depth:
    /// <c>CreatureBase.GetReceivingLightAmount()</c>, which is 0 anywhere the
    /// sky cannot reach and climbs towards 1 in the open at midday. Two bands,
    /// so that walking into a doorway is felt on the next sweep:
    ///
    ///   - <b>sheltered</b> (below <c>Vampire/SunShelterLight</c>, and always
    ///     while asleep): nothing at all, on the mood side and on the stat side
    ///     both. This is the coffin, the cellar, the mine and the covered
    ///     walkway.
    ///   - <b>open sky</b> (at or above <c>Vampire/SunBurnLight</c>): the day
    ///     entry <em>and</em> the burn - real health off the top and real pain
    ///     with it, once every <c>Vampire/SunBurnSeconds</c>, out of an instant
    ///     effector so both numbers stay in the JSON where they can be tuned.
    ///
    /// Between the two bands the day entry is on and the burn is not, which is
    /// what a porch at dusk should feel like.
    ///
    /// <b>Sleep is shelter, whatever the light says.</b> A settler asleep is a
    /// settler in a bed, and a vampire's bed is the coffin; the game gives no
    /// cheap way to ask which bed, and asking would buy nothing - what was
    /// asked for is that a sleeping vampire is not being cooked, and that is
    /// exactly what this says. <c>Vampire/SleepShieldsFromSun</c> turns it off
    /// for anyone who would rather it did not.
    ///
    /// The Count's rank still buys the burn off, on the same ladder the stat
    /// side uses, so a rank 3 Count is untouched here too.
    /// </summary>
    internal static class VampireDaylight
    {
        internal const string VampireDayEffector = "VampireDaylightEffector";
        internal const string GhoulDayEffector = "GhoulDaylightEffector";

        private const string VampireBurnEffector = "VampireSunburn";
        private const string GhoulBurnEffector = "GhoulSunburn";

        /// <summary>
        /// How often the sweep runs, in real seconds. Short enough that
        /// stepping out of a door is felt before the settler has crossed the
        /// yard, long enough to cost nothing on a village of forty.
        /// </summary>
        private const float SweepSeconds = 4f;

        /// <summary>When each settler last took the burn, by unique id.</summary>
        private static readonly System.Collections.Generic.Dictionary<int, float> Burned =
            new System.Collections.Generic.Dictionary<int, float>();

        internal static void Start()
        {
            GMPlugin.Every("vampire daylight sweep", SweepSeconds, Sweep);
        }

        /// <summary>
        /// Whether the sun can be said to be off this creature entirely - under
        /// a roof, underground, at night, or asleep.
        /// </summary>
        internal static bool IsSheltered(HumanoidInstance humanoid)
        {
            if (humanoid == null) return true;
            if (!(GMPlugin.SunlightByPlace?.Value ?? true)) return false;

            if ((GMPlugin.SleepShieldsFromSun?.Value ?? true) && humanoid.IsSleeping) return true;

            return Light(humanoid) < (GMPlugin.SunShelterLight?.Value ?? 0.15f);
        }

        private static float Light(CreatureBase creature)
        {
            try
            {
                return creature.GetReceivingLightAmount();
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[sun] could not read the light: {e}");
                return 0f;
            }
        }

        /// <summary>
        /// What the last census said, so the line below is written when
        /// something changes and not four times a minute forever.
        /// </summary>
        private static string lastCensus;

        private static void Sweep()
        {
            if (!(GMPlugin.SunlightByPlace?.Value ?? true)) return;
            if (!MonoSingleton<WorkerManager>.IsInstantiated()) return;

            var burnAt = GMPlugin.SunBurnLight?.Value ?? 0.6f;
            var every = GMPlugin.SunBurnSeconds?.Value ?? 12f;

            var counted = 0;
            var open = 0;
            var brightest = 0f;

            foreach (var settler in WorkerManager.WorkersHere)
            {
                if (settler == null || settler.HasDisposed || settler.HasDied) continue;

                var ghoul = GameAccess.HasPerk(settler, VampireSunlight.GhoulPerk);
                var vampire = !ghoul && GameAccess.HasPerk(settler, VampireSunlight.VampirePerk);
                if (!ghoul && !vampire) continue;

                counted++;

                // Vanilla's sunlight need does not apply to the dead. Left to
                // itself it fires heatstroke on every vampire in the village
                // and never stops, because the Vampire perk drops the stat's
                // floor to 7 and the threshold is 15 - so a vampire is
                // heatstruck in its own bed, which is the opposite of the whole
                // idea. Topped up every sweep, and the effector ended if it got
                // in before we did.
                Unneed(settler);

                var light = Light(settler);
                if (light > brightest) brightest = light;

                // Rank buys the sun off, one step at a time: a rank 3 Count
                // walks out at noon and nothing happens at all.
                var rank = vampire ? VampireSunlight.CountRank(settler) : 0;
                var sheltered = rank >= 3 || IsSheltered(settler);

                Sync(settler, ghoul ? GhoulDayEffector : VampireDayEffector, !sheltered);
                Mend(settler, sheltered);

                if (sheltered)
                {
                    // Out of the sun is out of the sun: the visible mark goes
                    // with it, so the health panel stops saying they are
                    // burning the moment they step under a roof.
                    Unburn(settler);
                    continue;
                }

                open++;

                if (light < burnAt)
                {
                    Unburn(settler);
                    continue;
                }

                Burn(settler, ghoul ? GhoulBurnEffector : VampireBurnEffector, every, rank);
            }

            Census(counted, open, brightest, burnAt);
        }

        /// <summary>
        /// Says what the sweep is looking at.
        ///
        /// Every way this rule can fail is silent. No vampire in the village,
        /// the vampire indoors, the light never reaching the burn threshold, a
        /// rank 3 Count who is meant to be immune - all four look exactly like
        /// "el sol no hace nada", which is the report that has now come back
        /// twice. So the sweep says how many it found, how many the sky can
        /// reach, and the brightest light any of them is standing in against
        /// the number that would blister them. One line, and only when the
        /// answer changes.
        /// </summary>
        private static void Census(int counted, int open, float brightest, float burnAt)
        {
            var line = counted == 0
                ? "[sun] no vampire or ghoul among the settlers"
                : $"[sun] {counted} of ours, {open} under open sky, "
                  + $"brightest light {brightest:0.00} (burns at {burnAt:0.00})";

            if (line == lastCensus) return;

            lastCensus = line;
            GMPlugin.Log?.LogInfo(line);
        }

        /// <summary>
        /// Leaves the settler carrying the day entry exactly while the sky can
        /// see them, and takes it off the moment they step under something.
        /// </summary>
        private static void Sync(HumanoidInstance settler, string effectorId, bool wanted)
        {
            var stats = GameAccess.Stats(settler);
            if (stats == null) return;

            if (stats.IsEffectorActive(effectorId) == wanted) return;

            if (!wanted)
            {
                // ForceEndEffector, for the same reason the burn mark uses it:
                // EndEffector(string) walks away without doing anything when
                // the entry still has time on it, and comes back looking like
                // it worked. Anything this sweep puts up, this sweep has to be
                // able to take down on the next pass.
                GameAccess.ForceEndEffector(settler, effectorId);
                return;
            }

            if (!stats.StartEffector(effectorId, 1f, false, -1, null))
            {
                GMPlugin.Log?.LogWarning(
                    $"[sun] '{effectorId}' refused on {GameAccess.Name(settler)} "
                    + "- missing from Effectors.json, or banned on that creature");
            }
        }

        /// <summary>Mending out of the sun, and mending in the box.</summary>
        private const string ShelteredRest = "UndeadShelteredRest";
        private const string GraveRest = "UndeadGraveRest";

        /// <summary>
        /// Gives the health back, which nothing ever did.
        ///
        /// <b>The other half of the sunburn report.</b> Lifting the mark was
        /// only ever the visible half - "no es porque no este debajo es que si
        /// se quema la quemadura baja mucho". A vampire caught out at noon lost
        /// real health every twelve seconds and then mended at a living
        /// settler's rate, which is most of a day for a burn that took a
        /// minute. So the player saw a vampire walk into the cellar and stay
        /// hurt, and read that as the rule not working.
        ///
        /// Two bands, because the coffin has to be worth building:
        ///
        ///   - <b>under any roof</b>: mends about three times as fast as a
        ///     living settler.
        ///   - <b>asleep</b> - which for a vampire means the coffin, since that
        ///     is the bed it owns: eight times, and wounds with it.
        ///
        /// Both are plain effectors in <c>Effectors.json</c>, so the numbers
        /// live next to every other number a player might want to change, and
        /// both come straight back off in daylight.
        /// </summary>
        private static void Mend(HumanoidInstance settler, bool sheltered)
        {
            if (!(GMPlugin.ShelterHeals?.Value ?? true))
            {
                Sync(settler, ShelteredRest, false);
                Sync(settler, GraveRest, false);
                return;
            }

            var inTheBox = sheltered && settler.IsSleeping;

            Sync(settler, GraveRest, inTheBox);
            Sync(settler, ShelteredRest, sheltered && !inTheBox);
        }

        /// <summary>The wound the health panel shows while they are cooking.</summary>
        internal const string BurningMarkId = "undead_sunburn";

        /// <summary>The vanilla effector a low sunlight need fires.</summary>
        private const string Heatstroke = "heatstroke";

        /// <summary>
        /// Puts the sunlight need back to full and ends the heatstroke that a
        /// low one brings, for anything of ours.
        ///
        /// See the note on <see cref="VampireSunlight.Postfix"/>: the need is
        /// vanilla's vitamin D, a vampire's floor sits under the threshold that
        /// fires heatstroke, and the two together cook a vampire in its own
        /// coffin. The dead have no use for daylight, so the need is simply
        /// held at full and the sun reaches them the one way this mod says it
        /// does - in the open, through <see cref="Burn"/>.
        /// </summary>
        private static void Unneed(HumanoidInstance settler)
        {
            GameAccess.FillStat(settler, NSMedieval.StatsSystem.StatType.SunLight);

            // Forced for the same reason as the burn mark: heatstroke carries a
            // duration, so asking for it by name is a request the game is free
            // to decline, and it does.
            GameAccess.ForceEndEffector(settler, Heatstroke);
        }

        /// <summary>
        /// Takes the visible burn mark off. Called the moment they are out of
        /// the sun, so the panel never claims somebody is burning in a cellar.
        /// </summary>
        private static void Unburn(HumanoidInstance settler)
        {
            var stats = GameAccess.Stats(settler);
            if (stats == null || !stats.IsEffectorActive(BurningMarkId)) return;

            // ForceEndEffector, not EndEffector. The mark is a wound, and
            // EndEffector(string) refuses to cancel anything whose duration has
            // not run out - quietly, coming back as if it had worked. That is
            // the whole of "entra bajo techo y le sigue quemando": the sweep
            // asked every four seconds and the game said no every time, without
            // a line anywhere to say it had.
            var lifted = GameAccess.ForceEndEffector(settler, BurningMarkId);

            GMPlugin.Log?.LogInfo(lifted
                ? $"[sun] {GameAccess.Name(settler)} is out of the sun - burn mark lifted"
                : $"[sun] {GameAccess.Name(settler)} is out of the sun but the burn mark WOULD NOT COME OFF");
        }

        /// <summary>
        /// The burn itself. An instant effector, fired on a clock of its own so
        /// the interval is a number and not "whatever the sweep rate happens to
        /// be" - and so the health and the pain live in the JSON next to every
        /// other number the player might want to tune.
        ///
        /// It also puts up <see cref="BurningMarkId"/>, which is the standing
        /// wound the health panel names - "Quemandose al sol". The instant
        /// effector cannot do that job: it lasts a frame by definition, so it
        /// takes the health and vanishes, and the player asked to be able to
        /// look at a settler and see <em>why</em> they are losing it.
        ///
        /// A Count's rank thins the burn on the way past: rank 1 takes it as
        /// it comes, rank 2 gets a longer gap between burns, rank 3 never
        /// reaches here at all.
        /// </summary>
        private static void Burn(HumanoidInstance settler, string effectorId, float every, int rank)
        {
            if (every <= 0f) return;

            // The crypt lord is not immune, but the sun works on him slowly.
            if (rank >= 2) every *= 2.5f;

            var stats = GameAccess.Stats(settler);
            if (stats == null) return;

            // The mark goes up as soon as they are in the sun, not on the first
            // burn: twelve seconds of standing in the open with nothing in the
            // panel is twelve seconds of the player wondering.
            if (!stats.IsEffectorActive(BurningMarkId))
            {
                if (!stats.StartEffector(BurningMarkId, 1f, false, -1, null))
                {
                    GMPlugin.Log?.LogWarning(
                        $"[sun] '{BurningMarkId}' refused on {GameAccess.Name(settler)} "
                        + "- missing from Wounds.json");
                }
            }

            var now = UnityEngine.Time.time;

            float last;
            if (Burned.TryGetValue(settler.UniqueId, out last) && now - last < every) return;

            Burned[settler.UniqueId] = now;

            if (stats.StartEffector(effectorId, 1f, false, -1, null))
            {
                GMPlugin.Log?.LogInfo($"[sun] {GameAccess.Name(settler)} is burning in the open");
            }
            else
            {
                GMPlugin.Log?.LogWarning(
                    $"[sun] '{effectorId}' refused on {GameAccess.Name(settler)} "
                    + "- missing from Effectors.json, or banned on that creature");
            }
        }
    }
}
