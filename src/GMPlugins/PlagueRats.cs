using System.Linq;
using System.Reflection;
using HarmonyLib;
using NSEipix.Base;
using NSMedieval.GameEventSystem;
using NSMedieval.Goap;
using NSMedieval.Manager;
using NSMedieval.State;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// What the four classes below have in common: the bodies nobody buried.
    /// </summary>
    internal static class Carrion
    {
        internal const string RatId = "rat";
        internal const string FeverEffectorId = "plague_fever";
        internal const string RatRaidEventId = "game_event_rat_raid";
        internal const string AnimalRaidGroupId = "animalRaid";

        /// <summary>
        /// Human bodies lying on the ground right now.
        ///
        /// <c>ResourcePileTracker.CarcassPiles</c> is a set of
        /// <c>HumanCarcassPileInstance</c>, one per body, and a body put into a
        /// grave stops being a pile - so this counts exactly what the player
        /// would call "cadaveres sin enterrar", and counts down as the
        /// gravedigger works.
        /// </summary>
        internal static int Corpses()
        {
            if (!MonoSingleton<ResourcePileTracker>.IsInstantiated()) return 0;

            var piles = ResourcePileTracker.Instance?.CarcassPiles;
            return piles?.Count ?? 0;
        }

        internal static bool IsRat(CreatureBase creature)
        {
            var animal = creature as AnimalInstance;
            return animal?.Blueprint != null && animal.Blueprint.GetID() == RatId;
        }

        private static float lastBiteLine;
        private static int bitesSince;

        /// <summary>
        /// One line per stretch of biting, with a count, rather than one per
        /// bite: thirty rats on one settler would otherwise write faster than
        /// the log can be read.
        /// </summary>
        internal static void Bit(HumanoidInstance victim)
        {
            bitesSince++;

            var now = UnityEngine.Time.time;
            if (now - lastBiteLine < 5f) return;

            lastBiteLine = now;
            GMPlugin.Log?.LogInfo(
                $"[plague] {bitesSince} rat bite(s) landed, last on {GameAccess.Name(victim)}");
            bitesSince = 0;
        }
    }

    /// <summary>
    /// A rat you can tell apart from a rat.
    ///
    /// Every rat in this mod carries the plague - <c>AnimalBase.json</c> hands
    /// the species our own hit effector group - and until now nothing said so.
    /// The name says it in the panel; this says it at a glance across the
    /// field, and it says it the way the game already says everything about an
    /// animal's coat.
    ///
    /// <c>AnimalView.SetMaterialBasedOnType</c>, which runs inside
    /// <c>Setup</c>, pushes <c>animalInstance.FurColor</c> into the shader's
    /// <c>_FurColor</c>. So there is nothing to paint here: put the colour on
    /// the instance before the view is set up and the game paints it, every
    /// time the view is rebuilt, with no material of ours anywhere.
    ///
    /// The field is not public, which is the only reason this needs reflection.
    /// </summary>
    [HarmonyPatch]
    internal static class PlagueRatMark
    {
        private static readonly AccessTools.FieldRef<AnimalInstance, Color> FurRef = Fur();

        private static bool told;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(NSMedieval.View.Animals.AnimalView), "Setup",
                new[] { typeof(AnimalInstance) });
        }

        private static void Prefix(AnimalInstance instance)
        {
            if (FurRef == null || instance == null) return;
            if (!Carrion.IsRat(instance)) return;

            var wanted = GMPlugin.PlagueRatColor?.Value;
            if (string.IsNullOrEmpty(wanted)) return;

            Color colour;
            if (!ColorUtility.TryParseHtmlString(wanted, out colour))
            {
                if (!told)
                {
                    told = true;
                    GMPlugin.Log?.LogWarning($"[plague] '{wanted}' is not a colour the game can read");
                }

                return;
            }

            FurRef(instance) = colour;

            if (!told)
            {
                told = true;
                GMPlugin.Log?.LogInfo($"[plague] rats painted {wanted}");
            }
        }

        private static AccessTools.FieldRef<AnimalInstance, Color> Fur()
        {
            try
            {
                return AccessTools.FieldRefAccess<AnimalInstance, Color>("furColor");
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[plague] furColor unreachable: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// A rat bite that can actually give somebody the plague.
    ///
    /// It could not before, and the reason is the arithmetic that hid the
    /// undead wound all over again: <c>CombatHitManager
    /// .CheckAndStartHitEffectors</c> starts a hit effector only when
    /// <c>hitInfo.Damage &gt;= threshold * current health</c>. A mature rat's
    /// <c>UnarmedDamage</c> is <b>1</b>, and the group asked for 0.15 - fifteen
    /// percent of a settler's health, about fifteen damage. A rat could only
    /// ever have infected someone already down to seven hit points, which is to
    /// say: never.
    ///
    /// Lowering the number in the JSON until a rat clears it means every single
    /// bite infects, and thirty rats bite a lot. So the fever comes out of the
    /// group entirely and is rolled here instead, where a chance can exist. The
    /// small bite wounds stay in the JSON, at thresholds a one-damage bite can
    /// actually reach.
    ///
    /// Immunity needs no check: surviving the fever bans the effector on that
    /// settler for good (<see cref="PlagueImmunity"/>), and a banned effector
    /// refuses to start.
    /// </summary>
    [HarmonyPatch]
    internal static class PlagueRatBite
    {
        /// <summary>The plague badge, on the settler that just got bitten.</summary>
        internal const string PlagueIcon = "aldrich_bubble_plague_bite";

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(CombatHitManager), "HandleHitEffectors");
        }

        // Positional: the game's parameter names are not a documented surface,
        // and a wrong one is an IL compile error at startup, not a warning.
        // The signature is (IDamageDealAgent attacker, IDamageTakingAgent
        // victim, CombatHitInfo hit).
        private static void Postfix(IDamageDealAgent __0, IDamageTakingAgent __1)
        {
            if (!Carrion.IsRat(__0 as CreatureBase)) return;

            var bitten = __1 as HumanoidInstance;
            if (bitten == null || bitten.HasDisposed || bitten.HasDied) return;

            // Every landed bite, not only the infecting ones. "Hacen la
            // animacion pero no surge ningun efecto" is two different bugs
            // wearing the same face - the bite never landing, and the bite
            // landing for one point of damage on a settler with a hundred - and
            // the log said nothing about either, because until now the only
            // line here was printed on the 12% that infected.
            Carrion.Bit(bitten);

            var chance = GMPlugin.PlagueBiteChance?.Value ?? 0f;
            if (chance <= 0f) return;

            var stats = GameAccess.Stats(bitten);
            if (stats == null || stats.IsEffectorActive(Carrion.FeverEffectorId)) return;

            // <b>What the mask is for.</b> Until now a plague mask was two
            // attribute bumps and nothing else: the one number it ought to
            // move - the odds a bite takes - it did not touch, so a settler
            // in the full robe caught it exactly as often as one bare-faced.
            // The wards are read off the stats rather than off the equipment
            // list because the equipment side already puts them there, and a
            // stat is the same question for a vanilla mouthpiece as for ours.
            chance *= Ward(stats);

            if (Random.value > chance) return;

            if (!stats.StartEffector(Carrion.FeverEffectorId, 1f, false, -1, null)) return;

            BiteFeedback.Bubble(bitten, PlagueIcon);
            GMPlugin.Log?.LogInfo($"[plague] {GameAccess.Name(bitten)} was bitten by a plague rat");
        }

        /// <summary>
        /// What the settler is wearing over its face, as a multiplier on the
        /// odds of a bite infecting.
        ///
        /// Only the best one counts. The three pieces all sit in the head
        /// slot, so nobody can wear two of them anyway, but reading it this
        /// way means the rule does not change if a later slot lets them.
        /// </summary>
        // internal, no private: el contagio entre colonos lee el mismo
        // numero. Una careta que para la peste de una rata y no la del
        // vecino no es una careta, es una excepcion.
        /// <remarks>
        /// Two slots since the 21st. The coat (<c>PlagueHoodWard</c>, the id
        /// kept from when it was a hood) is a body garment now, and what goes
        /// on the face - the beaked mask, or vanilla cloth - is the head slot,
        /// so a settler can wear one of each and the two multiply: mask and
        /// coat together is 0.35 x 0.15 with the defaults, about one bite in
        /// twenty that would have taken.
        /// </remarks>
        internal static float Ward(NSMedieval.StatsSystem.StatsInstance stats)
        {
            var ward = 1f;

            if (stats.IsEffectorActive("PlagueHoodWard"))
                ward *= GMPlugin.PlagueHoodWard?.Value ?? 0.15f;

            if (stats.IsEffectorActive("PlagueMaskWard"))
                ward *= GMPlugin.PlagueMaskWard?.Value ?? 0.35f;
            else if (stats.IsEffectorActive("MouthpiecePlagueWard"))
                ward *= GMPlugin.PlagueClothWard?.Value ?? 0.70f;

            return ward;
        }
    }

    /// <summary>
    /// Bodies bring rats.
    ///
    /// This was written down as wanted and never built, and the partida of the
    /// 6th is what that costs: twenty bodies on the ground and not one rat,
    /// which is exactly what vanilla promises. Nothing in the game ties the two
    /// together - <c>animalRaid</c> is weighed by days past, settler count,
    /// season and map type, and that is the whole list.
    ///
    /// <c>EventGroupInstance.GetValue</c> is where all of those are multiplied
    /// into one number, and every group's number is weighed against every
    /// other's when the scheduler picks. So one more multiplier here is the
    /// smallest change that makes a graveyard of a village louder than a tidy
    /// one.
    /// </summary>
    [HarmonyPatch]
    internal static class CarrionDrawsRats
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EventGroupInstance), "GetValue");
        }

        private static void Postfix(EventGroupInstance __instance, ref float __result)
        {
            var per = GMPlugin.RatsPerCorpse?.Value ?? 0f;
            if (per <= 0f || __result <= 0f) return;
            if (__instance?.Blueprint == null) return;
            if (__instance.Blueprint.GetID() != Carrion.AnimalRaidGroupId) return;

            var corpses = Carrion.Corpses();
            if (corpses <= 0) return;

            __result *= 1f + corpses * per;
        }
    }

    /// <summary>
    /// And past a certain number of them, it is the rats.
    ///
    /// The weight above only buys the group a turn; which of its four events
    /// fires is a second draw, and three of the four are foxes, polecats and
    /// dogs. A village standing in its own dead getting a fox is the wrong
    /// answer often enough to be worth taking out of the game's hands.
    /// </summary>
    [HarmonyPatch]
    internal static class CarrionPicksRats
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EventGroup), "GetEventToStart");
        }

        private static void Postfix(EventGroup __instance, ref GameEvent __result)
        {
            var enough = GMPlugin.RatsCorpseCertainty?.Value ?? 0;
            if (enough <= 0 || __instance == null) return;
            if (__instance.GetID() != Carrion.AnimalRaidGroupId) return;
            if (__result != null && __result.GetID() == Carrion.RatRaidEventId) return;

            var corpses = Carrion.Corpses();
            if (corpses < enough) return;

            var rats = __instance.GameEvents?.FirstOrDefault(e => e?.GetID() == Carrion.RatRaidEventId);
            if (rats == null) return;

            GMPlugin.Log?.LogInfo($"[plague] {corpses} unburied - the animal raid is the rats");
            __result = rats;
        }
    }

    /// <summary>
    /// Gives the plague rat a bite worth flinching at.
    ///
    /// The report was "hacen la animacion o el intento pero no esta surgiendo
    /// ningun efecto ni el de ataque normal", and the arithmetic says both
    /// halves of that are the same number. `attributes_rat_mature` sets
    /// <c>UnarmedDamage</c> to <b>1</b>. A settler stands at a hundred hit
    /// points and heals faster than that: the bite lands, the game takes its
    /// one point, and on screen absolutely nothing happens - no wound, no
    /// blood, no health bar that visibly moves. It is not a miss, it is a hit
    /// too small to see, which looks exactly like a miss.
    ///
    /// Melee cannot miss for want of skill here, incidentally, whatever the
    /// rat's <c>UnarmedPrecision</c> of 0.1 suggests: <c>CombatHitManager
    /// .HasHit</c> only consults precision on the ranged branch, and for melee
    /// the only roll is the target's evade. So the rats were connecting all
    /// along.
    ///
    /// <c>CombatCalculator.GetBaseDamage</c> is where the attacker's number is
    /// read before armour, criticals and the rest are applied to it, so
    /// multiplying here leaves every other rule intact. Only the rat is
    /// touched, and only while the mod is loaded - a tamed rat in a pen bites
    /// just as hard, which is the correct answer for an animal that is carrying
    /// the plague.
    ///
    /// The multiplier is a config entry because the right number is a matter of
    /// how nasty a swarm should be, not a fact: 4 turns a bite from invisible
    /// into something a settler notices, without making one rat a threat.
    /// </summary>
    [HarmonyPatch]
    internal static class PlagueRatTeeth
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(NSMedieval.Manager.CombatCalculator), "GetBaseDamage");
        }

        // Positional: the first argument is the attacker, and a wrong parameter
        // name is an IL compile error at startup rather than a warning.
        private static void Postfix(IDamageDealAgent __0, ref float __result)
        {
            var bite = GMPlugin.PlagueRatDamage?.Value ?? 1f;
            if (bite <= 1f || __result <= 0f) return;

            if (!Carrion.IsRat(__0 as CreatureBase)) return;

            __result *= bite;
        }
    }
}
