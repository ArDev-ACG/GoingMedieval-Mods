using System.Reflection;
using HarmonyLib;
using NSMedieval.CombatAi;
using NSMedieval.Goap;
using NSMedieval.State;
using NSMedieval.StatsSystem;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// A settler the horde kills does not stay killed.
    ///
    /// <see cref="UndeadInfection"/> already covers the other half of this - the
    /// wound that festers and closes wrong - and deliberately only turns one in
    /// three, because surviving the bite has to be the likely outcome or nobody
    /// would ever fight the horde twice. Being torn apart by one is the opposite
    /// case and gets the opposite answer: it always turns, because there is
    /// nothing left to survive.
    ///
    /// The interception is <c>HumanoidInstance.OnHealthDepleted</c>, which is
    /// the whole of dying: it fires BeforeDeathEvent, marks the creature dead in
    /// CreatureBase, and hands off to the behaviour to drop the body, write the
    /// history entry and tell the settlement it lost someone. Skipping it skips
    /// all three, and what stands in its place is a settler on a sliver of
    /// health, carrying the Risen perk, on EnemyBehaviour - the same three
    /// things the infection does, minus the dice.
    ///
    /// <b>The health has to go back first.</b> Skipping the death with the bar
    /// still empty means the next tick calls this again, and the tick after
    /// that; putting a quarter of it back is what makes the refusal stick. It
    /// also happens to be the right number for play: what gets up is easy to put
    /// down, which is the difference between losing a settler and losing the
    /// settlement.
    ///
    /// Who did the killing is read the way <c>CreatureBase.IsKilled</c> reads it
    /// - the last agent to land damage, if it landed recently - but without that
    /// method's <c>hasDied</c> guard, since the entire point here is to be asked
    /// before anyone has died.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenFromTheKill
    {
        /// <summary>
        /// How long a blow still counts as the killing one, in the game's own
        /// time units. Taken from IsKilled, which uses the same window to decide
        /// whether a death was a kill or just bad luck.
        /// </summary>
        private const long KillWindow = 2;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(HumanoidInstance), "OnHealthDepleted",
                new[] { typeof(bool) });
        }

        private static bool Prefix(HumanoidInstance __instance, bool wasNaturalDeath)
        {
            if (__instance == null || __instance.HasDied) return true;

            // Only the player's own. A raider killed by a walker is already on
            // the horde's side of the fight, and turning a trader would make
            // every caravan that walks into a raid a permanent enemy.
            if (!ReferenceEquals(__instance.ActiveBehaviour, __instance.WorkerBehaviour)) return true;

            // Not "does it carry the perk" but "is it one of them by any of the
            // three tests": a settler killed again on the tick it rose used to
            // come back a second time, twice grey and twice enlisted, because
            // the perk was the only half being asked about.
            if (Census.IsUndead(__instance)) return true;

            // The infection first, and on any death at all.
            //
            // This is "no los convierte en zombies si por alguna razon mueren",
            // and the gap it closes is the shape of the old rule rather than a
            // slip inside it. Turning was written as the ending of a *wound*:
            // the roll lives in UndeadInfection, on EndEffector, which is the
            // tick where a festering wound finally closes. A settler who dies
            // while still carrying it never reaches that tick - the effector is
            // torn down with the body, not closed - so the one case where
            // getting back up is least in doubt was the one case that could not
            // happen.
            //
            // What killed them is beside the point: torn apart, bled out,
            // starved, fallen off a roof, or the fever itself. Something of the
            // horde's was in the blood when the body stopped, and that is the
            // whole of the condition. No dice either - the one in three is the
            // chance of *surviving* the wound, and this settler did not.
            if ((GMPlugin.RiseFromInfectionDeath?.Value ?? true)
                && UndeadInfection.IsInfected(__instance))
            {
                // A killer, when there is one, only decides which raid the
                // newcomer falls in with. A fever has none, and RisenRestless
                // is what gives that one something to do.
                var byWhom = Killer(__instance);
                if (byWhom != null && !Census.IsUndead(byWhom)) byWhom = null;

                if (Rise(__instance, byWhom, "died with the infection in them")) return false;
            }

            if (!(GMPlugin.RiseFromUndeadKill?.Value ?? false)) return true;
            if (wasNaturalDeath) return true;

            var killer = Killer(__instance);
            if (killer == null || !Census.IsUndead(killer)) return true;

            return !Rise(__instance, killer, $"was killed by {GameAccess.Name(killer)}");
        }

        /// <summary>
        /// The turn itself lives in <see cref="RisenTurn"/>, because the
        /// infection needs exactly the same seven steps and used to do three
        /// of them.
        /// </summary>
        private static bool Rise(HumanoidInstance humanoid, HumanoidInstance killer, string how)
        {
            return RisenTurn.Turn(humanoid, killer, how);
        }

        /// <summary>
        /// Whoever last drew blood, if they did it just now.
        ///
        /// CombatAi keeps both halves as plain states - the agent and the moment
        /// - and IsKilled is nothing more than the two of them read together. A
        /// creature bleeding out from a wound taken yesterday has a stale
        /// LastDamageTakenFrom, which is what the time window is for.
        /// </summary>
        private static HumanoidInstance Killer(CreatureBase victim)
        {
            var ai = victim?.CombatAi;
            if (ai == null) return null;

            var now = NSMedieval.GlobalSaveController.CurrentVillageData?.DateAndTime;
            if (now == null) return null;

            var struck = ai.GetState<long>(CombatAiState.LastDamageTakenTime);
            if (now.CurrentTimeTutorialAware - struck >= KillWindow) return null;

            return ai.GetState<IDamageDealAgent>(CombatAiState.LastDamageTakenFrom)
                   as HumanoidInstance;
        }
    }
}
