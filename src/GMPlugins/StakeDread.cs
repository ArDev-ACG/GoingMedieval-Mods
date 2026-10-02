using System.Collections.Generic;
using NSMedieval;
using NSMedieval.BuildingComponents;
using NSMedieval.State;
using NSMedieval.Types;
using NSMedieval.Village;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// The body on the stake is for whoever is coming up the road.
    ///
    /// <b>It lives in Vampire Court now.</b> It shipped in Carrion and Plague
    /// because that is where the corpse handling was, and that was the wrong
    /// shelf: a settlement that pikes its dead at the gate is a court making a
    /// point, not a village with a rat problem. The building, the effector, the
    /// icon and the texture all moved on 7 sep; only the ids stayed, so any
    /// stake already standing in a save survives the move.
    ///
    /// A settlement that puts its dead on pikes is saying something, and the
    /// only people it is saying it to are the ones outside the wall. So the
    /// stake is decoration by every rule the game has - it takes a tile, it has
    /// hit points, nobody works at it - and the one thing it does is felt only
    /// by creatures hostile to the settlement.
    ///
    /// <b>Why a sweep of our own and not the game's proximity system.</b> The
    /// same wall <see cref="CountAura"/> ran into: <c>CreatureBase
    /// .TryInitProximitySpheres</c> builds one static sphere of radius 6 shared
    /// by every creature alive, so a mod cannot give one building its own reach
    /// without giving it to rotting wounds and messy eaters too.
    ///
    /// <b>Why attributes and not mood.</b> A raider has no Happiness panel -
    /// <c>SwapStatsModel</c> puts enemies on a stats model that does not carry
    /// one - so "lowers their morale" has to be spent on something an enemy
    /// actually has. The effector does both: a MoodModify for anything that
    /// turns out to have a mood, and multipliers on the combat attributes that
    /// make a frightened man worse at his job.
    /// </summary>
    internal static class StakeDread
    {
        /// <summary>The building, and the effector it puts on them.</summary>
        internal const string StakeId = "impaled_stake";

        private const string DreadEffectorId = "StakeDread";

        /// <summary>
        /// How often the sweep runs, in real seconds. Slower than the horde
        /// watchdog because nothing here is urgent: a raider that walks into
        /// the dread a few seconds late has still walked into it.
        /// </summary>
        private const float SweepSeconds = 5f;

        internal static void Start()
        {
            GMPlugin.Every("stake dread sweep", SweepSeconds, Sweep);
        }

        /// <summary>
        /// What the last sweep counted, so a change is said once instead of
        /// every five seconds. -1 is "nothing counted yet", which is what makes
        /// the first sweep always speak.
        /// </summary>
        private static int lastStakes = -1;

        private static void Sweep()
        {
            var radius = GMPlugin.StakeDreadRadius?.Value ?? 0;
            if (radius <= 0) return;

            // The hostiles first, and out on an empty list: a village at peace
            // is the common case, and this way it never touches the map.
            var afraid = Hostiles();
            if (afraid.Count == 0) return;

            var stakes = Stakes();
            var reach = radius * radius;

            // The one number that says whether this is a rule that does not
            // work or a rule with nothing to work on. "Se construyo un empalado
            // y no pasa nada" and "el barrido no encuentra el empalado" read
            // identically in a log without it.
            if (stakes.Count != lastStakes)
            {
                lastStakes = stakes.Count;
                GMPlugin.Log?.LogInfo(
                    $"[stake] {stakes.Count} pike(s) standing, {afraid.Count} hostile(s) on the map");
            }

            foreach (var enemy in afraid)
            {
                // Uno por uno y cada uno en su jaula. En el log del 22 el
                // barrido entero se caia con
                // `[beat] stake dread sweep failed: NullReferenceException`
                // dos veces por partida, y un barrido caido es que los anillos
                // del empalado no hacen nada para **nadie**: basta con que un
                // solo cuerpo de la lista este a medio desechar - el aviso
                // `[access] could not read the map` sale una linea antes, asi
                // que es el momento de la carga - para llevarse por delante a
                // los demas. Ahora ese cuerpo se salta y el resto sigue.
                try
                {
                    var inReach = stakes.Count > 0 && Near(enemy, stakes, reach);

                    Sync(enemy, inReach);
                    if (inReach) Dishearten(enemy);
                }
                catch (System.Exception e)
                {
                    if (!saidFailure)
                    {
                        saidFailure = true;
                        GMPlugin.Log?.LogError(
                            $"[stake] skipped {GameAccess.Name(enemy)}, and says it once: {e}");
                    }
                }
            }
        }

        /// <summary>El primer cuerpo que se salta se cuenta; los demas, no.</summary>
        private static bool saidFailure;

        /// <summary>
        /// Takes the morale down by hand, because the effector's own
        /// <c>MoodModify</c> does not.
        ///
        /// "penaliza pero no baja la moral." MoodModify is not a write to the
        /// Mood stat: it is a <em>thought</em>, an entry in the list a settler's
        /// happiness is totalled from, and that list is built by the settler
        /// side of the game. An enemy has the Mood stat - it is right there in
        /// the `enemy` entry of StatsModelRepository - but nothing on that side
        /// ever totals thoughts into it, so the modifier is filed and never
        /// applied. The number in the JSON was correct and had nobody to read
        /// it.
        ///
        /// So the stat is written directly, every sweep, with a floor: the aim
        /// is a raider who fights worse, not one paralysed by the sight of a
        /// corpse, and the floor is what stops a minute of standing there from
        /// emptying the bar.
        /// </summary>
        private static void Dishearten(HumanoidInstance enemy)
        {
            var perSweep = GMPlugin.StakeMoodDrain?.Value ?? 0.06f;
            var floor = GMPlugin.StakeMoodFloor?.Value ?? 0.25f;

            if (perSweep <= 0f) return;

            var stats = GameAccess.Stats(enemy);
            var mood = stats?.GetStat(NSMedieval.StatsSystem.StatType.Mood);
            if (mood == null) return;

            if (mood.Max <= 0f || mood.Current <= mood.Max * floor) return;

            GameAccess.DrainStat(enemy, NSMedieval.StatsSystem.StatType.Mood, perSweep);
        }

        /// <summary>
        /// Everything on the map that is hostile and upright.
        ///
        /// The village's own NPC list is where raids, wanderers and the horde
        /// all end up - but not every walker. A settler who turned mid-raid
        /// only joins that list when <c>RisenRoster.Adopt</c> could give it the
        /// horde's NPC blueprint; when it could not, the body is hostile,
        /// standing, and in no list at all. Those are exactly the creatures the
        /// stake is aimed at, so the watchdog's own roster of them is folded in
        /// here.
        /// </summary>
        private static List<HumanoidInstance> Hostiles()
        {
            var found = new List<HumanoidInstance>();

            var npcs = GlobalSaveController.CurrentVillageData?.NPCs;
            if (npcs != null)
            {
                for (var i = 0; i < npcs.Count; i++) Consider(found, npcs[i]);
            }

            foreach (var turned in Census.Turned)
            {
                if (npcs != null && npcs.Contains(turned)) continue;   // counted once
                Consider(found, turned);
            }

            return found;
        }

        private static void Consider(List<HumanoidInstance> found, HumanoidInstance who)
        {
            // `ActiveBehaviour` de un cuerpo a medio desechar tira, y tiraba
            // dentro del bucle que arma la lista: la otra mitad del barrido
            // caido del 22.
            try
            {
                if (who == null || who.HasDisposed || who.HasDiedOrFainted) return;
                if (!(who.ActiveBehaviour is EnemyBehaviour)) return;

                found.Add(who);
            }
            catch (System.Exception)
            {
                // Un cuerpo que no se deja preguntar no esta en el mapa.
            }
        }

        /// <summary>
        /// Every impaled body standing on the map. Blueprints and half-built
        /// ones are in none of these grids, so a stake only frightens anyone
        /// once it is actually up.
        ///
        /// <b>Which grid it lands in is not ours to guess.</b> This asked for
        /// <c>Furniture</c> alone, because a stake is furniture in the sense a
        /// player means it. The game files buildings by <c>buildingType</c>,
        /// and <c>impaled_stake</c> is 32768 - Decoration - copied from the
        /// scarecrow whose prefab it borrows. One wrong grid is a sweep that
        /// runs perfectly and finds nothing, forever, with nothing in the log
        /// to say so. The mask is the same one <see cref="RisenTargetBuildings"/>
        /// uses for "everything the player put down", and asking for five grids
        /// instead of one costs a list walk every five seconds.
        /// </summary>
        private const GridDataType BuiltThings =
            GridDataType.BuildingFinished | GridDataType.Furniture
            | GridDataType.ProductionBuilding | GridDataType.FurnitureGate
            | GridDataType.Drawbridge;

        private static List<Vec3Int> Stakes()
        {
            var found = new List<Vec3Int>();

            var objects = GameAccess.WorldObjects(BuiltThings);

            foreach (var obj in objects)
            {
                var building = obj as BaseBuildingInstance;
                if (building?.Blueprint == null) continue;
                if (building.Blueprint.GetID() != StakeId) continue;

                found.Add(building.GetGridPosition());
            }

            return found;
        }

        private static bool Near(CreatureBase enemy, List<Vec3Int> stakes, int reach)
        {
            var here = enemy.GetGridPosition();

            foreach (var stake in stakes)
            {
                var dx = here.x - stake.x;
                var dy = here.y - stake.y;
                var dz = here.z - stake.z;

                if (dx * dx + dy * dy + dz * dz <= reach) return true;
            }

            return false;
        }

        /// <summary>
        /// Leaves the enemy carrying the dread exactly while it is in sight of
        /// a stake, and not a moment after it has walked out of range - which
        /// is also what takes it off everyone when the last stake comes down.
        /// </summary>
        private static void Sync(HumanoidInstance enemy, bool inReach)
        {
            var stats = GameAccess.Stats(enemy);
            if (stats == null) return;

            var active = stats.IsEffectorActive(DreadEffectorId);

            if (inReach == active) return;

            if (!inReach)
            {
                stats.EndEffector(DreadEffectorId);
                return;
            }

            if (stats.StartEffector(DreadEffectorId, 1f, false, -1, null))
            {
                GMPlugin.Log?.LogInfo($"[stake] {GameAccess.Name(enemy)} can see what is on the pike");
            }
            else
            {
                GMPlugin.Log?.LogWarning(
                    $"[stake] '{DreadEffectorId}' refused on {GameAccess.Name(enemy)} "
                    + "- missing from Effectors.json, or banned on that creature");
            }
        }
    }
}
