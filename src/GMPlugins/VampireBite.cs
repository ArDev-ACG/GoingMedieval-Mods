using System.Reflection;
using HarmonyLib;
using NSMedieval.Controllers;
using NSMedieval.Goap;
using NSMedieval.State;
using NSMedieval.StatsSystem;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// What a vampire's teeth do, and where each way of using them ends up.
    ///
    /// There are two routes into this class and they deliberately do different
    /// things:
    ///
    ///   - <b>a landed melee hit</b> (the Harmony postfix below). The vampire
    ///     feeds, and the shock of the blood loss sometimes drops the victim
    ///     where they stand. It never turns anyone. Turning people by hitting
    ///     them made every fight a recruitment drive, which is not what a raid
    ///     should feel like.
    ///   - <b>the ordered bite</b> (<see cref="OrderedBite"/>, driven by
    ///     BiteMenuItem and BiteGoal). The vampire walks over and drinks
    ///     deliberately. Only here can someone be turned into a Ghoul, and only
    ///     if they are a prisoner: a bound captive is the one victim who cannot
    ///     fight back or walk away, so it is the one place the bite has the
    ///     time to take. It is still a roll, not a certainty.
    ///
    /// The stun is a percentage per hit, not a counter, so a long fight has no
    /// memory of the last one: each blow is its own roll.
    ///
    /// "Stun" is <see cref="GameAccess.Faint"/>. The game has no stun of its
    /// own - the only word it knows for "out of the fight but alive" is
    /// fainting, which aborts the victim's GOAP agent, lays them down and lets
    /// FaintGoal wake them later. That is also exactly the state that lets the
    /// player capture them, which is what feeds the prisoner half of the rule
    /// above.
    ///
    /// The feeding effector doubles as the cooldown: while it is running the
    /// vampire is full, so a long fight is one meal rather than thirty.
    /// </summary>
    [HarmonyPatch]
    internal static class VampireBite
    {
        internal const string VampirePerk = "Vampire";
        internal const string GhoulPerk = "Ghoul";
        private const string FedEffector = "VampireFedOnBlood";
        private const string DrainedEffector = "VampireBloodDrained";

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(CombatController), "OnDamageTaken");
        }

        private static void Postfix(IDamageDealAgent deal, IDamageTakingAgent take, CombatHitInfo hitInfo)
        {
            var biter = deal as HumanoidInstance;
            if (biter == null || !GameAccess.HasPerk(biter, VampirePerk)) return;
            if (!hitInfo.DidAnyDamage()) return;

            var victim = take as CreatureBase;
            if (!CanBeDrunkFrom(victim, biter)) return;

            Feed(biter);
            Sate(biter, Drain(biter, victim));
            TryStun(biter, victim);
        }

        /// <summary>
        /// Anything with blood the vampire has not already spoiled. A Ghoul is
        /// spoken for; a fellow vampire is not food.
        /// </summary>
        internal static bool CanBeDrunkFrom(CreatureBase victim, HumanoidInstance biter)
        {
            if (victim == null || ReferenceEquals(victim, biter) || victim.HasDied) return false;

            var human = victim as HumanoidInstance;
            if (human == null) return true;   // animals are always a meal

            return !GameAccess.HasPerk(human, GhoulPerk) && !GameAccess.HasPerk(human, VampirePerk);
        }

        /// <summary>
        /// The deliberate bite: walk up, drink, and - on a prisoner only - roll
        /// for the turning. A turned prisoner wakes up on the player's side,
        /// which is the same thing recruiting one does, minus the persuasion.
        /// </summary>
        internal static void OrderedBite(HumanoidInstance biter, CreatureBase victim)
        {
            if (biter == null || !CanBeDrunkFrom(victim, biter)) return;

            Feed(biter);
            Sate(biter, Drain(biter, victim));
            GMPlugin.Log?.LogInfo($"[bite] {GameAccess.Name(biter)} drank from {GameAccess.Name(victim)}");

            var human = victim as HumanoidInstance;

            // A person shows the bite themselves: the blood-loss effector puts
            // "Mareado por la sangre perdida" over their head, with the tipped
            // goblet on it. An animal has no mood, so no effector runs and no
            // bubble appears - the goat lost a slice of health and nothing on
            // screen said why. This is that missing half, and only that half:
            // putting it on people too would double up on the effector bubble.
            if (human == null) BiteFeedback.Bubble(victim, BiteFeedback.BittenIcon);

            // Un colono libre queda marcado: es un thrall (Thrall.cs).
            if (human != null && !GameAccess.IsCaptive(human)) Thrall.Mark(human);

            if (human == null || !GameAccess.IsCaptive(human)) return;

            var chance = GMPlugin.GhoulTurnChance?.Value ?? 0f;
            if (UnityEngine.Random.value >= chance)
            {
                GMPlugin.Log?.LogInfo($"[bite] {GameAccess.Name(human)} held on to their own blood");
                return;
            }

            human.TryAddNewPerk(GhoulPerk);
            GMPlugin.Log?.LogInfo($"[bite] {GameAccess.Name(human)} turned into a ghoul");
            Enlist(human);
        }


        /// <summary>
        /// What the bite costs the other side.
        ///
        /// Until now feeding was free for everyone except the vampire's own
        /// conscience: the biter healed and got a mood, and the victim walked
        /// off untouched. Blood taken out of someone has to leave a mark, so
        /// this takes a slice of their health and leaves them light-headed -
        /// slower on their feet, slower at work, easier to hit and quicker to
        /// tire.
        ///
        /// The effector is the cooldown as well as the wound: a victim who is
        /// still light-headed does not lose another slice, so a long fight
        /// costs one bite's worth of blood rather than thirty.
        ///
        /// Animals bleed but do not sulk - the effector is a mood, and there is
        /// no mood on a goat - so they only take the health.
        /// </summary>
        private static float Drain(HumanoidInstance biter, CreatureBase victim)
        {
            var stats = GameAccess.Stats(victim);
            if (stats == null || stats.IsEffectorActive(DrainedEffector)) return 0f;

            var lost = GameAccess.DrainFraction(victim, GMPlugin.BiteDrainPercent?.Value ?? 0f);

            // The actual blood, which until now the bite never touched. Every
            // creature carries a Blood stat of its own, 0 to 100, with the
            // game's own thresholds at 80, 50 and 10 firing ConDmgBloodLoss01,
            // 02 and 03 on the way down: light-headed, then failing, then
            // critical. Taking a slice out of it is how the bite borrows all of
            // that instead of restating it in an effector of ours - and it is
            // why drinking from the same throat all week eventually kills.
            var blood = GameAccess.DrainStat(victim, StatType.Blood,
                GMPlugin.BiteBloodPercent?.Value ?? 0f);

            if (victim is HumanoidInstance)
            {
                stats.StartEffector(DrainedEffector, 1f, false, biter?.UniqueId ?? -1, null);
            }

            if (lost > 0f || blood > 0f)
            {
                GMPlugin.Log?.LogInfo(
                    $"[bite] {GameAccess.Name(victim)} lost {lost:F1} health and {blood:F1} blood "
                    + $"to {GameAccess.Name(biter)}");
            }

            // As a fraction of that creature's own blood, not as a number:
            // what the vampire got out of a goat and out of a trader are
            // different amounts of the same mouthful.
            var pool = stats.GetStat(StatType.Blood);
            return pool == null || pool.Max <= 0f ? 0f : blood / pool.Max;
        }

        /// <summary>
        /// Blood is food.
        ///
        /// Until now a vampire drank and then went and ate bread, which made
        /// the thirst a second need bolted onto a settler rather than the
        /// thing that replaced the first one. What was asked for is plain -
        /// "morder a alguien y tomar sangre quita el hambre, el porcentaje que
        /// le quito" - so the hunger comes back by exactly the share of the
        /// victim's blood that ended up in the vampire, times whatever
        /// <see cref="GMPlugin.BloodFeedsHunger"/> says a mouthful is worth.
        ///
        /// Nothing here is clamped by hand: <c>AddCurrent</c> stops at the
        /// stat's own maximum, so a vampire that drains a room does not end up
        /// with a hunger bar off the end of the panel.
        /// </summary>
        private static void Sate(HumanoidInstance biter, float bloodFraction)
        {
            if (biter == null || bloodFraction <= 0f) return;

            var worth = GMPlugin.BloodFeedsHunger?.Value ?? 0f;
            if (worth <= 0f) return;

            var fed = GameAccess.RaiseStat(biter, StatType.Hunger, bloodFraction * worth);
            if (fed <= 0f) return;

            GMPlugin.Log?.LogInfo(
                $"[bite] the blood put {fed:F1} of {GameAccess.Name(biter)}'s hunger back");
        }

        /// <summary>
        /// Blood loss enough to drop them, rolled fresh on every hit. Someone
        /// already down cannot be dropped again.
        /// </summary>
        private static void TryStun(HumanoidInstance biter, CreatureBase victim)
        {
            var chance = GMPlugin.BiteStunChance?.Value ?? 0f;
            if (chance <= 0f || UnityEngine.Random.value >= chance) return;
            if (GameAccess.HasFainted(victim)) return;

            if (GameAccess.Faint(victim))
            {
                GMPlugin.Log?.LogInfo(
                    $"[bite] {GameAccess.Name(biter)} drained {GameAccess.Name(victim)} into a faint");
            }
        }

        /// <summary>
        /// Puts the turned prisoner under the player's command.
        ///
        /// This is the whole of what the game does when a prisoner is recruited:
        /// RecruitPrisonerGoal ends by calling SetActiveBehaviour&lt;WorkerBehaviour&gt;
        /// and nothing else - no faction rewrite, no re-instantiated view.
        /// </summary>
        private static void Enlist(HumanoidInstance human)
        {
            if (ReferenceEquals(human.ActiveBehaviour, human.WorkerBehaviour)) return;

            try
            {
                human.SetActiveBehaviour<WorkerBehaviour>(true);
                GMPlugin.Log?.LogInfo($"[bite] {GameAccess.Name(human)} now answers to the settlement");
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[bite] could not enlist {GameAccess.Name(human)}: {e.Message}");
            }
        }

        /// <summary>
        /// One meal: the effector, and the blood it puts back.
        ///
        /// The effector doubles as the cooldown - while it runs the vampire is
        /// full - so the healing is inside that guard and a long fight heals
        /// once, not thirty times.
        ///
        /// <b>The thirst is not inside the guard, and that was the bug.</b>
        /// It used to be, and the log of the 14th says exactly what that cost:
        /// eight bites in a row - cat, goat, goat, mallard, hare, mallard,
        /// mallard, hare - every one of them logging "drank from", and not one
        /// "the thirst resets" among them. <c>VampireFedOnBlood</c> lasts 24
        /// seconds and the hunt sends an order every 25, so from the second
        /// bite onward the guard was always up: the vampire kept the Blood
        /// Thirst entry in its panel while it drained half the livestock, and
        /// stopped only when it found a bottle. Drinking is what resets the
        /// thirst, so it resets on every drink; the cooldown is about the
        /// healing, which is a different question.
        /// </summary>
        private static void Feed(HumanoidInstance biter)
        {
            var stats = GameAccess.Stats(biter);
            if (stats == null) return;

            // The meal is the whole point of the thirst, so it is the thirst's
            // reset too - see VampireThirst. Outside the guard, always.
            VampireThirst.Fed(biter);

            if (stats.IsEffectorActive(FedEffector)) return;

            stats.StartEffector(FedEffector, 1f, false, -1, null);

            var healed = GameAccess.HealFraction(biter, GMPlugin.BiteHealPercent?.Value ?? 0f);
            if (healed > 0f)
            {
                GMPlugin.Log?.LogInfo(
                    $"[bite] {GameAccess.Name(biter)} healed {healed:F1} from the blood");
            }
        }
    }
}
