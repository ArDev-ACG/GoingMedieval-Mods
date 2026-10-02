using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HarmonyLib;
using NSEipix.Base;
using NSEipix.Repository;
using NSMedieval;
using NSMedieval.Manager;
using NSMedieval.Model;
using NSMedieval.Repository;
using NSMedieval.State;
using NSMedieval.StatsSystem;
using NSMedieval.View;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// The turn itself, in one place.
    ///
    /// There are two ways a settler joins the horde - torn apart by one
    /// (<see cref="RisenFromTheKill"/>) or dying of the wound one left behind
    /// (<see cref="UndeadInfection"/>) - and until now they did different
    /// things. The kill path did the full six steps; the infection path called
    /// <c>BecomeAggressive()</c>, which returns on the spot for anyone still a
    /// Worker, so what it produced was a settler wearing the Risen perk and
    /// still drawing wages.
    ///
    /// Both go through here now, and here ends with the part that was missing
    /// from both: <see cref="RisenRoster.Evict"/>, which takes the new walker
    /// off the player's books.
    /// </summary>
    internal static class RisenTurn
    {
        /// <summary>
        /// Turns a settler, in the order the game insists on.
        ///
        ///   1. <b>Stand up.</b> Through <c>ForceUnFaint</c>, because the
        ///      game's own UnFaint only files a request while a raid runs.
        ///   2. <b>Clear everything.</b> Drop the load, end every effector -
        ///      wounds are effectors here, so this also stops the bleeding -
        ///      and refill blood and consciousness.
        ///   3. <b>Change sides</b> before anything asks which side it is on:
        ///      BecomeAggressive and the raid code both read Faction.
        ///   4. <b>Take the horde's character</b> - all eleven perks, not just
        ///      the Risen tag.
        ///   5. <b>Turn on the settlement</b> through EnemyBehaviour directly.
        ///   6. <b>Enlist</b> in the killer's raid, when there is a killer.
        ///   7. <b>Leave the settlement</b> - off the colonist bar, out of the
        ///      worker registry, out of the save's roster.
        ///
        /// A false anywhere before step 5 means the settler dies the ordinary
        /// way, which is the right failure: better a corpse than a colonist who
        /// is neither dead nor one of them.
        /// </summary>
        internal static bool Turn(HumanoidInstance humanoid, HumanoidInstance killer, string how)
        {
            if (humanoid == null) return false;

            var faction = GameAccess.FactionByBlueprint(RisenPallor.UndeadFactionId);
            if (faction == null)
            {
                GMPlugin.Log?.LogWarning(
                    $"[risen] no '{RisenPallor.UndeadFactionId}' faction in this world - "
                    + $"{GameAccess.Name(humanoid)} stays as it was");
                return false;
            }

            // 1. Stand up.
            GameAccess.ForceUnFaint(humanoid);

            // 2. Clear everything.
            GameAccess.Inventory(humanoid)?.ClearInventoryOnDeath();
            var cleared = GameAccess.EndAllEffectors(humanoid);

            var restored = GameAccess.HealFraction(humanoid, GMPlugin.RisePercent?.Value ?? 0f);
            GameAccess.FillStat(humanoid, StatType.Blood);
            GameAccess.FillStat(humanoid, StatType.Consciousness);

            if (restored <= 0f)
            {
                GMPlugin.Log?.LogWarning(
                    $"[risen] {GameAccess.Name(humanoid)} could not be brought back up - dying normally");
                return false;
            }

            // 3. Change sides.
            humanoid.SetFaction(faction);

            // 4. Take the horde's character.
            if (!RisenTraits.ApplyTo(humanoid))
            {
                GMPlugin.Log?.LogWarning(
                    $"[risen] the horde's perks did not take on {GameAccess.Name(humanoid)} "
                    + "- revisar que existan en el repositorio");
                return false;
            }

            // 5. Turn on the settlement.
            if (!GameAccess.TurnEnemy(humanoid))
            {
                GMPlugin.Log?.LogWarning(
                    $"[risen] {GameAccess.Name(humanoid)} could not be moved onto EnemyBehaviour "
                    + "- dying normally");
                return false;
            }

            // 6. Fall in with the raid that made it, so the commander has
            //    somewhere to send it. An infection has no killer to enlist
            //    with; RisenRestless is what gives that one something to do.
            var enlisted = killer != null && GameAccess.JoinRaid(humanoid, killer);

            // 7. Stop being one of the player's, and get watched for standing
            //    still - a settler that turned with no raid to join has nobody
            //    to tell it where to go.
            //
            //    The view is taken first and on purpose: WorkerManager's
            //    dictionary is the only index from a humanoid to the thing that
            //    draws it, and step 7 is what empties that entry.
            var view = RisenNameplate.ViewOf(humanoid);
            var evicted = RisenRoster.Evict(humanoid);
            RisenRestless.Watch(humanoid);

            // 8. And a name over its head, in the horde's red. A walker drawn
            //    by a WorkerView never reaches NPCView.InstantiateNameElement,
            //    so nothing else was ever going to give it one.
            var named = RisenNameplate.Apply(humanoid, view);

            GMPlugin.Log?.LogInfo(
                $"[risen] {GameAccess.Name(humanoid)} {how}, shed {cleared} effector(s) and got back "
                + $"up for {faction.BlueprintId}"
                + (enlisted ? $" in raid {humanoid.EnemyBehaviour?.RaidId}" : " with no raid to join")
                + (evicted ? ", off the settlement's books" : ", STILL on the colonist list")
                + (named ? ", named like the rest of the horde" : ", WITH NO NAME over it"));

            return true;
        }
    }

    /// <summary>
    /// Takes a walker off the player's books.
    ///
    /// This is the "sigo teniendolos en la barra de colonos" bug, and it is not
    /// one oversight but two, both inside the same call. Turning a settler goes
    /// through <c>SetActiveBehaviour&lt;EnemyBehaviour&gt;</c>, which does the
    /// view swap in two halves:
    ///
    ///   - <c>IncognitoDispose</c> sets <c>isInIncognitoMode = true</c>
    ///     <b>before</b> calling <c>WorkerController.RemoveWorker</c>, and
    ///     <c>WorkersViewManager.OnWorkerRemoved</c> opens with "if this one is
    ///     incognito, do not take it out of the list, just re-sort". Incognito
    ///     is how the game parks a settler who left with a caravan and is
    ///     coming back, so keeping the portrait is right there and wrong here.
    ///   - <c>IncognitoSpawn</c> then re-registers anything whose
    ///     <c>workerBehaviour</c> field is not null - which is every settler
    ///     ever born, whatever behaviour it is on now - so the walker is handed
    ///     back a WorkerView, a row in the registry and a place in the save's
    ///     roster.
    ///
    /// Between them, what the player sees is a colonist who cannot be selected
    /// and still reports what it is doing. Worse than the portrait: the horde's
    /// own <c>EnemyTargetWorkersAiPlanGoal</c> reads that same registry, so
    /// walkers kept picking their own dead as the thing to hunt, walked to
    /// them, found nothing to swing at, and stopped. That is the
    /// "ya no buscan mas" half of the report.
    ///
    /// The eviction is done by hand rather than through
    /// <c>WorkerController.RemoveWorker</c> for one reason: that call ends in
    /// <c>WorkerView.Dispose()</c>, and the WorkerView is the only view this
    /// body has - a turned settler is drawn by the worker view it was born
    /// with - so disposing it would leave a walker nothing would ever draw
    /// again. The registry entry goes; the view stays.
    /// </summary>
    internal static class RisenRoster
    {
        private const string WalkerNpcId = "undead_horde_walker_easy";

        internal static bool Evict(HumanoidInstance humanoid)
        {
            if (humanoid == null) return false;

            var ok = false;

            try
            {
                ok |= FromSave(humanoid);
                ok |= FromRegistry(humanoid);
                ok |= FromBar(humanoid);

                if (MonoSingleton<WorkerController>.IsInstantiated())
                {
                    WorkerController.Instance.WorkerCountChanged();
                }
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[risen] could not evict {GameAccess.Name(humanoid)}: {e}");
            }

            return ok;
        }

        /// <summary>
        /// The save's own two lists.
        ///
        /// <c>RemoveWorker</c> covers both <c>Workers</c> and the
        /// <c>workersById</c> index behind it, which is what a load rebuilds a
        /// settlement from.
        ///
        /// The NPC list is only joined when the walker can be given a real NPC
        /// blueprint, because <c>NPCManager.CreateViewAndSetup</c> refuses one
        /// without ("Tried to create NPC with no behaviour or blueprint") and a
        /// save full of NPCs that cannot be rebuilt is a worse bug than the one
        /// being fixed. With the blueprint on, a settler that turned mid-raid
        /// comes back after a reload as what it became; without it the body is
        /// simply not saved, and reloading reads as the horde having finished
        /// the job.
        /// </summary>
        private static bool FromSave(HumanoidInstance humanoid)
        {
            var village = GlobalSaveController.CurrentVillageData;
            if (village == null) return false;

            village.RemoveWorker(humanoid);

            if (Adopt(humanoid) && !village.NPCs.Contains(humanoid)) village.AddNPC(humanoid);

            return true;
        }

        /// <summary>
        /// Hands the walker the horde's own NPC blueprint, which is what makes
        /// <c>HumanoidInstance.IsNpc()</c> - literally <c>Blueprint is NPC</c> -
        /// answer yes about a body that was born a settler.
        ///
        /// Any of the three walkers would do: all this is read for is the
        /// equipment preset on a reload, and the fact of not being null.
        /// </summary>
        private static bool Adopt(HumanoidInstance humanoid)
        {
            var behaviour = humanoid.ActiveBehaviour;
            if (behaviour == null) return false;
            if (behaviour.NpcBlueprint != null) return true;
            if (BlueprintRef == null) return false;

            var npc = Repository<NPCRepository, NPC>.Instance?.GetByID(WalkerNpcId);
            if (npc == null)
            {
                GMPlugin.Log?.LogWarning(
                    $"[risen] no '{WalkerNpcId}' in NPCs.json - {GameAccess.Name(humanoid)} will not "
                    + "survive a reload");
                return false;
            }

            BlueprintRef(behaviour) = npc;
            return behaviour.NpcBlueprint != null;
        }

        /// <summary>
        /// The manager's own registry, minus the view.
        ///
        /// <c>AllWorkers</c>, <c>WorkersHere</c>, <c>GetWorkersCount</c> and
        /// every "is there a settler who could do this" search read this
        /// dictionary, and so does the goal the horde hunts people with. The
        /// fainted set is cleaned with it because a settler killed while
        /// unconscious is in there, and nothing else would ever take it out.
        /// </summary>
        private static bool FromRegistry(HumanoidInstance humanoid)
        {
            if (!MonoSingleton<WorkerManager>.IsInstantiated()) return false;

            var manager = WorkerManager.Instance;
            if (manager == null) return false;

            var removed = false;

            if (ViewsRef != null)
            {
                WorkerView view;
                removed = ViewsRef(manager).TryRemove(humanoid, out view);
            }

            if (FaintedRef != null) FaintedRef(manager).Remove(humanoid);

            return removed;
        }

        /// <summary>
        /// The portrait strip itself. The list is public and the redraw is a
        /// single call, so this half needs no reflection - only the timing:
        /// after the behaviour swap, when <c>isInIncognitoMode</c> is false
        /// again and a removal is finally read as a removal.
        /// </summary>
        private static bool FromBar(HumanoidInstance humanoid)
        {
            if (!MonoSingleton<global::Managers.WorkersViewManager>.IsInstantiated()) return false;

            var bar = global::Managers.WorkersViewManager.Instance;
            if (bar?.Workers == null) return false;

            if (!bar.Workers.Remove(humanoid)) return false;

            // Non-public, like most of what actually redraws anything here.
            // Without it the row keeps its pixels until something else happens
            // to touch the list.
            Redraw?.Invoke(bar, null);
            return true;
        }

        private static readonly System.Reflection.MethodInfo Redraw =
            AccessTools.Method(typeof(global::Managers.WorkersViewManager), "NotifyListUpdate");

        private static readonly AccessTools.FieldRef<WorkerManager,
            ConcurrentDictionary<HumanoidInstance, WorkerView>> ViewsRef = Views();

        private static readonly AccessTools.FieldRef<WorkerManager, HashSet<HumanoidInstance>>
            FaintedRef = Fainted();

        private static readonly AccessTools.FieldRef<HumanoidBehaviour, HumanoidBlueprint>
            BlueprintRef = Blueprint();

        private static AccessTools.FieldRef<WorkerManager,
            ConcurrentDictionary<HumanoidInstance, WorkerView>> Views()
        {
            try
            {
                return AccessTools.FieldRefAccess<WorkerManager,
                    ConcurrentDictionary<HumanoidInstance, WorkerView>>("instanceViewDictionary");
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[risen] instanceViewDictionary unreachable: {e.Message}");
                return null;
            }
        }

        private static AccessTools.FieldRef<WorkerManager, HashSet<HumanoidInstance>> Fainted()
        {
            try
            {
                return AccessTools.FieldRefAccess<WorkerManager, HashSet<HumanoidInstance>>(
                    "faintedWorkers");
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[risen] faintedWorkers unreachable: {e.Message}");
                return null;
            }
        }

        private static AccessTools.FieldRef<HumanoidBehaviour, HumanoidBlueprint> Blueprint()
        {
            try
            {
                return AccessTools.FieldRefAccess<HumanoidBehaviour, HumanoidBlueprint>("blueprint");
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[risen] behaviour blueprint unreachable: {e.Message}");
                return null;
            }
        }
    }
}
