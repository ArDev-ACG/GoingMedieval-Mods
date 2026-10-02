using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSEipix.Base;
using NSMedieval;
using NSMedieval.AdditionalMenuItems;
using NSMedieval.BuildingComponents;
using NSMedieval.Controllers;
using NSMedieval.Draft;
using NSMedieval.Enums;
using NSMedieval.Goap;
using NSMedieval.Goap.Goals;
using NSMedieval.Model;
using NSMedieval.State;
using NSMedieval.State.WorkerJobs;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Drinking the flask, and the flask counting as a meal.
    ///
    /// <b>What was reported.</b> "el frasco de sangre no se puede tomar". The
    /// brew was produced, it stacked, it looked like a flask - and there was no
    /// way for the player to hand one to a vampire. The only thing that ever
    /// drank one was <see cref="VampireThirst"/>'s own shortcut, which takes a
    /// bottle out of the stock without anybody walking anywhere; the log of the
    /// 14th has exactly one line of it, at the end of eight bites.
    ///
    /// <b>Two halves, because two things were missing.</b>
    ///
    ///   - <see cref="BloodDraughtMenuItem"/> is the order: "Beber brebaje de
    ///     sangre" on the right-click menu of a pile of it, for a selected
    ///     vampire or ghoul. It does not invent a goal - it hands the game the
    ///     same <c>DraftOrderConsume</c> that vanilla's own "Drink" item hands
    ///     it, which sets <c>ForceEatPile</c> and forces <c>DrinkGoal</c>, so
    ///     the settler walks to the pile and drinks with the game's animation
    ///     and the game's bookkeeping.
    ///   - <see cref="BloodDraughtDrunk"/> is the consequence. A flask drunk
    ///     through the game's own goal fires the resource's
    ///     <c>onUseEffects</c> and nothing else: the thirst clock, which lives
    ///     in this mod and not in a stat, never heard about it. So the meal was
    ///     invisible to the very need it was brewed for. This is the postfix
    ///     that tells the thirst, and it covers every route at once - the order
    ///     below, vanilla's "Drink", and a vampire that drinks on its own
    ///     because the stimulant need told it to.
    /// </summary>
    internal static class BloodDraught
    {
        /// <summary>
        /// Whether this is one of ours, and therefore whether blood is food.
        /// Ghouls too: they are made by the bite and live off the same thing.
        /// </summary>
        internal static bool DrinksBlood(HumanoidInstance human)
        {
            return human != null
                   && (GameAccess.HasPerk(human, VampireBite.VampirePerk)
                       || GameAccess.HasPerk(human, VampireBite.GhoulPerk));
        }

        /// <summary>
        /// Who a goal belongs to, by reflection over the property rather than a
        /// direct call: the getter is on <c>Goal</c> and a rename there would be
        /// a compile error in four files instead of one null here.
        /// </summary>
        private static readonly MethodInfo OwnerGetter =
            AccessTools.PropertyGetter(typeof(Goal), "AgentOwner");

        internal static HumanoidInstance Drinker(Goal goal)
        {
            if (goal == null || OwnerGetter == null) return null;

            return OwnerGetter.Invoke(goal, null) as HumanoidInstance;
        }

        /// <summary>
        /// Called from both consumption postfixes: one flask down, so the
        /// thirst resets the same way a bite resets it.
        /// </summary>
        internal static void Swallowed(Goal goal, ResourceInstance consumed)
        {
            if (consumed == null) return;
            if (consumed.BlueprintId != VampireThirst.BloodDraughtId) return;

            var human = Drinker(goal);
            if (!DrinksBlood(human)) return;

            GMPlugin.Log?.LogInfo(
                $"[draught] {GameAccess.Name(human)} drank a flask - the thirst resets");

            VampireThirst.Fed(human);
        }
    }

    /// <summary>
    /// The thirst hears about a flask that was actually drunk.
    ///
    /// Two patches and not one: <c>DrinkGoal</c> overrides
    /// <c>OnConsumedResource</c>, and Harmony patches a method body, not a
    /// slot - a virtual call that lands in the override never runs a patch on
    /// the base. <see cref="BloodDraught.Swallowed"/> resets a clock to the
    /// same number twice, so an override that also calls its base is not a
    /// double meal.
    /// </summary>
    [HarmonyPatch]
    internal static class BloodDraughtDrunk
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(DrinkGoal), "OnConsumedResource");
        }

        private static void Postfix(DrinkGoal __instance, ResourceInstance resourceInstance)
        {
            try
            {
                BloodDraught.Swallowed(__instance, resourceInstance);
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[draught] could not count a drunk flask: {e}");
            }
        }
    }

    /// <summary>
    /// The same, for the eating goal. A flask is a stimulant, and the hunger
    /// goal will pick one up if that is what the diet allows.
    /// </summary>
    [HarmonyPatch]
    internal static class BloodDraughtEaten
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(HungerGoal), "OnConsumedResource");
        }

        private static void Postfix(HungerGoal __instance, ResourceInstance resourceInstance)
        {
            try
            {
                BloodDraught.Swallowed(__instance, resourceInstance);
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[draught] could not count an eaten flask: {e}");
            }
        }
    }

    /// <summary>
    /// "Beber brebaje de sangre" on a pile of the stuff.
    ///
    /// Public class, public constructor, for the same reason
    /// <see cref="BiteMenuItem"/> is: the menu is built by
    /// <c>ConstructorInfo.Invoke</c> and <c>Type.GetConstructors()</c> only
    /// returns the public ones. And like that one, it stays invisible to
    /// anybody who is not a vampire by leaving <c>Text</c> empty -
    /// <c>AdditionalMenuManager.ShowMenu</c> skips an item with no text, which
    /// is better than a greyed-out line on every villager's crate of beer.
    /// </summary>
    public class BloodDraughtMenuItem : AdditionalMenuItemBase
    {
        internal const string ItemId = "BloodDraughtMenuItem";

        public BloodDraughtMenuItem(IAdditionalMenuOwner owner)
            : base(owner, JobType.None, false)
        {
            var pile = PileOf(owner);
            if (pile == null) return;

            var drinker = GetSelectedWorker();
            if (!BloodDraught.DrinksBlood(drinker)) return;

            Text = IsSpanish() ? "Beber brebaje de sangre" : "Drink blood draught";
            Tooltip = IsSpanish()
                ? "El vampiro va hasta el monton, destapa un frasco y bebe. Calma la sed igual que un mordisco."
                : "The vampire walks to the pile, unstoppers a flask and drinks. It answers the thirst the way a bite does.";

            EnableIfWorkerIsSelected(false, false);
            // A stored flask is reached through its shelf, not on its own tile.
            var reachTarget = owner.GetAsTarget() ?? pile;
            DisableIfUnreachableFromSelectedWorker(new IGoapTargetable[] { reachTarget });
        }

        /// <summary>
        /// The flask this menu is about. A loose pile answers for itself; a
        /// shelf or chest answers with the first flask stored in it, or with
        /// nothing - so the order never shows on storage that holds none.
        /// Inside storage the click selects the building, whose menu is
        /// <c>storageBuilding</c>, and that is why it was missing there.
        /// </summary>
        private static ResourcePileInstance PileOf(IAdditionalMenuOwner owner)
        {
            var target = owner?.GetAsTarget();

            if (target is ResourcePileInstance loose)
            {
                return !loose.HasDisposed && loose.BlueprintId == VampireThirst.BloodDraughtId
                    ? loose
                    : null;
            }

            if (!(target is BaseBuildingInstance building) || building.HasDisposed) return null;

            var shelf = building.GetComponentInstance<ShelfComponentInstance>();
            if (shelf == null || shelf.HasDisposed) return null;

            foreach (var stored in shelf.GetStoredPiles())
            {
                if (stored != null && !stored.HasDisposed
                    && stored.BlueprintId == VampireThirst.BloodDraughtId)
                {
                    return stored;
                }
            }

            return null;
        }

        protected override void OnClickCallback()
        {
            base.OnClickCallback();

            var pile = PileOf(Owner);
            var drinker = GetSelectedWorker();
            if (pile == null || drinker == null) return;
            if (!MonoSingleton<DraftController>.IsInstantiated()) return;

            // The game's own order, not one of ours: this is the identical call
            // PileForceDrinkMenuItem makes, so the walking, the animation and
            // the stack bookkeeping are all vanilla's.
            MonoSingleton<DraftController>.Instance.ExecuteDraftOrder(
                drinker, new DraftOrderConsume(pile, true));

            GMPlugin.Log?.LogInfo(
                $"[draught] {GameAccess.Name(drinker)} was sent to drink from a pile of "
                + VampireThirst.BloodDraughtId);
        }

        private static bool IsSpanish()
        {
            if (!MonoSingleton<LocalizationController>.IsInstantiated()) return false;

            return MonoSingleton<LocalizationController>.Instance.GetCurrentLanguageEnum()
                   == Language.Spanish;
        }
    }

    /// <summary>
    /// Registers the item so the menu map can build it, the same way
    /// <c>BiteMenuRegistration</c> does.
    /// </summary>
    [HarmonyPatch]
    internal static class BloodDraughtMenuRegistration
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(AdditionalMenuItemMap), "Constuctors");
        }

        private static void Postfix(Dictionary<string, ConstructorInfo> __result)
        {
            if (__result == null || __result.ContainsKey(BloodDraughtMenuItem.ItemId)) return;

            __result[BloodDraughtMenuItem.ItemId] =
                typeof(BloodDraughtMenuItem).GetConstructors()[0];

            GMPlugin.Log?.LogInfo(
                "[draught] BloodDraughtMenuItem registered in AdditionalMenuItemMap");
        }
    }

    /// <summary>
    /// Puts it into the pile menus.
    ///
    /// <c>ResourcePileView.GetAdditionalMenuId</c> answers with one of a short
    /// list, picked from the resource's category: a flask carries
    /// <c>CtgAlcohol</c> and <c>CtgDestilMat</c>, which lands it in
    /// <c>resourcePileAlcohol</c>. <c>storageBuilding</c> is the menu of a shelf
    /// or chest, which is what the click selects once the flask is stored.
    /// <c>resourcePileEdible</c> is here too
    /// because the category of a brew is a decision that has been changed once
    /// already, and an item that quietly stops appearing is worse than one
    /// listed in a menu it never matches - the constructor checks the blueprint
    /// either way.
    /// </summary>
    [HarmonyPatch]
    internal static class BloodDraughtMenuInjection
    {
        private static readonly HashSet<string> PileMenus = new HashSet<string>
        {
            "resourcePileAlcohol", "resourcePileEdible", "storageBuilding",
        };

        private static readonly Dictionary<string, string[]> Extended =
            new Dictionary<string, string[]>();

        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(AdditionalMenuItemData), "MenuItems");
        }

        private static void Postfix(AdditionalMenuItemData __instance, ref string[] __result)
        {
            if (__instance == null || __result == null) return;

            var id = __instance.GetID();
            if (id == null || !PileMenus.Contains(id)) return;
            if (Array.IndexOf(__result, BloodDraughtMenuItem.ItemId) >= 0) return;

            if (!Extended.TryGetValue(id, out var extended))
            {
                extended = new string[__result.Length + 1];
                Array.Copy(__result, extended, __result.Length);
                extended[__result.Length] = BloodDraughtMenuItem.ItemId;
                Extended[id] = extended;

                GMPlugin.Log?.LogInfo(
                    $"[draught] menu '{id}' now offers {BloodDraughtMenuItem.ItemId}");
            }

            __result = extended;
        }
    }
}
