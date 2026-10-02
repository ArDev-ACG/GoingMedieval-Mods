using NSMedieval;
using NSMedieval.AdditionalMenuItems;
using NSMedieval.Controllers;
using NSMedieval.Enums;
using NSMedieval.Goap;
using NSMedieval.State;
using NSMedieval.State.WorkerJobs;
using NSEipix.Base;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// The "Bite" entry on the right-click menu of anything with blood in it.
    ///
    /// Which menu a target shows comes from AdditionalMenuData.json, keyed by
    /// the id the view reports - "worker", "prisoner", "trader", "enemy",
    /// "animal" and so on - so VampireCourt appends this item to those lists in
    /// its own copy of that file. Which of them the player actually sees is
    /// decided here: AdditionalMenuManager skips any item whose Text is empty,
    /// so a constructor that returns without setting Text is an item that never
    /// appears. That is how the option stays invisible for everyone who is not
    /// a vampire, instead of showing up greyed out on every villager.
    ///
    /// Public class, public constructor: the menu is built by
    /// ConstructorInfo.Invoke over AdditionalMenuItemMap.Constuctors, and
    /// Type.GetConstructors() only returns public ones.
    /// </summary>
    public class BiteMenuItem : AdditionalMenuPrioritiseItem
    {
        internal const string ItemId = "BiteMenuItem";

        public BiteMenuItem(IAdditionalMenuOwner owner)
            : base(owner, JobType.None, true, false)
        {
            var victim = owner?.GetAsTarget() as CreatureBase;
            if (victim == null) return;

            var biter = GetSelectedWorker();
            if (biter == null || !GameAccess.HasPerk(biter, VampireBite.VampirePerk)) return;
            if (ReferenceEquals(victim, biter)) return;
            if (!VampireBite.CanBeDrunkFrom(victim, biter)) return;

            Text = Label();
            Tooltip = Explanation();
            EnableIfWorkerIsSelected(false, false);
            DisableIfUnreachableFromSelectedWorker(new IGoapTargetable[] { victim });
        }

        protected override void OnClickCallback()
        {
            base.OnClickCallback();

            // A fresh click is a fresh order, so whatever patience the vampire
            // used up chasing the last victim is given back. Without this, a
            // settler that had just failed twice would get one attempt at the
            // next order and give up.
            BiteRetry.Clear(GetSelectedWorker());

            ForceGoal(BiteGoal.GoalId, Owner?.GetAsTarget() as IReservable, null);
        }

        /// <summary>
        /// The game's localisation files belong to the game; a mod cannot add a
        /// key to them, so the two languages this mod ships text in are decided
        /// here and everything else falls back to English.
        ///
        /// The test is on the language <em>enum</em>, not on the name string:
        /// the name is whatever the language file calls itself, and comparing
        /// against a hardcoded "Spanish" is one localisation rename away from
        /// silently going back to English.
        /// </summary>
        private static bool IsSpanish()
        {
            if (!MonoSingleton<LocalizationController>.IsInstantiated()) return false;

            return MonoSingleton<LocalizationController>.Instance.GetCurrentLanguageEnum()
                   == Language.Spanish;
        }

        private static string Label()
        {
            return IsSpanish() ? "Morder" : "Bite";
        }

        private static string Explanation()
        {
            return IsSpanish()
                ? "El vampiro va hasta la victima y bebe. Solo a un prisionero puede convertirlo en ghoul."
                : "The vampire walks over and drinks. Only a prisoner can be turned into a ghoul.";
        }
    }
}
