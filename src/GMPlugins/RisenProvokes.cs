using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSMedieval.CombatAi;
using NSMedieval.Goap;
using NSMedieval.State;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// A raider hit by a walker hits back.
    ///
    /// <b>What was reported.</b> "si los alzados atacan a un enemigo no colono
    /// - un personaje con nombre en rojo de otra faccion - este no regresa el
    /// golpe". Settlers and animals answered; raiders stood and took it.
    ///
    /// <b>Two doors, both shut.</b>
    ///
    ///   - <c>OnHitAiReaction.OnHit</c> is where anything that is hit picks
    ///     its attacker as the next target. It gives up when the victim
    ///     already has a target and the attacker is the same kind of body -
    ///     one humanoid hitting another while busy is written off as a stray
    ///     blow. A raider walking to the village always has a target.
    ///   - Even when a next target does get written, the raider never reads
    ///     it: <c>CombatAiTargetSwitcherEnemy.DoProcessing</c> returns false
    ///     outright while the raider carries its commander's order, which is
    ///     the whole of a raid. And the call for backup refuses on purpose,
    ///     because the walkers are enemies too as far as the game can tell.
    ///
    /// <see cref="RisenProvokes"/> opens the first: a blow from a walker always
    /// names the walker as the next target of a raider or visitor it lands
    /// on. <see cref="RisenProvokesUnderOrders"/> opens the second, but only
    /// for that: when the target waiting to be taken is one of ours, the
    /// switcher runs as it would with no order. Every other target stays
    /// behind the order, so a raid still behaves like a raid.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenProvokes
    {
        private static readonly HashSet<int> Said = new HashSet<int>();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(OnHitAiReaction), "OnHit");
        }

        private static void Postfix(IDamageDealAgent deal, IDamageTakingAgent take)
        {
            try
            {
                var walker = deal as HumanoidInstance;
                if (walker == null || !Census.IsUndead(walker)) return;
                if (walker.HasDisposed || walker.HasDiedOrFainted) return;

                var victim = take as HumanoidInstance;
                if (victim == null || victim.HasDisposed || victim.HasDiedOrFainted) return;
                if (Census.IsUndead(victim) || victim.IsWorker()) return;

                // Whatever the game makes of it, the brawl sweep now knows who
                // this one has a score to settle with.
                RisenBrawl.Provoked(victim, walker);

                var ai = victim.CombatAi;
                if (ai == null || ai.HasDisposed) return;
                if (ReferenceEquals(ai.GetState<IDamageTakingAgent>(CombatAiState.PreferedTarget), walker)) return;
                if (ReferenceEquals(ai.GetState<IDamageTakingAgent>(CombatAiState.NextTarget), walker)) return;

                RisenGoalState.Set(ai, CombatAiState.NextTarget, walker);

                if (Said.Add(victim.UniqueId))
                {
                    GMPlugin.Log?.LogInfo(
                        $"[risen] {GameAccess.Name(victim)} devuelve el golpe a {GameAccess.Name(walker)}");
                }
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[risen] could not make a victim answer a walker: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Lets a raider under orders take a walker as its target. See
    /// <see cref="RisenProvokes"/> for why the order has to be stepped round.
    ///
    /// The gate is the first line of the method -
    /// <c>if (enemyBehaviour?.CurrentOrder != null) return false;</c> - and it
    /// reads a private field. While the target waiting to be taken is a
    /// walker, the prefix blanks that field and the postfix puts it back, so
    /// the rest of the method runs exactly as vanilla wrote it. (The first
    /// version called the base method through a reverse patch, and Harmony
    /// refused to patch the class at all.)
    /// </summary>
    [HarmonyPatch]
    internal static class RisenProvokesUnderOrders
    {
        private static readonly System.Type Switcher =
            AccessTools.TypeByName("NSMedieval.CombatAi.CombatAiTargetSwitcherEnemy");

        private static readonly FieldInfo AgentField =
            AccessTools.Field(typeof(CombatAiTargetSwitcherDefault), "agent");

        private static readonly FieldInfo BehaviourField =
            Switcher == null ? null : AccessTools.Field(Switcher, "enemyBehaviour");

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(Switcher, "DoProcessing");
        }

        private static void Prefix(object __instance, out object __state)
        {
            __state = null;
            try
            {
                if (BehaviourField == null) return;

                var ai = AgentField?.GetValue(__instance) as CombatAiAgent;
                if (ai == null || ai.HasDisposed) return;

                var owner = ai.AgentOwner as HumanoidInstance;
                if (owner == null || Census.IsUndead(owner)) return;

                var next = ai.GetState<IDamageTakingAgent>(CombatAiState.NextTarget)
                           ?? ai.GetState<IDamageTakingAgent>(CombatAiState.NextTargetValidated);
                var walker = next as HumanoidInstance;
                if (walker == null || !Census.IsUndead(walker)) return;

                __state = BehaviourField.GetValue(__instance);
                if (__state != null) BehaviourField.SetValue(__instance, null);
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[risen] target switch under orders failed: {e.Message}");
            }
        }

        private static void Finalizer(object __instance, object __state)
        {
            if (__state != null) BehaviourField?.SetValue(__instance, __state);
        }
    }
}
