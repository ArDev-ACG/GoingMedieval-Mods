using System;
using System.Collections.Generic;
using HarmonyLib;
using NSEipix.Repository;
using NSMedieval.BuildingComponents;
using NSMedieval.UI.Utils;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Puts our furniture's description back in the info panel.
    ///
    /// "Los muebles tienen descripcion - no aparecen." All fourteen carry one,
    /// in English and in Spanish, and the <b>name</b> out of that same entry
    /// shows fine - so whatever is dropping it is dropping only the second
    /// half.
    ///
    /// The path a description takes is four calls long and every one of them
    /// can come back empty without saying anything:
    ///
    ///   BaseBuildingViewComponent.GetDescriptions()
    ///     -> BuildingUtils.GetLocalizedInfo(blueprintId)
    ///        -> BuildingUtils.GetLocalizationInfoKey(id)     // "" when the
    ///           -> BuildingUtils.GetLocKeys(id, out keys)    //    blueprint
    ///           -> LocKeyUtils.GetInfo(keys)                 //    is missing
    ///              -> LocKeyUtils.GetLanguageEntry(keys)     // falls back to
    ///                                                        //    entry [0]
    ///        -> LocalizationController.GetText(thatString)
    ///
    /// Vanilla's entries hold a <em>term</em> there - `building_info_wooden_well`
    /// - and I2.Loc turns it into a sentence. A mod cannot add terms to that
    /// table, so ours hold the sentence itself and lean on GetText handing back
    /// the key it could not translate, which it does for the one-argument
    /// overload. Which of those steps is losing it is not worth another round
    /// of guessing, so this stops guessing: whenever the answer for one of our
    /// buildings comes back empty, or comes back as the id, the description is
    /// read straight off the blueprint and returned.
    ///
    /// It also says so once per building, so the next log names the step that
    /// was failing instead of leaving it to a screenshot.
    /// </summary>
    [HarmonyPatch(typeof(BuildingUtils), nameof(BuildingUtils.GetLocalizedInfo))]
    internal static class ModBuildingInfo
    {
        /// <summary>Buildings already reported, so the log says it once.</summary>
        private static readonly HashSet<string> Told = new HashSet<string>();

        private static void Postfix(string buildableBaseId, ref string __result)
        {
            if (string.IsNullOrEmpty(buildableBaseId)) return;
            if (!Ours.Contains(buildableBaseId)) return;

            // Anything that already reads as a sentence is left alone. The two
            // failures worth catching are an empty string and the id coming
            // back at us, which is what a missing translation looks like.
            if (!string.IsNullOrWhiteSpace(__result) && __result != buildableBaseId) return;

            var info = Info(buildableBaseId);
            if (string.IsNullOrWhiteSpace(info)) return;

            __result = info;

            if (Told.Add(buildableBaseId))
            {
                GMPlugin.Log?.LogInfo(
                    $"[info] '{buildableBaseId}' came back with no description - "
                    + "read it off the blueprint instead");
            }
        }

        /// <summary>
        /// Our building ids. They are not prefixed - they were named before the
        /// prefix rule existed, and renaming them would orphan every one
        /// already standing in a save - so the list is written out.
        /// </summary>
        private static readonly HashSet<string> Ours = new HashSet<string>
        {
            "blood_altar", "blood_ritual_circle", "count_coffin", "court_banner",
            "court_banner_wall", "count_throne", "crimson_candle", "blood_brazier",
            "veiled_mirror", "court_reliquary", "count_crypt", "blood_well",
            "vigil_table", "impaled_stake", "cat_statue", "mass_grave",
        };

        /// <summary>
        /// The description as the blueprint holds it: in the player's language
        /// when there is an entry for it, and in whatever entry has one when
        /// there is not.
        /// </summary>
        private static string Info(string id)
        {
            try
            {
                var blueprint = Repository<BaseBuildingRepository, BaseBuildingBlueprint>
                    .Instance?.GetByID(id);

                var keys = blueprint?.LocKeys;
                if (keys == null || keys.Length == 0) return null;

                var text = LocKeyUtils.GetInfo(keys);
                if (!string.IsNullOrWhiteSpace(text)) return text;

                // GetLanguageEntry picked an entry with no info in it. Take the
                // first one that has any.
                foreach (var entry in keys)
                {
                    if (!string.IsNullOrWhiteSpace(entry?.Info)) return entry.Info;
                }
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[info] could not read the description of '{id}': {e}");
            }

            return null;
        }
    }
}
