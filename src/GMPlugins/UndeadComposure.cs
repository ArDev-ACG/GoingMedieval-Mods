using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSMedieval.State;
using NSMedieval.StatsSystem;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// One answer to "who is dead", and the two things that follow from it.
    ///
    /// The mod had three separate tests for this - <see cref="RisenPallor"/>
    /// looked at the blueprint prefix, the faction and the Risen perk; the sun
    /// looked at Vampire and Ghoul; the bite looked at Ghoul and Vampire again
    /// - and each of them meant a slightly different thing by it. That is
    /// tolerable while each rule only has to answer for itself, and stops being
    /// tolerable the moment a rule says "this does not happen to the dead",
    /// because then half the court would faint and the other half would not.
    /// </summary>
    internal static class Undead
    {
        internal const string VampirePerk = "Vampire";
        internal const string GhoulPerk = "Ghoul";
        internal const string RisenPerk = "Risen";

        /// <summary>Anything of ours that is walking around without being alive.</summary>
        internal static bool Is(CreatureBase creature)
        {
            var human = creature as HumanoidInstance;
            if (human == null) return false;

            return GameAccess.HasPerk(human, VampirePerk)
                   || GameAccess.HasPerk(human, GhoulPerk)
                   || Census.IsUndead(human);
        }
    }

    /// <summary>
    /// The dead do not pass out.
    ///
    /// Fainting is the game's only word for "out of the fight but alive", and
    /// <c>CreatureBase.OnStatEffectorEvent</c> reaches for it the moment
    /// Consciousness bottoms out. That is exactly what this mod spends its time
    /// pushing down: <c>VampireThirstRavenous</c> halves ConsciousnessMax and
    /// drains 8 to 14 a tick on top, so a vampire that goes a day without blood
    /// ends up face down in the yard - which reads as the mod breaking the
    /// settler rather than as a vampire going hungry. Sunburn pain did the same
    /// from the other direction.
    ///
    /// Both halves of the call are blocked, and they are two different methods
    /// for a reason that has already cost a round of testing:
    /// <c>HumanoidInstance.Faint()</c> calls the base and then
    /// <c>HandleOnFaint()</c>, so stopping only the base leaves a creature that
    /// is "unconscious" and still standing up.
    /// </summary>
    [HarmonyPatch]
    internal static class UndeadNeverFaints
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(CreatureBase), "Faint");
        }

        private static bool Prefix(CreatureBase __instance)
        {
            return !Refuse(__instance);
        }

        /// <summary>
        /// Whether this creature is one of ours, and says so once per creature
        /// rather than on every tick that tries to drop it.
        /// </summary>
        internal static bool Refuse(CreatureBase creature)
        {
            if (!(GMPlugin.UndeadNeverFaint?.Value ?? true)) return false;
            if (!Undead.Is(creature)) return false;

            if (Told.Add(creature.UniqueId))
            {
                GMPlugin.Log?.LogInfo(
                    $"[composure] {GameAccess.Name(creature)} does not pass out - the dead have no use for it");
            }

            return true;
        }

        private static readonly HashSet<int> Told = new HashSet<int>();
    }

    /// <summary>
    /// The other half of the same method, for the same reason.
    /// </summary>
    [HarmonyPatch]
    internal static class UndeadNeverFaintsHumanoid
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(HumanoidInstance), "Faint");
        }

        private static bool Prefix(HumanoidInstance __instance)
        {
            return !UndeadNeverFaints.Refuse(__instance);
        }
    }

    /// <summary>
    /// A corpse is not news to something that is one.
    ///
    /// Every one of these is a vanilla mood entry about dead bodies - seeing
    /// one, butchering one, cooking one, eating one, wearing what came off one.
    /// They are the right rules for a farmer and they are nonsense on a vampire
    /// that feeds on people and a ghoul that eats them, and in play they were
    /// the reason a court with a mass grave in it lived at permanent breaking
    /// point while doing exactly what the mod is about.
    ///
    /// The block is at <c>StartEffector</c> rather than in the entries
    /// themselves: the ids belong to vanilla, a mod that rewrote them would
    /// change them for the living too, and the cannibal perk already proves the
    /// game expects some creatures to be exempt.
    ///
    /// Refusing to start it is reported back as <c>false</c>, which is the same
    /// answer the game gives for an effector a creature is not allowed to have,
    /// so nothing downstream has to learn a new case.
    /// </summary>
    [HarmonyPatch]
    internal static class UndeadUnmoved
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(StatsInstance), "StartEffector");
        }

        /// <summary>
        /// The vanilla mood entries a body puts on whoever is looking at it,
        /// cutting it up, cooking it, eating it or wearing it.
        /// </summary>
        private static readonly HashSet<string> AboutTheDead = new HashSet<string>
        {
            "SawDeadBody",
            "SawDeadBodyFriend",
            "ButcheredHuman",
            "ButcheredHumanFriend",
            "CookedHumanMeat",
            "AteHumanFlesh",
            "AteHumanFleshCooked",
            "AteRotten",
            "AteRawMeat",
            "MadeSomethigOutOfHumanLeather",
            "WearingDeadManItem",
            "WearingHumanSkinItem",
            "BeingAroundHumanLeather",
            "BeingAroundHumanLeatherItems",
            "AffectionSawCannibalButcheringNegative",
            "AffectionSawCannibalEatingNegative",

            // The disgust has a second half that is not a mood entry at all.
            // "ni les dan asco los cuerpos" was read the first time as the
            // sixteen thoughts above, and those are only what the settler
            // thinks; being sick is what the settler does. The chain is three
            // effectors - the animation, the aftermath and the stop - and a
            // ghoul doubled over in the yard after eating what it was made to
            // eat is the same complaint in a different coat.
            "VomitAnim",
            "VomitAfter",
            "VomitStop",
        };

        private static bool Prefix(StatsInstance __instance, string effectorId, ref bool __result)
        {
            if (!(GMPlugin.UndeadIgnoreCorpses?.Value ?? true)) return true;
            if (effectorId == null || !AboutTheDead.Contains(effectorId)) return true;

            var owner = GameAccess.StatsOwner(__instance);
            if (!Undead.Is(owner)) return true;

            // Said once per creature and effector, and no more. A rule that
            // refuses in silence reads in a log exactly like a rule that never
            // ran, and this one had already been given up for broken once; the
            // dedup is what keeps a court standing over a mass grave from
            // writing a thousand identical lines.
            if (Told.Add(owner.UniqueId + "/" + effectorId))
            {
                GMPlugin.Log?.LogInfo(
                    $"[composure] {GameAccess.Name(owner)} is not troubled by '{effectorId}'");
            }

            __result = false;
            return false;
        }

        private static readonly HashSet<string> Told = new HashSet<string>();
    }
}
