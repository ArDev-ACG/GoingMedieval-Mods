using System.Collections.Generic;
using NSEipix.Base;
using NSMedieval.Manager;
using NSMedieval.State;
using NSMedieval.StatsSystem;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Blood is a need, and a vampire that goes without stops asking politely.
    ///
    /// <b>Why this had to exist.</b> Until now feeding was pure profit: a bite
    /// healed, it drank, and nothing ever made a vampire <em>want</em> one.
    /// Being a vampire was a list of bonuses with a sunburn attached, and the
    /// player had no reason to build any of the court around it. What was asked
    /// for is the other half - "mostrar en los efectos como negativo Sed de
    /// Sangre, y si la conciencia baja, que su rebeldia sea ir a morder a lo
    /// que sea".
    ///
    /// <b>Why a clock of ours and not a stat.</b> A real need is an entry in
    /// <c>StatsModelRepository.json</c>, and a mod cannot add one: the stats
    /// model is built per creature type at load and that repository takes no
    /// new stats. So the thirst lives here, one number per vampire, ticking in
    /// real seconds and reset by <see cref="VampireBite"/> whenever one feeds.
    /// What the player sees is an effector, which is what the game's own needs
    /// show them anyway.
    ///
    /// <b>Three stages, and the last one is a person going wrong.</b>
    ///
    ///   - <b>Mild</b> - the edge of it: a mood penalty and nothing more.
    ///   - <b>Rising</b> - it starts to cost. Worse temper, slower hands, and
    ///     consciousness beginning to slip.
    ///   - <b>Ravenous</b> - consciousness far enough gone that the settler is
    ///     not entirely present, and this is where the rebellion happens: they
    ///     go for the nearest living thing on their own, settler or goat or
    ///     trader, without being told.
    ///
    /// The rebellion is deliberately <em>not</em> a turn. A hungry vampire is
    /// still one of the player's and stays on the colonist bar; what they lose
    /// is the judgement to leave the neighbours alone. That is a problem the
    /// player can fix - feed them, lock them in, put them in the coffin - and a
    /// settler who could not be fixed would just be a death sentence on a
    /// timer.
    /// </summary>
    internal static class VampireThirst
    {
        private const float SweepSeconds = 5f;

        internal const string MildEffector = "VampireThirstMild";
        internal const string RisingEffector = "VampireThirstRising";
        internal const string RavenousEffector = "VampireThirstRavenous";

        private static readonly string[] Stages = { MildEffector, RisingEffector, RavenousEffector };

        /// <summary>
        /// When each vampire last had blood, by unique id. Real seconds,
        /// because that is what every other clock in this mod runs on, and a
        /// paused game should not make anybody hungrier.
        /// </summary>
        private static readonly Dictionary<int, float> LastFed = new Dictionary<int, float>();

        internal static void Start()
        {
            GMPlugin.Every("vampire thirst", SweepSeconds, Sweep);
        }

        /// <summary>
        /// Called from the bite: a meal resets the clock and takes every stage
        /// of the thirst back off.
        /// </summary>
        internal static void Fed(HumanoidInstance vampire)
        {
            if (vampire == null) return;

            LastFed[vampire.UniqueId] = UnityEngine.Time.time;

            var stats = GameAccess.Stats(vampire);
            if (stats == null) return;

            // ForceEndEffector, not the polite call: EndEffector(string)
            // checks elapsed < Duration first and walks away without doing
            // anything, which looks exactly like success. The three thirst
            // stages have a duration, so the polite version never took a
            // single one off - which is why a vampire that had just fed kept
            // the "Sed de Sangre" entry in its panel.
            foreach (var id in Stages)
            {
                if (!stats.IsEffectorActive(id)) continue;
                if (!GameAccess.ForceEndEffector(vampire, id))
                {
                    GMPlugin.Log?.LogWarning(
                        $"[thirst] could not clear '{id}' off {GameAccess.Name(vampire)}");
                }
            }

            GMPlugin.Log?.LogInfo($"[thirst] {GameAccess.Name(vampire)} has fed - the thirst resets");
        }

        private static void Sweep()
        {
            var minutes = GMPlugin.ThirstMinutes?.Value ?? 0f;
            if (minutes <= 0f) return;
            if (!MonoSingleton<WorkerManager>.IsInstantiated()) return;

            var now = UnityEngine.Time.time;
            var full = minutes * 60f;

            foreach (var settler in WorkerManager.WorkersHere)
            {
                if (settler == null || settler.HasDisposed || settler.HasDied) continue;
                if (!GameAccess.HasPerk(settler, VampireSunlight.VampirePerk)) continue;

                float fed;
                if (!LastFed.TryGetValue(settler.UniqueId, out fed))
                {
                    // First sighting starts the clock rather than starving them
                    // on the spot: a vampire that has just been made has not
                    // been going without for a week.
                    LastFed[settler.UniqueId] = now;
                    continue;
                }

                Stage(settler, (now - fed) / full);
            }
        }

        /// <summary>
        /// Puts exactly one stage on the settler - the one their thirst has
        /// reached - and takes the other two off.
        ///
        /// One at a time, because three stacked mood penalties read in the
        /// panel as a single enormous one, and because the player should be
        /// able to glance at a vampire and see how far gone it is.
        /// </summary>
        private static void Stage(HumanoidInstance vampire, float thirst)
        {
            var wanted = thirst >= 1.0f ? RavenousEffector
                       : thirst >= 0.66f ? RisingEffector
                       : thirst >= 0.33f ? MildEffector
                       : null;

            var stats = GameAccess.Stats(vampire);
            if (stats == null) return;

            foreach (var id in Stages)
            {
                var want = id == wanted;
                if (stats.IsEffectorActive(id) == want) continue;

                if (!want)
                {
                    // Same veto as in Fed(): a staged effector has a duration,
                    // so it has to be forced out or the vampire ends up
                    // wearing two stages at once.
                    GameAccess.ForceEndEffector(vampire, id);
                    continue;
                }

                if (!stats.StartEffector(id, 1f, false, -1, null))
                {
                    GMPlugin.Log?.LogWarning(
                        $"[thirst] '{id}' refused on {GameAccess.Name(vampire)} "
                        + "- missing from Effectors.json");
                    continue;
                }

                GMPlugin.Log?.LogInfo(
                    $"[thirst] {GameAccess.Name(vampire)} is at {id} ({thirst:P0} of the way)");
            }

            Hunt(vampire, wanted);
        }

        /// <summary>
        /// When each vampire was last sent to feed itself, by unique id.
        ///
        /// The sweep runs every five seconds and <see cref="BiteRetry.Send"/>
        /// aborts whatever the settler is doing, so without this a thirsty
        /// vampire would be pulled off its own walk twelve times a minute and
        /// never arrive anywhere. One order, then long enough to carry it out.
        /// </summary>
        private static readonly Dictionary<int, float> LastSent = new Dictionary<int, float>();

        private const float OrderCooldownSeconds = 25f;

        /// <summary>
        /// A thirsty vampire looks after itself.
        ///
        /// <b>What changed.</b> This used to be one rule - at Ravenous, and
        /// only once Consciousness had already slipped, go for whatever is
        /// nearest - which in practice meant nothing ever happened: the player
        /// watched the mood entry go from bad to worse while the vampire kept
        /// hauling stone. What was asked for is the plain version - "morder a
        /// alguien cuando se tiene sed debe ser automatico, buscar botellas de
        /// sangre o morder animales, en el ultimo nivel de sed si atacar
        /// colonos o mercaderos" - and it is the better rule anyway, because
        /// the player can see it coming and has two ways to stop it.
        ///
        /// So the ladder is the thirst's own:
        ///
        ///   - <b>Mild</b> - nothing. It is an itch, not a decision.
        ///   - <b>Rising</b> - a bottle of blood draught out of the
        ///     settlement's stock if there is one, and failing that the nearest
        ///     animal. Nobody in the village is touched at this stage, which is
        ///     the window the player has to go and brew something.
        ///   - <b>Ravenous</b> - the bottle first, still, and then whoever is
        ///     nearest: settler, trader, goat, it stops mattering.
        ///
        /// Consciousness is no longer the gate. It was the wrong one twice
        /// over: it never fell far enough to fire while the vampire was working
        /// normally, and now that the dead do not faint - see
        /// <see cref="UndeadNeverFaints"/> - it is no longer even the thing
        /// that measures how far gone one is.
        ///
        /// It is deliberately not a turn. A hungry vampire is still one of the
        /// player's and stays on the colonist bar; what it loses is the
        /// judgement to leave the neighbours alone, and that is a problem with
        /// three fixes - feed it, bottle it, or shut it in the coffin.
        /// </summary>
        private static void Hunt(HumanoidInstance vampire, string stage)
        {
            if (!(GMPlugin.ThirstFeedsItself?.Value ?? true)) return;
            if (stage != RisingEffector && stage != RavenousEffector) return;

            // Rising thirst waits for the coffin to open. BiteRetry.Send aborts
            // whatever the settler is doing, sleep included, and a bite that
            // cannot land sends them back to bed - so every 25 s a day-sleeping
            // vampire got up, wandered off and lay down again, which read as
            // "no respetan el horario". Ravenous still wakes them, the way
            // vanilla starvation does.
            if (stage == RisingEffector && vampire.IsSleeping) return;

            var now = UnityEngine.Time.time;
            float sent;
            if (LastSent.TryGetValue(vampire.UniqueId, out sent)
                && now - sent < OrderCooldownSeconds) return;

            LastSent[vampire.UniqueId] = now;

            if (Bottle(vampire)) return;

            var prey = Nearest(vampire, stage == RavenousEffector);
            if (prey == null)
            {
                GMPlugin.Log?.LogInfo(
                    $"[thirst] {GameAccess.Name(vampire)} has nothing within reach to feed on");
                return;
            }

            if (!BiteRetry.Send(vampire, prey)) return;

            GMPlugin.Log?.LogWarning(
                $"[thirst] {GameAccess.Name(vampire)} went for {GameAccess.Name(prey)} on its own "
                + $"({stage})");
        }

        internal const string BloodDraughtId = "blood_draught";
        private const string DraughtEffector = "BloodDraughtFeast";

        /// <summary>
        /// Takes one blood draught out of the settlement's stock, if there is
        /// one within reach, and counts it as a meal.
        ///
        /// <b>This is a shortcut and it is written down as one.</b> The proper
        /// version is a goal of its own - walk to the pile, pick it up, drink
        /// it - the way <see cref="BiteGoal"/> is for the bite, and that is
        /// what should eventually replace it. What this does instead is take
        /// one out of the stock, fire the same effector the bottle's
        /// <c>onUseEffects</c> would, and reset the thirst; the vampire does
        /// not walk anywhere. <c>Vampire/ThirstBottleRange</c> is what keeps
        /// that from being a vampire drinking out of a cellar on the far side
        /// of the map.
        ///
        /// It is still the right behaviour to have first: it is the whole
        /// reason to build the altar and brew the stuff, and without it the
        /// bottles the player makes do nothing but sit in a crate while the
        /// vampires eat the livestock.
        /// </summary>
        private static bool Bottle(HumanoidInstance vampire)
        {
            if (!MonoSingleton<ResourcePileManager>.IsInstantiated()) return false;

            var blueprint = NSEipix.Repository.Repository<NSMedieval.Repository.ResourceRepository,
                NSMedieval.Model.Resource>.Instance?.GetByID(BloodDraughtId);
            if (blueprint == null) return false;

            var piles = MonoSingleton<ResourcePileManager>.Instance;
            if (piles.GetPilesCount(blueprint) <= 0) return false;

            if (!Within(vampire, piles, blueprint)) return false;

            if (piles.RemoveResources(blueprint, 1, false, null, null) <= 0) return false;

            GameAccess.Stats(vampire)?.StartEffector(DraughtEffector, 1f, false, -1, null);

            GMPlugin.Log?.LogInfo(
                $"[thirst] {GameAccess.Name(vampire)} drank a blood draught out of the stock");

            Fed(vampire);
            return true;
        }

        /// <summary>
        /// Whether any pile of that resource is close enough to count as the
        /// settlement's own stock rather than somebody else's cellar.
        /// </summary>
        private static bool Within(HumanoidInstance vampire, ResourcePileManager piles,
                                   NSMedieval.Model.Resource blueprint)
        {
            var range = GMPlugin.ThirstBottleRange?.Value ?? 0f;
            if (range <= 0f) return true;

            var here = vampire.GetPosition();
            var reach = range * range;

            try
            {
                // PooledHashSet is a struct - it is never null, and asking
                // does not compile.
                using (var found = piles.GetAllPiles(blueprint))
                {
                    foreach (var pile in found)
                    {
                        if (pile == null || pile.HasDisposed) continue;
                        if ((NSEipix.VectorExtension.ToVector3World(pile.GridDataPosition) - here)
                            .sqrMagnitude <= reach) return true;
                    }
                }
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[thirst] could not look for bottles: {e}");
            }

            return false;
        }

        /// <summary>
        /// The closest living thing that is not another vampire and not the
        /// settler itself.
        ///
        /// Settlers and animals both, on purpose: what was asked for is "ir a
        /// morder a lo que sea", and a vampire that only ever went for people
        /// would empty the village while the goats watched.
        /// </summary>
        private static CreatureBase Nearest(HumanoidInstance vampire, bool peopleToo)
        {
            CreatureBase best = null;
            var nearest = float.MaxValue;

            var here = vampire.GetPosition();

            // Below Ravenous the village is off the menu: the hunt goes for
            // livestock only, which is the window the player has to react in.
            if (peopleToo)
            foreach (var other in WorkerManager.WorkersHere)
            {
                if (other == null || other.HasDisposed || other.HasDiedOrFainted) continue;
                if (ReferenceEquals(other, vampire)) continue;

                // Not each other: a court that eats itself does not last long,
                // and two starving vampires in one room would be a loop.
                if (GameAccess.HasPerk(other, VampireSunlight.VampirePerk)) continue;

                var far = (other.GetPosition() - here).sqrMagnitude;
                if (far >= nearest) continue;

                nearest = far;
                best = other;
            }

            // The village's own list, which is the same place the horde reads
            // its NPCs from - AnimalController is all events and no roster.
            var animals = NSMedieval.GlobalSaveController.CurrentVillageData?.Animals;
            if (animals == null) return best;

            foreach (var animal in animals)
            {
                if (animal == null || animal.HasDisposed || animal.HasDiedOrFainted) continue;

                var far = (animal.GetPosition() - here).sqrMagnitude;
                if (far >= nearest) continue;

                nearest = far;
                best = animal;
            }

            return best;
        }
    }
}
