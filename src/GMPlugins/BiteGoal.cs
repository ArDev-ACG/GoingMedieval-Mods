using System.Collections.Generic;
using NSEipix.Base;
using NSMedieval.Goap;
using NSMedieval.Goap.Actions;
using NSMedieval.Manager;
using NSMedieval.State;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// The vampire walks over to whoever the player pointed at and drinks.
    ///
    /// Shaped after RecruitPrisonerGoal, which is the vanilla goal that also
    /// takes a living creature as its target: one step to reach it, one timed
    /// step to do the thing, and every step failing out the moment the target
    /// stops existing. The victim arrives through the same door every
    /// prioritise-menu order uses - AdditionalMenuPrioritiseItem.ForceGoal
    /// parks it on the ReservationManager as the agent's preferred reservable,
    /// and the first init step lifts it back out.
    ///
    /// The class and its constructor are public on purpose: the game builds
    /// goals through ConstructorInfo.Invoke on entries of GoalsMap.Constuctors,
    /// and Type.GetConstructors() only ever hands back public ones.
    ///
    /// There is no bite animation, so the vampire stands over the victim for
    /// the duration. That is the accepted cost until there is a Unity project
    /// to build one in.
    /// </summary>
    public class BiteGoal : Goal
    {
        internal const string GoalId = "BiteOrderGoal"; // "Order" en el nombre: el validador de horarios de mods (ModInstance.ValidateScheduleRepositoryMod) se salta los goals de orden; con "BiteGoal" salia en el aviso de cualquier mod que traiga ScheduleModelRepository.json

        /// <summary>
        /// How long the vampire stands over the victim before the blood moves.
        ///
        /// Was 2.5 s. Every second of it is a second the victim can walk out of
        /// - there is no animation holding either of them in place - so it is
        /// now short enough to finish inside one of a wandering trader's pauses
        /// and still long enough to read as an act rather than a touch.
        /// </summary>
        private const float BiteSeconds = 1.4f;

        /// <summary>
        /// Whether blood was actually taken this time round. Read by
        /// <see cref="EndGoalWith"/> to tell "the victim got away" from "the
        /// bite is done", and reset on every start, because the goal object is
        /// pooled and reused for the next order.
        /// </summary>
        private bool drank;

        public BiteGoal(Agent selfAgent) : base(GoalId, selfAgent, GoalInterruptMode.HigherPriority)
        {
            AddInitStep(GameAccess.PreferredReservable(this).GetInitSequenceStep<CreatureBase>(false));
            AddInitStep(new ThreadSequenceStep(TakeOrderedVictim, null, null));
        }

        public override bool AgentTypeCheck()
        {
            var human = AgentOwner as HumanoidInstance;
            return human != null && human.WorkerBehaviour != null;
        }

        public override bool CanStart(bool isForced)
        {
            var biter = AgentOwner as HumanoidInstance;
            if (biter == null || !GameAccess.HasPerk(biter, VampireBite.VampirePerk)) return false;

            return isForced;   // never picked up on its own; only ever ordered
        }

        /// <summary>
        /// The one place that knows the order is over, and the only place that
        /// can tell why.
        ///
        /// GoapAction.Complete ends the goal with Incompletable the moment any
        /// action fails, and for this goal that is nearly always the chase
        /// losing a path to a victim that moved. Nothing downstream hears about
        /// it - no log line, no bubble, the vampire simply goes back to work -
        /// which is what "el boton no hace nada" actually was.
        /// </summary>
        public override void EndGoalWith(GoalCondition condition)
        {
            var biter = AgentOwner as HumanoidInstance;
            var victim = Victim();

            if (MonoSingleton<ReservationManager>.IsInstantiated())
            {
                MonoSingleton<ReservationManager>.Instance.ReleaseAll(AgentOwner);
            }

            base.EndGoalWith(condition);

            if (drank || condition == GoalCondition.Succeeded || condition == GoalCondition.OnGoing)
            {
                return;
            }

            BiteRetry.Failed(biter, victim);
        }

        /// <summary>
        /// Moves the victim the player clicked from the reservation handler
        /// into this goal's target slot. Without a target there is nothing to
        /// walk to, so the goal refuses to start.
        /// </summary>
        private bool TakeOrderedVictim()
        {
            drank = false;

            var handler = GameAccess.PreferredReservable(this);
            if (handler == null || !handler.HasTarget()) return false;

            var target = handler.GetTarget();
            if (!(target.ObjectInstance is CreatureBase)) return false;

            SetTarget(TargetIndex.A, target, false);
            handler.ClearTarget();
            return true;
        }

        protected override IEnumerable<GoapAction> GetNextAction()
        {
            // The reach is read here rather than held in a field because the
            // goal object outlives the order: it is built once per settler and
            // pooled, so a value cached in the constructor would survive a
            // config change that the player made to fix this very thing.
            var reach = GMPlugin.BiteReachDistance?.Value ?? 2.2f;

            yield return GoToActions
                .GoToCreatureTarget(TargetIndex.A, reach)
                .FailIfTargetDisposedForbidenOrNull(TargetIndex.A);

            // Wait() hands back an action already set to Delay for the given
            // number of seconds, which is the only reason this is not a plain
            // `new GoapAction`: both the mode and the duration are read-only
            // from outside the assembly.
            var bite = GeneralActions.Wait(BiteSeconds);
            bite.OnInit = FaceVictim;
            bite.OnComplete = FinishBite;

            yield return bite.FailIfTargetDisposedForbidenOrNull(TargetIndex.A);
        }

        private void FaceVictim()
        {
            var biter = AgentOwner as HumanoidInstance;
            var victim = Victim();
            if (biter == null || victim == null) return;

            biter.FaceObject(victim.GetPosition());
        }

        private void FinishBite(ActionCompletionStatus status)
        {
            if (status != ActionCompletionStatus.Success) return;

            var biter = AgentOwner as HumanoidInstance;

            drank = true;
            BiteRetry.Clear(biter);
            VampireBite.OrderedBite(biter, Victim());
        }

        private CreatureBase Victim()
        {
            return GetTarget(TargetIndex.A).ObjectInstance as CreatureBase;
        }
    }
}
