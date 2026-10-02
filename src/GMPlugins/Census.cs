using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSMedieval.CombatAi;
using NSMedieval.Manager;
using NSMedieval.State;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Lo que varios mods necesitan saber del mapa sin depender unos de otros:
    /// quien hay, quien es un alzado y la lista de colonos alzados. Cada DLL
    /// lleva su copia; la lista de alzados solo la llena la de Undead Horde.
    /// </summary>
    internal static class Census
    {
        internal const string RisenPerk = "Risen";
        internal const string UndeadFactionId = "undead_horde";
        internal const string UndeadBlueprintPrefix = "undead_horde_";

        /// <summary>Los colonos que se han levantado; ver RisenRestless.</summary>
        internal static readonly List<HumanoidInstance> Turned = new List<HumanoidInstance>();

        /// <summary>
        /// Todo lo vivo que el juego tiene en alguna lista: los colonos, los NPC
        /// de la partida y los animales.
        /// </summary>
        internal static IEnumerable<CreatureBase> Everything()
        {
            if (NSEipix.Base.MonoSingleton<WorkerManager>.IsInstantiated())
            {
                foreach (var settler in WorkerManager.WorkersHere) yield return settler;
            }

            var npcs = NSMedieval.GlobalSaveController.CurrentVillageData?.NPCs;
            if (npcs != null)
            {
                for (var i = 0; i < npcs.Count; i++) yield return npcs[i];
            }

            // El censo de animales guarda **vistas**, no instancias: la
            // criatura vive colgada de cada una en `AnimalInstance`.
            if (NSEipix.Base.MonoSingleton<AnimalManager>.IsInstantiated())
            {
                var animals = NSEipix.Base.MonoSingleton<AnimalManager>.Instance?.Animals;
                if (animals != null)
                {
                    foreach (var pair in animals)
                    {
                        var view = pair.Value;
                        if (view != null && view.AnimalInstance != null) yield return view.AnimalInstance;
                    }
                }
            }
        }

        /// <summary>
        /// Si este cuerpo es de los alzados.
        ///
        /// <b>Estar en la faccion no basta, y costo una ronda de pruebas.</b>
        /// La partida del 17 tiene tres lineas seguidas de `[risen] David
        /// Wright#115 va a por ...` y David Wright no era un alzado: su
        /// blueprint es `non_partisan_nomads_heavy_easy`, un hombre vivo y
        /// armado de la categoria `general`. Entro en la horda porque el juego
        /// reparte enemigos por **categoria y tipo**, y las diecisiete
        /// categorias de vanilla declaran las tres - `archer`, `basic` y
        /// `heavy` - mientras que `undead_horde` solo declara `basic`: cuando
        /// algo pide un pesado a la horda, la horda no tiene ninguno y el pool
        /// `general` no le prohibe a nadie nada (`allowedFactionTypes` vacio en
        /// las veinticinco entradas). El resultado es un saqueador con el
        /// `factionId` de los alzados.
        ///
        /// Y con este metodo diciendo que si por la faccion, ese saqueador
        /// pasaba a ser uno de ellos por partida doble: `RisenHunt` le daba
        /// ordenes de caza y `Worth` lo sacaba de la lista de presas. De ahi
        /// "los alzados no atacan a los saqueadores" - no es que no quisieran,
        /// es que el saqueador era de los suyos.
        ///
        /// Las dos primeras reglas siguen enteras porque cada una cubre un caso
        /// real y ninguna se puede confundir: el blueprint es lo que sale de la
        /// horda, y el perk es lo que lleva un colono que se levanta. La
        /// tercera se queda para lo que no es ni una cosa ni la otra, y pide
        /// ademas que el cuerpo sea de la horda de verdad.
        /// </summary>
        internal static bool IsUndead(HumanoidInstance humanoid)
        {
            if (humanoid == null) return false;

            var blueprint = GameAccess.Id(humanoid);
            if (blueprint != null && blueprint.StartsWith(UndeadBlueprintPrefix)) return true;
            if (GameAccess.HasPerk(humanoid, RisenPerk)) return true;

            return GameAccess.FactionId(humanoid) == UndeadFactionId
                   && GameAccess.NpcCategory(blueprint) == UndeadFactionId;
        }
    }

    /// <summary>
    /// <c>CombatAiAgent.IsStateSet</c> is not reachable from outside the game
    /// assembly, and it is what keeps the two goals that read it a last resort,
    /// so it is read reflectively rather than assumed. A lookup that fails
    /// answers "the state is set", which leaves a goal refused - the safe
    /// direction when the check itself is broken.
    /// </summary>
    internal static class RisenGoalState
    {
        private static readonly MethodInfo Getter =
            AccessTools.Method(typeof(CombatAiAgent), "IsStateSet");

        private static readonly MethodInfo Setter =
            AccessTools.Method(typeof(CombatAiAgent), "SetState",
                new[] { typeof(CombatAiState), typeof(object) });

        internal static bool IsSet(CombatAiAgent ai, CombatAiState state)
        {
            if (Getter == null || ai == null) return true;

            try
            {
                return Getter.Invoke(ai, new object[] { state }) as bool? ?? true;
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[raze] could not read combat state {state}: {e.Message}");
                return true;
            }
        }

        /// <summary>Writes one combat state, or says why it could not.</summary>
        internal static void Set(CombatAiAgent ai, CombatAiState state, object value)
        {
            if (Setter == null || ai == null) return;

            try
            {
                Setter.Invoke(ai, new[] { state, value });
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[raze] could not write combat state {state}: {e.Message}");
            }
        }
    }
}
