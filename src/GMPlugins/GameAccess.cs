using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using NSMedieval.BuildingComponents;
using NSMedieval.Modding;
using NSMedieval.State;
using NSMedieval.StatsSystem;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Reflected access to the handful of game members the patches need but
    /// cannot see.
    ///
    /// A creature's perks, stats and id are all serialized state, so the game
    /// declares them as plain non-public fields; the same goes for the mod
    /// list. Harmony's AccessTools builds a typed delegate for each of them
    /// once, at class-load time, which is far cheaper than reaching for
    /// reflection on every combat hit.
    ///
    /// Every accessor here returns null rather than throwing when the member
    /// moves in a game update, so a rename degrades into a patch that quietly
    /// does nothing instead of an exception on every frame.
    /// </summary>
    internal static class GameAccess
    {
        private static readonly AccessTools.FieldRef<HumanoidInstance, List<string>> PerkIdsRef =
            SafeField<HumanoidInstance, List<string>>("perkIds");

        private static readonly AccessTools.FieldRef<HumanoidInstance, string> FactionIdRef =
            SafeField<HumanoidInstance, string>("factionId");

        private static readonly AccessTools.FieldRef<NSMedieval.Goap.Goal, NSMedieval.Goap.PreferredReservableHandler> GoalPreferredRef =
            SafeField<NSMedieval.Goap.Goal, NSMedieval.Goap.PreferredReservableHandler>("preferredReservableHandler");

        private static readonly AccessTools.FieldRef<CreatureBase, StatsInstance> StatsRef =
            SafeField<CreatureBase, StatsInstance>("stats");

        private static readonly AccessTools.FieldRef<CreatureBase, string> IdRef =
            SafeField<CreatureBase, string>("id");

        private static readonly AccessTools.FieldRef<CreatureBase, InventoryInstance> InventoryRef =
            SafeField<CreatureBase, InventoryInstance>("inventory");

        private static readonly AccessTools.FieldRef<StatsInstance, HumanoidInstance> StatsHumanoidRef =
            SafeField<StatsInstance, HumanoidInstance>("ownerHumanoidInstance");

        private static readonly AccessTools.FieldRef<HumanoidRoleOwner, HumanoidInstance> RoleHumanoidRef =
            SafeField<HumanoidRoleOwner, HumanoidInstance>("humanoid");

        private static readonly AccessTools.FieldRef<BaseBuildingViewComponent, BaseBuildingBlueprint> BuildingBlueprintRef =
            SafeField<BaseBuildingViewComponent, BaseBuildingBlueprint>("baseBuildingBlueprint");

        private static readonly AccessTools.FieldRef<NSMedieval.Goap.GoalScheduler, NSMedieval.Goap.IGoapAgentOwner> SchedulerAgentRef =
            SafeField<NSMedieval.Goap.GoalScheduler, NSMedieval.Goap.IGoapAgentOwner>("agent");

        private static readonly AccessTools.FieldRef<HumanoidProximityBehaviour, HumanoidInstance> ProximityOwnerRef =
            SafeField<HumanoidProximityBehaviour, HumanoidInstance>("humanoidOwner");

        private static AccessTools.FieldRef<TOwner, TField> SafeField<TOwner, TField>(string name)
        {
            try
            {
                return AccessTools.FieldRefAccess<TOwner, TField>(name);
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError(
                    $"[access] {typeof(TOwner).Name}.{name} not found: {e.Message}");
                return null;
            }
        }

        internal static bool HasPerk(HumanoidInstance humanoid, string perkId)
        {
            if (humanoid == null || PerkIdsRef == null) return false;

            var perks = PerkIdsRef(humanoid);
            return perks != null && perks.Contains(perkId);
        }

        internal static StatsInstance Stats(CreatureBase creature)
        {
            return creature == null || StatsRef == null ? null : StatsRef(creature);
        }

        internal static string Id(CreatureBase creature)
        {
            return creature == null || IdRef == null ? "?" : IdRef(creature);
        }

        /// <summary>
        /// Something a log line can be traced back to in a saved game.
        ///
        /// <see cref="Id"/> reads the blueprint id, which for every settler in
        /// the world is the literal string "humanoid": three log lines about
        /// three different colonists all read the same, which is how a whole
        /// session of "[aura] humanoid now carries CountAuraLvl1" managed to
        /// say nothing at all. The settler's own name plus the unique id the
        /// game's own warnings print is what makes two lines comparable.
        /// </summary>
        internal static string Name(CreatureBase creature)
        {
            if (creature == null) return "?";

            var human = creature as HumanoidInstance;
            var name = human != null ? human.GetFullName() : Id(creature);

            return $"{name}#{creature.UniqueId}";
        }

        /// <summary>
        /// Whose goal pool a scheduler belongs to. The scheduler keeps its
        /// owner in a plain field and offers no way to ask.
        /// </summary>
        internal static NSMedieval.Goap.IGoapAgentOwner SchedulerAgent(NSMedieval.Goap.GoalScheduler scheduler)
        {
            return scheduler == null || SchedulerAgentRef == null ? null : SchedulerAgentRef(scheduler);
        }

        /// <summary>
        /// The settler a proximity behaviour speaks for - the one whose perks
        /// (and, here, whose role) reach out to everyone standing nearby.
        /// </summary>
        internal static HumanoidInstance ProximityOwner(HumanoidProximityBehaviour behaviour)
        {
            return behaviour == null || ProximityOwnerRef == null ? null : ProximityOwnerRef(behaviour);
        }

        /// <summary>The humanoid a stats block belongs to, or null for an animal.</summary>
        internal static CreatureBase StatsOwner(StatsInstance stats)
        {
            return stats == null || StatsHumanoidRef == null ? null : StatsHumanoidRef(stats);
        }

        internal static HumanoidInstance RoleOwnerHumanoid(HumanoidRoleOwner roleOwner)
        {
            return roleOwner == null || RoleHumanoidRef == null ? null : RoleHumanoidRef(roleOwner);
        }

        internal static BaseBuildingBlueprint BuildingBlueprint(BaseBuildingViewComponent view)
        {
            return view == null || BuildingBlueprintRef == null ? null : BuildingBlueprintRef(view);
        }

        /// <summary>
        /// The three model states a building view swaps between. All of them get
        /// scaled together, or the ghost the player drags around would not match
        /// the thing that ends up built.
        /// </summary>
        internal static IEnumerable<GameObject> BuildingModelParts(BaseBuildingViewComponent view)
        {
            if (view == null) yield break;

            foreach (var name in new[] { "finished", "blueprint", "foundation" })
            {
                var field = AccessTools.Field(typeof(BaseBuildingViewComponent), name);
                if (field != null) yield return field.GetValue(view) as GameObject;
            }
        }

        /// <summary>
        /// Flips an NPC onto EnemyBehaviour. Non-public, but it is the same call
        /// the game itself uses to turn a visitor hostile.
        ///
        /// <b>It does nothing to a settler.</b> The first line of
        /// BecomeAggressive is <c>if (activeBehaviourType &amp; 556) return;</c>,
        /// and 556 reads Worker | Enemy | CaptiveLabourer | Prisoner: the game
        /// refuses, by design, to turn one of the player's own. That refusal is
        /// the whole of "the settler got back up grey, perked and in the horde's
        /// faction, and then lay there" - it never stopped being a colonist, and
        /// a colonist that has just been killed is a colonist on the floor.
        /// Anything of ours that turns a settler goes through
        /// <see cref="TurnEnemy"/> instead.
        /// </summary>
        internal static bool BecomeAggressive(HumanoidInstance humanoid)
        {
            if (humanoid == null) return false;

            var method = AccessTools.Method(typeof(HumanoidInstance), "BecomeAggressive");
            if (method == null) return false;

            method.Invoke(humanoid, null);
            return true;
        }

        private static readonly System.Reflection.MethodInfo SetEnemyBehaviour =
            AccessTools.Method(typeof(HumanoidInstance), "SetActiveBehaviour",
                new[] { typeof(bool) }, new[] { typeof(EnemyBehaviour) });

        /// <summary>
        /// Turns anyone - a settler included - into an enemy.
        ///
        /// <c>SetActiveBehaviour&lt;EnemyBehaviour&gt;(true)</c> is the half of
        /// BecomeAggressive that does the work, and the half that knows how to
        /// move a body between the two sides: it creates the behaviour if the
        /// humanoid never had one, swaps the stats model, and - because the view
        /// is a WorkerView and the new behaviour is not WorkerBehaviour - runs
        /// IncognitoDispose/IncognitoSpawn, which takes the settler out of the
        /// Workers list, off the settlement's roster, and puts it back on the
        /// map wearing an NPC's view.
        ///
        /// What is left behind with the early return is the raid-building tail
        /// of BecomeAggressive, and leaving it behind is deliberate: it invents
        /// a new raid with a new commander over every NPC sharing the faction,
        /// which for a walker is wrong twice - the horde already has a raid, and
        /// re-commanding it mid-fight would scatter it. <see cref="JoinRaid"/>
        /// does that part properly.
        /// </summary>
        internal static bool TurnEnemy(HumanoidInstance humanoid)
        {
            if (humanoid == null || SetEnemyBehaviour == null) return false;

            try
            {
                SetEnemyBehaviour.Invoke(humanoid, new object[] { true });
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[access] could not turn {Name(humanoid)} enemy: {e.Message}");
                return false;
            }

            return humanoid.ActiveBehaviour is EnemyBehaviour;
        }

        /// <summary>
        /// Puts a fresh enemy into the raid - and under the commander - of the
        /// one that made it.
        ///
        /// A raid is two things: an id every unit carries on its EnemyBehaviour,
        /// which is what the end conditions count, and a commander that hands
        /// out the orders. A unit with neither still swings at whatever walks
        /// into its perception, but nothing ever tells it where the settlement
        /// is, so a walker made out of a settler would stand in the field it
        /// died in.
        ///
        /// Answers false when the killer itself has no raid - a stray walker
        /// left over from a finished one - and the newcomer is left to its own
        /// devices, which is what its killer is doing anyway.
        /// </summary>
        internal static bool JoinRaid(HumanoidInstance recruit, HumanoidInstance veteran)
        {
            var mine = recruit?.EnemyBehaviour;
            var theirs = veteran?.EnemyBehaviour;
            if (mine == null || theirs == null) return false;

            var raidId = theirs.RaidId;
            if (raidId <= 0) return false;

            mine.RaidId = raidId;

            try
            {
                var raid = NSMedieval.Manager.ActiveRaidInfo.GetById(raidId);
                var commanders = NSMedieval.Village.VillageManager.ActiveVillage?.Map?.CommanderAIManager;
                if (raid == null || commanders == null) return true;

                commanders.AssignUnitToCommander(mine, raid.CommanderAgentId);
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[access] could not enlist {Name(recruit)}: {e.Message}");
            }

            return true;
        }

        /// <summary>
        /// True while the humanoid is held captive. IsCaptive() is the same
        /// check the game's own test harness uses to pick a prisoner.
        /// </summary>
        internal static bool IsCaptive(HumanoidInstance humanoid)
        {
            if (humanoid == null) return false;

            var method = AccessTools.Method(typeof(HumanoidInstance), "IsCaptive");
            if (method == null) return false;

            return method.Invoke(humanoid, null) as bool? ?? false;
        }

        /// <summary>
        /// Drops a creature where it stands: the game has no stun, only the
        /// faint it uses when someone runs out of blood.
        ///
        /// <b>The lookup has to start at the runtime type.</b> HumanoidInstance
        /// declares its own Faint() that calls the CreatureBase one and then
        /// HumanoidBehaviour.HandleOnFaint() - which is the half that actually
        /// puts the body on the ground, drops what it was carrying and tells
        /// any running raid it lost someone. Resolving the method on
        /// CreatureBase got only the base half, and the result was an enemy who
        /// was unconscious by every rule the game knew and still standing up.
        /// </summary>
        internal static bool Faint(CreatureBase creature)
        {
            if (creature == null) return false;

            var method = AccessTools.Method(creature.GetType(), "Faint")
                         ?? AccessTools.Method(typeof(CreatureBase), "Faint");
            if (method == null) return false;

            method.Invoke(creature, null);
            return true;
        }

        private static readonly AccessTools.FieldRef<HumanoidInstance, bool> UnfaintPlannedRef =
            SafeField<HumanoidInstance, bool>("isUnfaintPlanned");

        private static readonly System.Reflection.MethodInfo HasFaintedSetter =
            AccessTools.PropertySetter(typeof(CreatureBase), "HasFainted");

        /// <summary>
        /// Stands a body up now, rather than some day.
        ///
        /// <c>HumanoidInstance.UnFaint()</c> is not the opposite of Faint: while
        /// any raid is still running it writes <c>isUnfaintPlanned</c> and
        /// leaves the body where it is, so the settler gets up once the fight is
        /// over. Every case this mod has for standing someone up happens during
        /// a raid - that is the only time the horde kills anyone - so the call
        /// did nothing, and what got back up stayed on the floor.
        ///
        /// The base class's own UnFaint is the whole of the real thing:
        /// HasFainted goes false, and its setter is what tells the animator to
        /// stop playing dead. The plan flag is cleared alongside it so nothing
        /// stands the body up a second time later.
        /// </summary>
        internal static bool ForceUnFaint(CreatureBase creature)
        {
            if (creature == null || HasFaintedSetter == null) return false;

            var humanoid = creature as HumanoidInstance;
            if (humanoid != null && UnfaintPlannedRef != null) UnfaintPlannedRef(humanoid) = false;

            try
            {
                HasFaintedSetter.Invoke(creature, new object[] { false });
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[access] could not wake {Name(creature)}: {e.Message}");
                return false;
            }

            return true;
        }

        internal static bool HasFainted(CreatureBase creature)
        {
            if (creature == null) return false;

            var getter = AccessTools.PropertyGetter(typeof(CreatureBase), "HasFainted");
            if (getter == null) return false;

            return getter.Invoke(creature, null) as bool? ?? false;
        }

        /// <summary>
        /// The faction a humanoid was spawned into. Serialized, so non-public
        /// like everything else that gets saved.
        /// </summary>
        /// <summary>
        /// La categoria de faccion a la que pertenece un blueprint de NPC, o
        /// null si no hay tal blueprint.
        ///
        /// Es la pregunta "de quien es este cuerpo", y no la misma que "en que
        /// faccion esta", que es <see cref="FactionId"/>. Las dos se separan
        /// cuando una faccion sale a la calle con gente que no es suya: el
        /// juego compra enemigos por categoria y tipo, y a falta de un tipo en
        /// la categoria propia tira del pool `general`, que no le esta vedado a
        /// nadie. El `factionId` de ese cuerpo es entonces el de quien lo saco,
        /// y su categoria sigue siendo la de quien lo escribio.
        /// </summary>
        internal static string NpcCategory(string blueprintId)
        {
            if (string.IsNullOrEmpty(blueprintId)) return null;

            try
            {
                var npc = NSEipix.Repository.Repository<NSMedieval.Repository.NPCRepository,
                    NSMedieval.Model.NPC>.Instance?.GetByID(blueprintId);
                return npc == null ? null : npc.Category;
            }
            catch (Exception)
            {
                // Una consulta al repositorio antes de que exista no es un
                // fallo: es un "todavia no", y la respuesta correcta es la
                // misma que para un blueprint que no esta.
                return null;
            }
        }

        internal static string FactionId(HumanoidInstance humanoid)
        {
            return humanoid == null || FactionIdRef == null ? null : FactionIdRef(humanoid);
        }

        /// <summary>
        /// The handler that carries the target a prioritise-menu order parked
        /// on the agent. Goal exposes it, but not to anyone outside its own
        /// assembly, so a subclass written here has to read the field.
        /// </summary>
        internal static NSMedieval.Goap.PreferredReservableHandler PreferredReservable(NSMedieval.Goap.Goal goal)
        {
            return goal == null || GoalPreferredRef == null ? null : GoalPreferredRef(goal);
        }

        /// <summary>
        /// Puts back a fraction of a creature's maximum health.
        ///
        /// Health here is the summary stat the wound system feeds, so this is
        /// not a cure: deep wounds keep bleeding and keep pulling it back down.
        /// What it does buy is the thing that makes feeding worth doing in a
        /// fight - a vampire that drinks mid-brawl walks out of it better than
        /// it walked in.
        /// </summary>
        internal static float HealFraction(CreatureBase creature, float fraction)
        {
            if (creature == null || fraction <= 0f) return 0f;

            var stats = Stats(creature);
            var health = stats?.GetStat(StatType.Health);
            if (health == null) return 0f;

            var before = health.Current;
            var amount = health.Max * fraction;
            health.AddCurrent(amount);

            return health.Current - before;
        }

        /// <summary>
        /// Takes a fraction of a creature's maximum health away, and answers
        /// how much it actually got.
        ///
        /// The mirror of <see cref="HealFraction"/> and deliberately not the
        /// same call with a negative number: the two read very differently at
        /// the call site, and a sign flip in the wrong place is a vampire that
        /// heals whoever it bites.
        /// </summary>
        internal static float DrainFraction(CreatureBase creature, float fraction)
        {
            if (creature == null || fraction <= 0f) return 0f;

            var stats = Stats(creature);
            var health = stats?.GetStat(StatType.Health);
            if (health == null) return 0f;

            var before = health.Current;
            health.AddCurrent(-(health.Max * fraction));

            return before - health.Current;
        }

        /// <summary>
        /// Takes a fraction of one of a creature's stats away, and answers how
        /// much it actually got.
        ///
        /// <see cref="DrainFraction"/> is this with StatType.Health baked in;
        /// what a bite really wants is Blood, which is its own 0-100 stat with
        /// its own thresholds - 80, 50 and 10 - that fire the game's own
        /// blood-loss effectors on the way down. Draining it is how a bite
        /// borrows all of that instead of restating it.
        ///
        /// A stat the creature does not have comes back null and costs nothing,
        /// which is what makes this safe to point at an animal.
        /// </summary>
        internal static float DrainStat(CreatureBase creature, StatType type, float fraction)
        {
            if (creature == null || fraction <= 0f) return 0f;

            var stat = Stats(creature)?.GetStat(type);
            if (stat == null) return 0f;

            var before = stat.Current;
            stat.AddCurrent(-(stat.Max * fraction));

            return before - stat.Current;
        }

        /// <summary>
        /// Puts a fraction of one of a creature's stats back, and answers how
        /// much of it landed.
        ///
        /// The mirror of <see cref="DrainStat"/>, and written as its own call
        /// for the same reason <see cref="HealFraction"/> is not
        /// <c>DrainFraction</c> with a minus sign: a sign flip here is a
        /// vampire that starves itself on every meal.
        /// </summary>
        internal static float RaiseStat(CreatureBase creature, StatType type, float fraction)
        {
            if (creature == null || fraction <= 0f) return 0f;

            var stat = Stats(creature)?.GetStat(type);
            if (stat == null) return 0f;

            var before = stat.Current;
            stat.AddCurrent(stat.Max * fraction);

            return stat.Current - before;
        }

        /// <summary>
        /// Puts a stat straight back to its maximum, thresholds and all.
        ///
        /// Used on the one occasion where a body has to stop being a casualty
        /// and start being a walker: a settler who gets back up with a corpse's
        /// worth of blood in the bar goes straight back down on the next tick.
        /// </summary>
        internal static void FillStat(CreatureBase creature, StatType type)
        {
            var stat = Stats(creature)?.GetStat(type);
            stat?.ForceCurrentValue(stat.Max);
        }

        /// <summary>
        /// Ends every effector a creature is carrying - wounds, moods, the lot.
        ///
        /// Wounds in this game are effectors like any other, so this is both
        /// "forget what happened to you" and "stop bleeding" in one call.
        ///
        /// EndEffectors takes names, not the running instances, and the names
        /// are copied out first: ending one edits the list they were read from.
        /// </summary>
        internal static int EndAllEffectors(CreatureBase creature)
        {
            var stats = Stats(creature);
            var active = stats?.GetActiveEffectors();
            if (active == null || active.Count == 0) return 0;

            var names = new List<string>(active.Count);
            foreach (var info in active) names.Add(info.Name);

            stats.EndEffectors(names);
            return names.Count;
        }

        /// <summary>
        /// Ends an effector even when the game would rather it ran its course.
        ///
        /// <c>StatsInstance.EndEffector(string)</c> is not the plain removal it
        /// reads as. Before taking anything off it asks:
        ///
        ///     elapsed = now - startTime
        ///     if (elapsed >= 0 &amp;&amp; blueprint.Duration > -1 &amp;&amp; elapsed &lt; blueprint.Duration)
        ///         return;                      // silently keeps it
        ///
        /// so anything with a duration that has not run out **cannot be
        /// cancelled by name**, and the call comes back looking like it worked.
        /// That is "si salio al sol y luego entra le sigue quemando": the sweep
        /// asks for the burn mark to come off the moment they are under a roof,
        /// the game declines, and nothing anywhere says so.
        ///
        /// The other overload - <c>EndEffector(ref ActiveEffectorInfo, int)</c>,
        /// the one the game itself calls when an effector expires - has no such
        /// guard. So the running instance is found in <c>activeEffectors</c> by
        /// name and that overload is called directly: same teardown, same
        /// events, no veto.
        /// </summary>
        internal static bool ForceEndEffector(CreatureBase creature, string effectorId)
        {
            var stats = Stats(creature);
            if (stats == null || string.IsNullOrEmpty(effectorId)) return false;

            try
            {
                if (!stats.IsEffectorActive(effectorId)) return false;

                var active = ActiveEffectorsRef == null ? null : ActiveEffectorsRef(stats);
                if (active == null || EndByIndex == null)
                {
                    // No way in: fall back to the polite call, which at least
                    // works for anything without a duration.
                    stats.EndEffector(effectorId);
                    return !stats.IsEffectorActive(effectorId);
                }

                for (var i = active.Count - 1; i >= 0; i--)
                {
                    if (active[i].Name != effectorId) continue;

                    object[] args = { active[i], i };
                    EndByIndex.Invoke(stats, args);
                    return true;
                }
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[stats] could not end '{effectorId}': {e}");
            }

            return false;
        }

        private static readonly AccessTools.FieldRef<StatsInstance, List<ActiveEffectorInfo>>
            ActiveEffectorsRef = SafeField<StatsInstance, List<ActiveEffectorInfo>>("activeEffectors");

        /// <summary>
        /// The overload that takes the running instance rather than its name.
        /// Looked up by signature, because the two share a name.
        /// </summary>
        private static readonly System.Reflection.MethodInfo EndByIndex =
            AccessTools.Method(typeof(StatsInstance), "EndEffector",
                new[] { typeof(ActiveEffectorInfo).MakeByRefType(), typeof(int) });

        /// <summary>
        /// What a creature is carrying. Serialized, so non-public like
        /// everything else that gets saved.
        /// </summary>
        internal static InventoryInstance Inventory(CreatureBase creature)
        {
            return creature == null || InventoryRef == null ? null : InventoryRef(creature);
        }

        /// <summary>
        /// The live faction instance for a blueprint id, or null if this world
        /// has no settlement of that faction.
        ///
        /// Faction instances are per-world, not per-repository: the entry in
        /// FactionRepository.json is the recipe, and WorldMapData holds the one
        /// that actually exists in this save. Only the instance can be handed
        /// to <c>HumanoidInstance.SetFaction</c>.
        /// </summary>
        internal static FactionInstance FactionByBlueprint(string blueprintId)
        {
            if (string.IsNullOrEmpty(blueprintId)) return null;

            try
            {
                if (!NSEipix.Base.MonoSingleton<NSMedieval.WorldMap.WorldMap>.IsInstantiated()) return null;

                var factions = NSEipix.Base.MonoSingleton<NSMedieval.WorldMap.WorldMap>
                    .Instance?.Data?.FactionInstances;
                if (factions == null) return null;

                foreach (var faction in factions)
                {
                    if (faction != null && faction.BlueprintId == blueprintId) return faction;
                }
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[access] could not look up faction {blueprintId}: {e.Message}");
            }

            return null;
        }

        /// <summary>
    /// Everything of a given kind standing on the map, and never null.
    ///
    /// <b>Why this is not just the call.</b> <c>VillageMap.GetWorldObjects</c>
    /// is two methods in a trenchcoat. Handed a single flag it indexes a
    /// dictionary, and throws if that kind has never been registered. Handed a
    /// combined mask it walks the dictionary concatenating matches, starting
    /// from <c>null</c> - so "nothing matched" comes back as null rather than
    /// as an empty list. And the dictionary itself does not exist until a
    /// village is loaded, which is where a sweep on a timer meets it:
    /// <c>[beat] stake dread sweep failed: NullReferenceException</c> in the
    /// 7 sep log, thrown inside the game's own method at VillageMap.cs:616.
    ///
    /// That one throw was not confined to the stake. The horde's raze goal
    /// reads the map through the same call, and an exception inside a Harmony
    /// postfix is swallowed - which is a horde standing in a field with a wall
    /// in front of it and nothing in the log to say why.
    ///
    /// So every reader goes through here, and gets an empty list on any of the
    /// three failures.
    /// </summary>
    internal static IEnumerable<NSMedieval.Village.WorldObject> WorldObjects(NSMedieval.Types.GridDataType kinds)
    {
        try
        {
            var map = NSMedieval.Village.VillageManager.ActiveVillage?.Map;
            if (map == null) return Enumerable.Empty<NSMedieval.Village.WorldObject>();

            return map.GetWorldObjects(kinds) ?? Enumerable.Empty<NSMedieval.Village.WorldObject>();
        }
        catch (Exception e)
        {
            GMPlugin.Log?.LogWarning($"[access] could not read the map for {kinds}: {e.Message}");
            return Enumerable.Empty<NSMedieval.Village.WorldObject>();
        }
    }

    /// <summary>
    /// Cuantos edificios de un id hay en pie, y cero ante cualquier duda.
    ///
    /// <c>BuildingsManagerMain</c> lleva un diccionario por id y
    /// <c>GetBuildingsCount</c> es una consulta a ese diccionario, asi que esto
    /// no recorre el mapa. Lo que si hace falta es el camino hasta el manager,
    /// que cuelga del mapa de la aldea activa: fuera de partida - en el menu,
    /// o mientras carga - no hay ninguno, y preguntar sin mas es la misma
    /// excepcion que se llevo por delante el barrido de la estaca el 7 de
    /// septiembre.
    /// </summary>
    internal static int BuildingsCount(string blueprintId)
    {
        if (string.IsNullOrEmpty(blueprintId)) return 0;

        try
        {
            var map = NSMedieval.Village.VillageManager.ActiveVillage?.Map;
            var manager = map?.BuildingsManagerMain;
            if (manager == null) return 0;

            return manager.GetBuildingsCount(blueprintId);
        }
        catch (Exception e)
        {
            GMPlugin.Log?.LogWarning($"[access] no se pudo contar {blueprintId}: {e.Message}");
            return 0;
        }
    }

    internal static IEnumerable<ModInstance> AllMods()
        {
            var manager = ModManager.Instance;
            if (manager == null) return Enumerable.Empty<ModInstance>();

            var method = AccessTools.Method(typeof(ModManager), "GetAllMods");
            if (method == null) return Enumerable.Empty<ModInstance>();

            return method.Invoke(manager, null) as IEnumerable<ModInstance>
                   ?? Enumerable.Empty<ModInstance>();
        }
    }
}
