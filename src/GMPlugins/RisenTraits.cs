using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSEipix.Repository;
using NSMedieval.Manager;
using NSMedieval.Model;
using NSMedieval.Repository;
using NSMedieval.State;
using NSMedieval.Types;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Decides what a walker <em>is</em>, instead of letting the dice decide.
    ///
    /// Nothing in NPCs.json can hand an NPC a perk - the model has no field for
    /// it - so every raider, ours included, gets whatever the generator rolls.
    /// For the horde that was the wrong answer twice over: a walker could come
    /// out Fleet Footed or Brawny and outrun the settlers it is supposed to
    /// shamble after, and it never came out carrying the Risen perk that the
    /// grey skin and the infection both key off.
    ///
    /// So the roll is replaced. Every humanoid the horde spawns is given the
    /// same short list and nothing else, which also means no speed perk can
    /// ever appear on one: they are not banned, they are simply never rolled.
    ///
    /// Three of the perks that were asked for are not here, and the game is the
    /// one refusing them - `conflictsWith` runs both ways, and
    /// PerkRepository.GetAvailablePerks drops anything that clashes with what
    /// the creature already has:
    ///
    ///   - Ravishing ("Arrebatador") is on Disfigured's conflict list.
    ///   - HeatResistant clashes with ColdHardy; a corpse keeping out the cold
    ///     is the half worth having.
    ///   - Vigorous clashes with Robust, and Robust is the better of the two
    ///     here - same healing, plus MotorFunction.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenTraits
    {
        /// <summary>
        /// Slow, cruel, unbothered by corpses and hard to put down. Order
        /// matters only in that Risen comes first, so a half-applied list still
        /// leaves something the rest of the mod recognises.
        /// </summary>
        internal static readonly string[] Traits =
        {
            RisenPallor.RisenPerk,   // ours: slower, tougher, hits harder
            "Laggardly",             // Lento
            "Ruthless",              // Cruel
            "Callous",               // Insensible
            "Cannibal",              // Canibal
            "Bloodlust",             // Sed de sangre
            "Disfigured",            // Desfigurado
            "Gobbler",               // Gloton
            "ColdHardy",             // Resistencia al frio
            "Robust",                // Robusto
            "Strapping",             // Fornido
        };

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(NPCManager), "OnNpcSpawned");
        }

        // El parametro se llama `instance` en el juego. Harmony empareja por
        // nombre, y llamarlo de otra forma no es un aviso: es
        // "FAILED RisenTraits: IL Compile Error" en el arranque y la clase
        // entera sin aplicar.
        private static void Postfix(HumanoidInstance instance)
        {
            if (instance == null) return;

            var blueprint = GameAccess.Id(instance);
            if (blueprint == null || !blueprint.StartsWith(RisenPallor.UndeadBlueprintPrefix)) return;

            ApplyTo(instance);
        }

        /// <summary>
        /// Gives a humanoid the horde's whole character, replacing whatever it
        /// had. Public to the mod because a settler who dies at the claws of a
        /// walker has to end up carrying exactly the same list as one the horde
        /// spawned - there is no such thing as a second-class Risen.
        /// </summary>
        internal static bool ApplyTo(HumanoidInstance instance)
        {
            if (instance == null) return false;

            var perks = new List<Perk>();
            var missing = new List<string>();

            foreach (var id in Traits)
            {
                var perk = Repository<PerkRepository, Perk>.Instance?.GetByID(id);
                if (perk == null) missing.Add(id);
                else perks.Add(perk);
            }

            if (missing.Count > 0)
            {
                GMPlugin.Log?.LogWarning(
                    $"[risen] perk(s) missing from the repository: {string.Join(", ", missing.ToArray())}");
            }

            if (perks.Count == 0) return false;

            // SetPerks, not TryAddNewPerk: this replaces the rolled list rather
            // than adding to it, and it is the call that re-runs
            // StatsBanAndAllowEffectors so the banned effectors take hold too.
            instance.SetPerks(perks);

            GMPlugin.Log?.LogInfo(
                $"[risen] {GameAccess.Name(instance)} rose with {perks.Count} trait(s)");

            return true;
        }
    }
}
