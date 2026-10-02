using System.Collections.Generic;
using NSMedieval;
using NSMedieval.CombatAi;
using NSMedieval.State;
using NSMedieval.Types;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// A walker that has stopped is a walker that has finished.
    ///
    /// Every way the horde picks a target ends at a search, and every search
    /// can come back empty: the commander's room picker only looks at rooms it
    /// can reach, the chase goal only at settlers it can path to, the raze goal
    /// only at things it may attack. When all three miss, the goal that was
    /// running ends and nothing starts, and what is left on screen is a corpse
    /// standing in a field, forever.
    ///
    /// The reported version of this was worse than a stray: with the turned
    /// settlers still in the worker registry, the chase goal kept finding
    /// <em>them</em>, walking over, and discovering there was nothing to swing
    /// at - because the horde does not attack its own. <see cref="RisenRoster"/>
    /// is what fixes that, and this is the belt to its braces: whatever the
    /// reason, half a minute of a walker not moving and not swinging is taken
    /// as the search having failed, and everything it is holding on to is let
    /// go so the search can run again from nothing.
    ///
    /// <b>Not moving is not the same as doing nothing.</b> A walker taking a
    /// wall apart stands perfectly still for as long as the wall lasts, and
    /// interrupting that every half minute would mean no wall ever came down.
    /// So progress is either of two things - a tile of ground covered, or a
    /// blow landed - and the timer only runs when neither has happened. The
    /// blow is read as "has <c>LastAttackTime</c> changed since the last look",
    /// which needs no assumption about what unit the game keeps that in.
    /// </summary>
    internal static class RisenRestless
    {
        /// <summary>How often the sweep runs, in real seconds.</summary>
        private const float SweepSeconds = 3f;

        private sealed class Mark
        {
            internal Vec3Int Where;
            internal object Struck;
            internal float Progressed;
        }

        private static readonly Dictionary<int, Mark> Seen = new Dictionary<int, Mark>();

        /// <summary>
        /// Walkers made out of settlers, which are not in the village's NPC
        /// list unless they could be given the horde's blueprint. Kept here so
        /// a turned settler standing alone in a burnt-out village still gets
        /// looked at.
        /// </summary>
        private static List<HumanoidInstance> Turned => Census.Turned;

        internal static void Watch(HumanoidInstance humanoid)
        {
            if (humanoid != null && !Turned.Contains(humanoid)) Turned.Add(humanoid);
        }

        /// <summary>
        /// The same roster, for anyone else who needs "every walker on the map"
        /// and would otherwise miss the ones the village's NPC list never got.
        /// Read-only on purpose: pruning belongs to <see cref="Horde"/>, which
        /// is the only place that knows a body has gone.
        /// </summary>
        internal static IEnumerable<HumanoidInstance> TurnedWalkers()
        {
            return Turned;
        }

        internal static void Start()
        {
            GMPlugin.Every("undead restless watch", SweepSeconds, Sweep);
        }

        private static void Sweep()
        {
            if (!(GMPlugin.UndeadRestless?.Value ?? true)) return;

            var patience = GMPlugin.UndeadIdleSeconds?.Value ?? 30f;
            if (patience <= 0f) return;

            foreach (var walker in Horde())
            {
                Look(walker, patience);
            }
        }

        /// <summary>
        /// Everything of ours that is upright and hostile right now. The NPC
        /// list is the village's own; <see cref="Turned"/> covers the bodies
        /// that were settlers an hour ago, and is pruned here rather than on a
        /// timer of its own.
        /// </summary>
        private static IEnumerable<HumanoidInstance> Horde()
        {
            var npcs = GlobalSaveController.CurrentVillageData?.NPCs;
            if (npcs != null)
            {
                for (var i = 0; i < npcs.Count; i++)
                {
                    if (IsStanding(npcs[i])) yield return npcs[i];
                }
            }

            for (var i = Turned.Count - 1; i >= 0; i--)
            {
                var walker = Turned[i];

                if (walker == null || walker.HasDisposed || walker.HasDied)
                {
                    if (walker != null) Seen.Remove(walker.UniqueId);
                    Turned.RemoveAt(i);
                    continue;
                }

                if (npcs != null && npcs.Contains(walker)) continue;   // counted once
                if (IsStanding(walker)) yield return walker;
            }
        }

        private static bool IsStanding(HumanoidInstance walker)
        {
            if (walker == null || walker.HasDisposed || walker.HasDiedOrFainted) return false;
            if (!(walker.ActiveBehaviour is EnemyBehaviour)) return false;

            return Census.IsUndead(walker);
        }

        private static void Look(HumanoidInstance walker, float patience)
        {
            var ai = walker.CombatAi;
            if (ai == null) return;

            var here = walker.GetGridPosition();
            // As object, not long: the game stores TimerController.TimeSinceStartup,
            // a float, and GetState<long> on a float is default(long) - which
            // read as "never swung" and had this kick walkers out of every
            // fight they stood still in, order, target and goal and all.
            var struck = ai.GetState<object>(CombatAiState.LastAttackTime);

            Mark mark;
            if (!Seen.TryGetValue(walker.UniqueId, out mark))
            {
                Seen[walker.UniqueId] = new Mark
                {
                    Where = here, Struck = struck, Progressed = Time.time,
                };
                return;
            }

            var moved = !here.Equals(mark.Where);
            var swung = !Equals(struck, mark.Struck) || RisenBrawl.StruckSince(walker, mark.Progressed);

            mark.Where = here;
            mark.Struck = struck;

            if (moved || swung)
            {
                mark.Progressed = Time.time;
                return;
            }

            if (Time.time - mark.Progressed < patience) return;

            // Once per stretch of standing still, not once per sweep.
            mark.Progressed = Time.time;
            Kick(walker, ai);
        }

        /// <summary>
        /// Lets go of everything that could be holding the walker in place, in
        /// the order the game reads them back in.
        ///
        ///   - <b>The order</b>, because <c>EnemyTargetProductionAiPlanGoal
        ///     .CanStart</c> refuses outright to anyone carrying one, and an
        ///     order to attack something unreachable is exactly what the
        ///     commander hands out when a raid runs out of village.
        ///   - <b>The target</b>, both the preferred one and the next one the
        ///     switcher had lined up. A stale target is the usual reason the
        ///     goals all decline: something is already chosen, so nothing
        ///     chooses again.
        ///   - <b>The raze memory</b>, so the chase is tried once more from
        ///     scratch before the walker settles for a fence.
        ///   - <b>The sensors</b>, which are what notice there is anything out
        ///     there at all, and finally the goal, aborted so the scheduler
        ///     picks a new one on the next tick instead of sitting inside one
        ///     whose plan can no longer be carried out.
        /// </summary>
        private static void Kick(HumanoidInstance walker, CombatAiAgent ai)
        {
            try
            {
                var enemy = walker.ActiveBehaviour as EnemyBehaviour;
                if (enemy != null) enemy.CurrentOrder = null;

                Set(ai, CombatAiState.PreferedTarget, null);
                Set(ai, CombatAiState.NextTarget, null);
                Set(ai, CombatAiState.NextTargetValidated, false);

                RisenChaseFirst.Forget(walker.UniqueId);

                Call(ResetSensors, ai);
                Call(RefreshSensors, ai);
                Call(RefreshReactions, ai);

                walker.GoapAgent?.Abort();

                GMPlugin.Log?.LogInfo(
                    $"[risen] {GameAccess.Name(walker)} stood still too long - looking again");
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[risen] could not restart {GameAccess.Name(walker)}: {e}");
            }
        }

        /// <summary>
        /// None of the four calls below is reachable from outside the game
        /// assembly, and all four are the whole of what "look again" means, so
        /// they are resolved once by name and a missing one says so instead of
        /// silently doing nothing.
        /// </summary>
        private static void Set(CombatAiAgent ai, CombatAiState state, object value)
        {
            SetState?.Invoke(ai, new[] { state, value });
        }

        private static void Call(System.Reflection.MethodInfo method, CombatAiAgent ai)
        {
            method?.Invoke(ai, null);
        }

        private static readonly System.Reflection.MethodInfo SetState =
            Find("SetState", typeof(CombatAiState), typeof(object));

        private static readonly System.Reflection.MethodInfo ResetSensors = Find("ResetSensorState");
        private static readonly System.Reflection.MethodInfo RefreshSensors = Find("ForceRefreshSensors");
        private static readonly System.Reflection.MethodInfo RefreshReactions = Find("ForceRefreshReactions");

        private static System.Reflection.MethodInfo Find(string name, params System.Type[] args)
        {
            var method = HarmonyLib.AccessTools.Method(typeof(CombatAiAgent), name,
                args.Length == 0 ? null : args);

            if (method == null) GMPlugin.Log?.LogError($"[risen] CombatAiAgent.{name} not found");

            return method;
        }
    }
}
