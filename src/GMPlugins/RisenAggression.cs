using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSMedieval.BuildingComponents;
using NSMedieval.CombatAi;
using NSMedieval.Goap;
using NSMedieval.Manager;
using NSMedieval.State;
using NSMedieval.Types;
using NSMedieval.Village;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// The Risen do not pick fights. They have no idea there is a choice.
    ///
    /// Everything a creature considers attacking passes through
    /// <c>HostileProximitySensor.IsHostile</c>, and that method asks a long,
    /// sensible list of questions - is the target one of the player's workers,
    /// is it a prisoner, is it flagged aggressive, is its type in my attack
    /// mask - which together mean a raider walks past a trader's caravan, past
    /// another faction's beggar and past a field of goats to get to a settler.
    ///
    /// That is the right behaviour for people with a reason to be here. The
    /// horde has no reason and no allegiance: anything warm within reach is the
    /// same thing to it. So for our own creatures the answer is replaced with
    /// the only question that matters - is it alive, and is it not one of us.
    ///
    /// What is deliberately kept:
    ///   - <b>the attack mask.</b> <c>CanAttackTypes()</c> is still respected,
    ///     so an unconscious walker (mask None) still attacks nothing. Lo que ya
    ///     no lleva es el recorte de `RisenSparesAnimals`, que quitaba el bit
    ///     Animal de la mascara: con el radio de <c>RisenHunt</c> el ganado
    ///     vuelve a ser presa, y una mascara que lo veta seria la regla nueva
    ///     escrita y desactivada a la vez.
    ///   - <b>the dead and the downed.</b> Nothing is gained by mobbing a body,
    ///     and a horde that stops to finish every fainted settler never reaches
    ///     the wall.
    ///   - <b>each other.</b> A horde that fights itself is a horde that never
    ///     arrives.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenHostility
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(HostileProximitySensor), "IsHostile");
        }

        // Positional injection: the game's parameter names are not part of any
        // documented surface, and getting a name wrong is not a warning - it is
        // "IL Compile Error" at startup and the whole class silently unpatched.
        //
        // Which argument is which matters more than it looks. The signature is
        // IsHostile(IDamageDealAgent item, IDamageTakingAgent self), and Refresh
        // calls it as IsHostile(candidate, this.combatAgent): the first is
        // whoever is being looked at, the second is the creature doing the
        // looking. Read the other way round - which is how this was written -
        // the patch only ever fired on the sensors of the living looking at a
        // walker, which vanilla already answers true to, and never once on the
        // walker's own perception. That is the whole of "ignoraron a los
        // animales y a los enemigos de otras facciones": the horde was never
        // the one being asked.
        private static void Postfix(IDamageDealAgent __0, IDamageTakingAgent __1, ref bool __result)
        {
            var walker = __1 as HumanoidInstance;
            if (walker == null || !Census.IsUndead(walker)) return;

            var target = __0 as CreatureBase;
            if (target == null || target.HasDisposed || target.HasDiedOrFainted) return;
            if (ReferenceEquals(target, walker)) return;

            // Not each other, and not the settlers who have already turned.
            var human = target as HumanoidInstance;
            if (human != null && Census.IsUndead(human)) return;

            // Y aqui ya no se estrecha nada: cualquier cosa viva que no sea
            // de los nuestros es hostil para un alzado.
            //
            // Esto era el "los alzados solo quieren la aldea" - un `return
            // false` para todo el que no fuera colono - y se dio la vuelta a
            // peticion: el radio de RisenHunt vale para lo que entre en el, sea
            // colono, saqueador, mercader o cabra, y el colono ya solo gana los
            // empates. Dejar aqui la exclusion seria escribir la regla nueva y
            // vetarla dos lineas mas abajo: RisenHunt senala la presa, pero
            // quien decide si el golpe llega a darse sigue siendo este metodo.
            //
            // Lo que gana el jugador de propina es que un alzado al que pega un
            // saqueador ahora devuelve el golpe. Antes no: el saqueador no era
            // hostil para el, asi que la horda se dejaba pegar.
            if (__result) return;
            if (!(GMPlugin.UndeadAttackEverything?.Value ?? false)) return;

            var prey = __0 as IDamageTakingAgent;
            if (prey == null) return;
            if ((walker.CanAttackTypes() & prey.DamageAgentType) == DamageTakingAgentType.None) return;

            __result = true;
        }

        /// <summary>
        /// Whether this is one of the player's people.
        ///
        /// Ya no veta nada - desde el radio de caza, un alzado ataca a todo lo
        /// vivo - pero sigue siendo la definicion de "colono" de todo el mod, y
        /// <c>RisenHunt.Rank</c> desempata por ella: el colono primero.
        ///
        /// <c>WorkerBehaviour</c> es la prueba, y es la correcta por una razon
        /// que conviene dejar escrita: un colono conserva su WorkerView y su
        /// WorkerBehaviour despues de cambiar de bando, que es justo por lo que
        /// la comprobacion de no-muerto tiene que ir antes. Lo que no tiene
        /// WorkerBehaviour es un visitante, un saqueador o un animal.
        /// </summary>
        internal static bool IsTheirs(HumanoidInstance human)
        {
            return human != null && human.WorkerBehaviour != null;
        }
    }

    /// <summary>
    /// And the other way round: nothing alive is on speaking terms with a
    /// corpse that walks.
    ///
    /// <b>Why this is a second patch and not the same one.</b>
    /// <see cref="RisenHostility"/> answers the walker's own sensor - "is that
    /// thing prey" - and that is all it answers. The question a raider, a
    /// trader's guard or a wolf asks is the mirror of it, and vanilla answers
    /// that one by faction: the Undead Horde is simply another faction, so
    /// anybody who is not at war with it walks straight past a walker, and two
    /// raiding parties share a yard without a blow struck. Reported as "los
    /// enemigos, cualquiera deberia ver como enemigo a un zombie".
    ///
    /// So for a target that is one of ours, the answer is yes for anything
    /// alive that can hit it at all - the player's settlers included, who
    /// already said yes, and every faction that never would have.
    ///
    /// The same three things are kept back as in the other direction: our own
    /// do not turn on each other, nothing picks a fight with a body on the
    /// ground, and the looker's attack mask still has the last word, so an
    /// unconscious settler does not suddenly swing at anything.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenHostileToAll
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(HostileProximitySensor), "IsHostile");
        }

        private static void Postfix(IDamageDealAgent __0, IDamageTakingAgent __1, ref bool __result)
        {
            if (__result) return;
            if (!(GMPlugin.UndeadHatedByAll?.Value ?? true)) return;

            // __0 is the thing being looked at, __1 the one doing the looking -
            // the opposite way round from RisenHostility, and the whole of the
            // difference between the two patches.
            var walker = __0 as HumanoidInstance;
            if (walker == null || !Census.IsUndead(walker)) return;
            if (walker.HasDisposed || walker.HasDiedOrFainted) return;

            var looker = __1 as CreatureBase;
            if (looker == null || looker.HasDisposed || looker.HasDiedOrFainted) return;
            if (ReferenceEquals(looker, walker)) return;

            var human = looker as HumanoidInstance;
            if (human != null && Census.IsUndead(human)) return;

            var attacker = __1 as IDamageDealAgent;
            var prey = __0 as IDamageTakingAgent;
            if (attacker == null || prey == null) return;
            if ((looker.CanAttackTypes() & prey.DamageAgentType) == DamageTakingAgentType.None) return;

            __result = true;
        }
    }

    /// <summary>
    /// A wall is a target too.
    ///
    /// The reported behaviour: seal the settlement, and the horde stops. Not
    /// mills around - stops, facing the wall, indefinitely. The reason is that
    /// every one of the game's "what do I attack" answers ends at a path:
    ///
    ///   - the commander's <c>PickHighestValueRoomBuilding</c> only looks at
    ///     <em>reachable</em> rooms;
    ///   - <c>EnemyTargetDoorsAiPlanGoal</c> asks whether a path would exist
    ///     <em>if doors were walkable</em>, which is a wall-shaped no;
    ///   - <c>EnemyTargetProductionAiPlanGoal</c>, the last one, looks only at
    ///     <c>GridDataType.ProductionBuilding</c> - the workshops, which are
    ///     inside, behind the wall the raider is standing at.
    ///
    /// A besieging army behaves that way on purpose: it waits, or it brings a
    /// trebuchet. The Risen have neither the patience nor the trebuchet, and a
    /// horde standing politely outside a wall is the single most immersion
    /// breaking thing this mod could ship.
    ///
    /// This widens the last of the three. The goal already knows how to walk to
    /// a building and hit it - <c>TargetProductionBuilding</c> does the
    /// pathfinding, the attack positioning and the SetPreferredTarget - so the
    /// only thing that has to change is <em>what counts as a building</em>. For
    /// our creatures that is everything the player put down, walls included,
    /// and the wall in front of the walker is by definition the one it can
    /// reach.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenTargetBuildings
    {
        /// <summary>
        /// Everything the player builds, as the map indexes it: finished
        /// structures (walls, floors, doors), furniture, gates, drawbridges and
        /// the workshops vanilla already looked at. Blueprints and half-built
        /// things are left out - there is nothing there yet to tear down.
        /// </summary>
        private const GridDataType BuiltThings =
            GridDataType.BuildingFinished | GridDataType.Furniture
            | GridDataType.ProductionBuilding | GridDataType.FurnitureGate
            | GridDataType.Drawbridge;

        private static readonly AccessTools.FieldRef<EnemyTargetProductionAiPlanGoal, List<WorldObject>>
            BuildingsRef = Ref();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EnemyTargetProductionAiPlanGoal),
                "FindProductionBuildings");
        }

        private static void Postfix(EnemyTargetProductionAiPlanGoal __instance, ref bool __result)
        {
            if (!(GMPlugin.UndeadRazeEverything?.Value ?? false)) return;
            if (BuildingsRef == null || __instance == null) return;
            if (!RisenChaseFirst.MayRaze(__instance.AgentOwner as HumanoidInstance)) return;

            var owner = __instance.AgentOwner as HumanoidInstance;
            if (owner == null || !Census.IsUndead(owner)) return;

            var found = Built(owner);

            // Said once per walker, because "the horde will not break anything"
            // and "the horde found nothing to break" are the same picture on
            // screen and were the same silence in the log for two test rounds.
            if (Told.Add(owner.UniqueId))
            {
                GMPlugin.Log?.LogInfo(
                    $"[raze] {GameAccess.Name(owner)} may raze and sees {found.Count} thing(s) to break");
            }

            if (found.Count == 0) return;

            BuildingsRef(__instance) = found;
            __result = true;
        }

        /// <summary>Walkers whose first raze search has already been reported.</summary>
        private static readonly HashSet<int> Told = new HashSet<int>();

        /// <summary>
        /// Every built thing on the map this creature could actually swing at.
        ///
        /// The one filter kept from vanilla is <c>IsAttackPossible</c> - the
        /// bitwise attacker-mask-against-target-type check that decides whether
        /// an attack is a thing that could happen at all.
        ///
        /// Vanilla's second filter is not copied. It reads, in IL, as "keep the
        /// building only if it stands in Low or Medium water", which cannot be
        /// what was meant and is not worth guessing at; deep water is already
        /// somewhere the pathfinder will not send a walker, so leaving it out
        /// costs nothing. Reachability is not tested here either, on purpose -
        /// the goal's own <c>TargetProductionBuilding</c> does that next, with
        /// the pathfinder, and it is better at it than any check written here.
        /// </summary>
        private static List<WorldObject> Built(HumanoidInstance attacker)
        {
            var found = new List<WorldObject>();

            var objects = GameAccess.WorldObjects(BuiltThings);

            foreach (var obj in objects)
            {
                var building = obj as BaseBuildingInstance;
                if (building == null) continue;
                if (!CombatUtils.IsAttackPossible(attacker, building)) continue;

                found.Add(obj);
            }

            return found;
        }

        private static AccessTools.FieldRef<EnemyTargetProductionAiPlanGoal, List<WorldObject>> Ref()
        {
            try
            {
                return AccessTools.FieldRefAccess<EnemyTargetProductionAiPlanGoal, List<WorldObject>>(
                    "productionBuildings");
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[raze] productionBuildings unreachable: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Colonists first. A wall is what you hit when you cannot reach one.
    ///
    /// The horde has two ways of picking a target and they pull against each
    /// other. <see cref="RisenTargetBuildings"/> hands the raze goal every built
    /// thing on the map, and the game's own goal order puts that goal ahead of
    /// the one that hunts settlers - so a walker that could see a colonist past
    /// a fence would take the fence apart instead.
    ///
    /// Rather than reorder a goal list shared with every human raid in the
    /// game, the raze goal stands down while a settler is known to be
    /// reachable.
    ///
    /// <b>Which way round that memory goes is the whole of this class, and it
    /// was the wrong way round.</b> The first version remembered
    /// <em>failure</em>: a walker could only start breaking things once
    /// <c>TargetBestWorkerThread</c> had come back false at least once. That
    /// search lives inside <c>EnemyTargetWorkersAiPlanGoal.CanStart</c>, whose
    /// very first line is
    ///
    ///     if (IsEnemy() &amp;&amp; EnemyBehaviour.CurrentOrder != null) return false;
    ///
    /// and a walker in a raid is under a commander's order nearly all the time.
    /// So the search never ran, the failure was never recorded, the raze goal
    /// was refused forever, and what was left on screen was a horde standing in
    /// a field doing nothing at all - reported word for word as "no atacan a
    /// los muros cuando no tienen colonos a la vista, no hacen nada, solo se
    /// quedan parados". A permission that has to be earned with evidence that
    /// can never arrive is not a rule, it is a deadlock.
    ///
    /// So it remembers <b>success</b> instead. Breaking things is the default,
    /// and it is called off only while there is fresh proof that a settler can
    /// really be reached - a search that came back true within
    /// <c>Undead/RazeMemorySeconds</c>. Village open: the chase keeps
    /// succeeding, the mark keeps being renewed, and the horde runs past the
    /// fences at the people. Village sealed: the last success lapses within
    /// twenty seconds and the walls start coming down. Nothing runs at all: the
    /// walls come down, which is the right answer when in doubt.
    ///
    /// <see cref="RisenChaseUnderOrders"/> is the other half - it lets the
    /// chase run under orders in the first place, so that "fresh proof" is
    /// something the horde can actually produce.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenChaseFirst
    {
        /// <summary>
        /// When each walker last found a way to a settler, by unique id.
        /// Written from the pathfinding thread and read from the main one, so
        /// every touch goes under the lock.
        /// </summary>
        private static readonly Dictionary<int, float> ReachedAt = new Dictionary<int, float>();

        private static readonly object Gate = new object();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EnemyTargetWorkersAiPlanGoal), "TargetBestPlayerUnitThread");
        }

        private static void Postfix(EnemyTargetWorkersAiPlanGoal __instance, bool __result)
        {
            var owner = __instance?.AgentOwner as HumanoidInstance;
            if (owner == null || !Census.IsUndead(owner)) return;

            lock (Gate)
            {
                // Heartbeat.Now, not Time.time. This postfix runs on the GOAP's
                // own worker thread, and Time.time throws there rather than
                // returning anything - taking the whole target search down with
                // it, silently, as a ThreadingJobSystem warning.
                if (__result) ReachedAt[owner.UniqueId] = Heartbeat.Now;
                else ReachedAt.Remove(owner.UniqueId);
            }
        }

        /// <summary>
        /// Wipes a walker's reached-a-settler mark, so the next thing it is
        /// allowed to do is break something. <see cref="RisenRestless"/> calls
        /// this when a walker has been standing still long enough to say the
        /// memory is stale whatever the clock says: one that has not moved in
        /// half a minute is not on its way to anybody.
        /// </summary>
        internal static void Forget(int uniqueId)
        {
            lock (Gate)
            {
                ReachedAt.Remove(uniqueId);
            }
        }

        /// <summary>
        /// Whether this walker should be breaking things rather than walking.
        ///
        /// Anything that is not one of ours - and the option turned off - gets
        /// vanilla's answer back, so this only ever narrows the horde's own
        /// behaviour. Everything else may raze <em>unless</em> it has just been
        /// shown a way to a living settler.
        /// </summary>
        internal static bool MayRaze(HumanoidInstance walker)
        {
            if (walker == null) return true;
            if (!Census.IsUndead(walker)) return true;
            if (!(GMPlugin.UndeadChaseFirst?.Value ?? true)) return true;

            var memory = GMPlugin.RazeMemorySeconds?.Value ?? 20f;

            lock (Gate)
            {
                float when;
                if (!ReachedAt.TryGetValue(walker.UniqueId, out when)) return true;

                if (Heartbeat.Now - when <= memory) return false;   // still on its way

                ReachedAt.Remove(walker.UniqueId);
                return true;
            }
        }
    }

    /// <summary>
    /// The goats, the traders, and everybody else the chase goal cannot see.
    ///
    /// <b>Where the horde's blindness really lives.</b> Two patches already
    /// said the horde should attack anything alive:
    /// <see cref="RisenHostility"/> makes the sensor call a goat hostile, and
    /// <see cref="RisenHuntAnimals"/> puts the Animal bit in the attack mask.
    /// Both work. Neither is enough, because between the sensor and the swing
    /// there is a goal, and <c>EnemyTargetWorkersAiPlanGoal
    /// .GatherAttackPositions</c> walks the perception list through this:
    ///
    ///     if (!(item is HumanoidInstance h) || h.WorkerBehaviour == null
    ///         || h.HasFainted) continue;
    ///
    /// A goat has no WorkerBehaviour. Neither has a trader, a beggar, or
    /// another faction's raider. The name of the goal is not decoration - it
    /// hunts <em>workers</em>, and the perception list is only ever a shortlist
    /// of settlers to it. So the horde saw the goat, was allowed to hit the
    /// goat, and had nothing that would ever walk it over to one. That is "las
    /// hordas y el ganado: no los atacan todavia, si solo estan zombies y
    /// animales" - with no settler on the map, the goal came back empty and
    /// there was nothing else to come back.
    ///
    /// So the ones vanilla threw away are picked back up, in the same shape the
    /// method leaves its own answer in: a path per candidate in
    /// <c>possibleAttackPoints</c>, or - if one is already close enough to
    /// swing at - the short circuit vanilla itself uses, which is
    /// <c>skipPathFinding</c> and a single entry in <c>possibleTargets</c>.
    ///
    /// <b>Only when vanilla found nobody.</b> A settler in reach always
    /// outranks a chicken; this runs on <c>__result == false</c>, which is
    /// exactly "there was no settler". That keeps "no soltar a un colono para
    /// ir a por una gallina" true without a priority rule of its own.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenHuntPerception
    {
        private static readonly AccessTools.FieldRef<EnemyTargetWorkersAiPlanGoal,
                List<System.Tuple<IDamageTakingAgent, NSMedieval.Village.Map.Pathfinding.Path>>>
            PointsRef = Points();

        private static readonly AccessTools.FieldRef<EnemyTargetWorkersAiPlanGoal,
            List<System.Tuple<int, IDamageTakingAgent>>> TargetsRef = Targets();

        /// <summary>
        /// <c>skipPathFinding</c> is declared volatile, and a volatile field
        /// has a modreq on its type that <c>FieldRefAccess</c> will not match.
        /// Plain reflection does, and this is written once per search.
        /// </summary>
        private static readonly FieldInfo SkipPathField =
            AccessTools.Field(typeof(EnemyTargetWorkersAiPlanGoal), "skipPathFinding");

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EnemyTargetWorkersAiPlanGoal), "GatherAttackPositions");
        }

        private static void Postfix(EnemyTargetWorkersAiPlanGoal __instance, ref bool __result)
        {
            if (__result || __instance == null) return;
            if (!(GMPlugin.UndeadAttackEverything?.Value ?? false)) return;
            if (PointsRef == null || TargetsRef == null) return;

            var owner = __instance.AgentOwner as HumanoidInstance;
            if (owner == null || !Census.IsUndead(owner)) return;

            var ai = owner.CombatAi;
            if (ai == null) return;

            var seen = ai.GetState<HashSet<IDamageDealAgent>>(CombatAiState.PerceptionHostiles);
            if (seen == null || seen.Count == 0) return;

            var points = PointsRef(__instance);
            if (points == null) return;

            var positioning = NSEipix.Base.MonoSingleton<CombatAttackerPositioningManager>
                .IsInstantiated()
                ? CombatAttackerPositioningManager.Instance
                : null;

            foreach (var candidate in seen)
            {
                var prey = candidate as IDamageTakingAgent;
                if (!Worth(owner, prey)) continue;

                // Already within reach: vanilla's own short circuit, which
                // spares the walker a path it does not need.
                if (CombatAttackerPositioningManager.IsInAttackPosition(owner, prey))
                {
                    SkipPathField?.SetValue(__instance, true);

                    var targets = TargetsRef(__instance);
                    if (targets == null) continue;

                    targets.Clear();
                    targets.Add(new System.Tuple<int, IDamageTakingAgent>(0, prey));

                    __result = true;
                    return;
                }

                if (positioning == null) continue;

                var path = positioning.CreatePath(owner, prey, false);
                if (path == null) continue;

                points.Add(
                    new System.Tuple<IDamageTakingAgent, NSMedieval.Village.Map.Pathfinding.Path>(
                        prey, path));
            }

            if (points.Count > 0) __result = true;
        }

        /// <summary>
        /// Whether this is one of the ones vanilla threw away and we want back.
        ///
        /// Anything with a WorkerBehaviour is skipped here because the method
        /// this runs after has already considered it: adding it again would put
        /// the same settler in the list twice. The rest of the tests are
        /// vanilla's own, in vanilla's order.
        /// </summary>
        private static bool Worth(HumanoidInstance owner, IDamageTakingAgent prey)
        {
            if (prey == null) return false;

            var body = prey as CreatureBase;
            if (body == null || body.HasDisposed || body.HasDiedOrFainted) return false;
            if (ReferenceEquals(body, owner)) return false;

            var human = body as HumanoidInstance;
            if (human != null)
            {
                if (human.WorkerBehaviour != null) return false;   // vanilla had this one
                if (Census.IsUndead(human)) return false;     // not each other
            }

            return CombatUtils.IsAttackPossible(owner, prey)
                   && CombatUtils.IsValidToFocus(owner, prey);
        }

        private static AccessTools.FieldRef<EnemyTargetWorkersAiPlanGoal,
            List<System.Tuple<IDamageTakingAgent, NSMedieval.Village.Map.Pathfinding.Path>>> Points()
        {
            try
            {
                return AccessTools.FieldRefAccess<EnemyTargetWorkersAiPlanGoal,
                    List<System.Tuple<IDamageTakingAgent,
                        NSMedieval.Village.Map.Pathfinding.Path>>>("possibleAttackPoints");
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[hunt] possibleAttackPoints unreachable: {e.Message}");
                return null;
            }
        }

        private static AccessTools.FieldRef<EnemyTargetWorkersAiPlanGoal,
            List<System.Tuple<int, IDamageTakingAgent>>> Targets()
        {
            try
            {
                return AccessTools.FieldRefAccess<EnemyTargetWorkersAiPlanGoal,
                    List<System.Tuple<int, IDamageTakingAgent>>>("possibleTargets");
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[hunt] possibleTargets unreachable: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Lets the horde hunt people while it is under orders.
    ///
    /// <c>EnemyTargetWorkersAiPlanGoal.CanStart</c> opens by refusing anyone
    /// holding a <c>CurrentOrder</c>, and a walker in a raid is under one
    /// almost always: the commander hands out "attack this room" and "move
    /// there" whether or not the thing it names can be reached. For a soldier
    /// that ordering is right - orders come before initiative. The Risen have
    /// no initiative for orders to come before, and an order they cannot path
    /// to is not an order, it is a wall.
    ///
    /// The damage went further than a walker ignoring a colonist. The search
    /// inside that goal is the only thing in the game that ever answers "can I
    /// reach a settler from here", and <see cref="RisenChaseFirst"/> is built
    /// on that answer. Refused under orders, the search never ran, so the
    /// answer never existed, so nothing downstream of it could work either.
    ///
    /// Every other condition vanilla checks is kept, in the order the game
    /// checks them: not fainted, a path driver present, and no preferred target
    /// already chosen.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenChaseUnderOrders
    {
        private static readonly AccessTools.FieldRef<EnemyTargetWorkersAiPlanGoal,
            NSMedieval.Village.Map.Pathfinding.PathfinderAgentDriver> DriverRef = Ref();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EnemyTargetWorkersAiPlanGoal), "CanStart");
        }

        private static void Postfix(EnemyTargetWorkersAiPlanGoal __instance, ref bool __result)
        {
            if (__result || __instance == null || DriverRef == null) return;

            var owner = __instance.AgentOwner as HumanoidInstance;
            if (owner == null || !Census.IsUndead(owner)) return;
            if (!(owner.ActiveBehaviour is EnemyBehaviour)) return;

            var ai = owner.CombatAi;
            if (ai == null) return;
            if (RisenGoalState.IsSet(ai, CombatAiState.Fainted)) return;
            if (RisenGoalState.IsSet(ai, CombatAiState.PreferedTarget)) return;

            // CanStart fills this in on its way past the order check and bails
            // before reaching it when the order is what stopped it, so on this
            // path it can still be null.
            if (DriverRef(__instance) == null) DriverRef(__instance) = owner.PathDriver;
            if (DriverRef(__instance) == null) return;

            __result = true;
        }

        private static AccessTools.FieldRef<EnemyTargetWorkersAiPlanGoal,
            NSMedieval.Village.Map.Pathfinding.PathfinderAgentDriver> Ref()
        {
            try
            {
                return AccessTools.FieldRefAccess<EnemyTargetWorkersAiPlanGoal,
                    NSMedieval.Village.Map.Pathfinding.PathfinderAgentDriver>("pathfinderAgentDriver");
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[chase] pathfinderAgentDriver unreachable: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Every walker on the map, wherever the game happens to be keeping it.
    ///
    /// Three sweeps needed this same list and each had grown its own copy,
    /// which is how the stake managed not to reach turned settlers for a whole
    /// test round: a settler who turns only joins the village's NPC list when
    /// <c>RisenRoster.Adopt</c> could hand it the horde's blueprint, and when it
    /// could not, the body is hostile, upright, and in no list anybody was
    /// reading.
    /// </summary>
    internal static class RisenRoll
    {
        internal static IEnumerable<HumanoidInstance> Walkers()
        {
            var npcs = NSMedieval.GlobalSaveController.CurrentVillageData?.NPCs;

            if (npcs != null)
            {
                for (var i = 0; i < npcs.Count; i++)
                {
                    if (Standing(npcs[i])) yield return npcs[i];
                }
            }

            foreach (var turned in RisenRestless.TurnedWalkers())
            {
                if (npcs != null && npcs.Contains(turned)) continue;   // counted once
                if (Standing(turned)) yield return turned;
            }
        }

        internal static bool Standing(HumanoidInstance walker)
        {
            if (walker == null || walker.HasDisposed || walker.HasDiedOrFainted) return false;
            if (!(walker.ActiveBehaviour is EnemyBehaviour)) return false;

            return Census.IsUndead(walker);
        }
    }

    /// <summary>
    /// Lets the goal above start at all while the horde is under orders.
    ///
    /// <c>CanStart</c> refuses outright to anyone holding a <c>CurrentOrder</c>,
    /// which during a raid is most of the time - the commander hands out
    /// "attack this" and "move there" whether or not a path to it exists. For a
    /// soldier that is correct: orders come before initiative. An order a
    /// walker cannot path to is not an order, it is a wall.
    ///
    /// Every other condition vanilla checks is kept, and they are what keep
    /// this a last resort: not fainted, and <b>no preferred target already
    /// chosen</b> - so anything living, anywhere reachable, still comes first.
    /// Melee too, because a walker has no other way of hitting anything.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenTargetBuildingsUnderOrders
    {
        private static readonly AccessTools.FieldRef<EnemyTargetProductionAiPlanGoal,
            NSMedieval.Village.Map.Pathfinding.PathfinderAgentDriver> DriverRef = Ref();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EnemyTargetProductionAiPlanGoal), "CanStart");
        }

        private static void Postfix(EnemyTargetProductionAiPlanGoal __instance, ref bool __result)
        {
            if (!(GMPlugin.UndeadRazeEverything?.Value ?? false)) return;
            if (__instance == null) return;

            // The refusal half, and the reason this postfix no longer returns
            // early on an answer of true. In CombatAiAgents.json the enemy's
            // goal list runs EnemyTargetProductionAiPlanGoal *before*
            // EnemyTargetWorkersAiPlanGoal, which in vanilla is harmless: the
            // production goal only ever looked at workshops, which sit deep
            // inside a village where a raider usually cannot get. Widened to
            // everything the player built, that same order reads "take apart
            // the nearest fence, then think about the people" - a demolition
            // crew, not a horde. So for our own creatures the goal stands down
            // while a settler is still known to be reachable.
            if (!RisenChaseFirst.MayRaze(__instance.AgentOwner as HumanoidInstance))
            {
                __result = false;
                return;
            }

            if (__result) return;
            if (DriverRef == null) return;

            var owner = __instance.AgentOwner as HumanoidInstance;
            if (owner == null || !Census.IsUndead(owner)) return;
            if (!(owner.ActiveBehaviour is EnemyBehaviour)) return;
            if (!RisenChaseFirst.MayRaze(owner)) return;

            var ai = owner.CombatAi;
            if (ai == null) return;
            if (RisenGoalState.IsSet(ai, CombatAiState.Fainted)) return;
            if (RisenGoalState.IsSet(ai, CombatAiState.PreferedTarget)) return;   // living first

            // The melee test that used to be here is gone. It read
            // `GetAttackType(owner, null) != Melee` and was meant as "a walker
            // has no other way of hitting anything" - but a settler who turns
            // mid-raid is a walker made out of whatever the game thought that
            // colonist was, and the answer for one of those is not reliably
            // Melee. So the settlers who turned were the one kind of Risen that
            // could never be allowed to break a wall, which is exactly "los
            // zombies levantados no estan golpeando a los muros". Nothing is
            // lost by dropping it: a walker that genuinely cannot reach a
            // building fails inside the goal a moment later, the way vanilla
            // already handles every other unreachable target.

            // CanStart fills this in on its way past the order check, and bails
            // before reaching it when the order is what stopped it - so on this
            // path it can still be null.
            if (DriverRef(__instance) == null) DriverRef(__instance) = owner.PathDriver;
            if (DriverRef(__instance) == null) return;

            __result = true;
        }

        private static AccessTools.FieldRef<EnemyTargetProductionAiPlanGoal,
            NSMedieval.Village.Map.Pathfinding.PathfinderAgentDriver> Ref()
        {
            try
            {
                return AccessTools.FieldRefAccess<EnemyTargetProductionAiPlanGoal,
                    NSMedieval.Village.Map.Pathfinding.PathfinderAgentDriver>("pathfinderAgentDriver");
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[raze] pathfinderAgentDriver unreachable: {e.Message}");
                return null;
            }
        }
    }
}
