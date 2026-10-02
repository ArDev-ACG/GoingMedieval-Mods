using System.Collections.Generic;
using NSMedieval.BuildingComponents;
using NSMedieval.CombatAi;
using NSMedieval.Goap;
using NSMedieval.CommanderAI.Orders;
using NSMedieval.Manager;
using NSMedieval.State;
using NSMedieval.Types;
using NSMedieval.Village;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Lo que los alzados hacen ahora: miran veinticinco casillas a su
    /// alrededor, van a por lo que entre, y se avisan.
    ///
    /// <b>Por que esto sustituye a "cualquiera ve a un zombie como enemigo" y a
    /// "los alzados solo quieren la aldea".</b> Las dos eran reglas escritas del
    /// lado del juego: una decia que si a <c>HostileProximitySensor.IsHostile</c>
    /// y la otra decia que no. Registrar el parche que faltaba no cambio lo que
    /// se ve en partida, y la razon es que la hostilidad no es la percepcion: el
    /// sensor del juego tiene su propio alcance, corto y con condiciones, y un
    /// alzado que no tiene a nadie en ese sensor no pregunta por nadie. Decir
    /// que si mas fuerte a una pregunta que no se hace no cambia nada.
    ///
    /// Asi que el alcance pasa a ser nuestro y esta escrito: un radio, un barrido
    /// y tres reglas.
    ///
    ///   1. <b>El radio.</b> Cada segundo y medio, todo lo vivo que este dentro
    ///      de <see cref="Radius"/> casillas de un alzado es presa suya, venga de
    ///      donde venga - colonos, saqueadores, mercaderes, visitantes y bichos.
    ///      Empatan por cercania y <b>desempata el colono</b>, que es lo unico
    ///      que queda de "solo quieren la aldea": ya no es una exclusion, es un
    ///      desempate.
    ///   2. <b>Andar a proposito.</b> Un alzado sin nada en el radio no se queda
    ///      quieto: se le manda una <c>MoveOrder</c> hacia la aldea - o hacia un
    ///      rumbo propio que gira, si no hay aldea que buscar - y al moverse el
    ///      radio viaja con el. Encontrar algo deja de depender de que la presa
    ///      se acerque.
    ///   3. <b>Avisar.</b> Cuando uno ataca, apunta **donde esta el** en
    ///      <see cref="Alerts"/>. Los demas, si no tienen nada propio que hacer,
    ///      caminan a ese punto; y al llegar, lo que sea que pase alli ya les cae
    ///      dentro del radio por la regla 1. Nadie hereda el objetivo de nadie:
    ///      se hereda **la posicion**, que es lo que se pidio y ademas es lo
    ///      unico que sobrevive a que la presa se mueva o se muera.
    ///
    /// Esto se come a la antigua <c>RisenPreferLiving</c>, que hacia la regla 1
    /// con una sola presa posible - el colono - y solo para soltar un muro.
    /// Soltar el muro sigue aqui: un objetivo que es un edificio pierde siempre
    /// contra cualquier cosa viva dentro del radio.
    /// </summary>
    internal static class RisenHunt
    {
        /// <summary>
        /// Cada cuanto se mira, en segundos reales. Mas rapido que el vigilante
        /// de los quietos porque esto es no perder una ocasion, no desatascar:
        /// quien pasa por delante de un alzado pasa una vez.
        /// </summary>
        private const float SweepSeconds = 1.5f;

        /// <summary>Casillas. Es el numero que se pidio.</summary>
        private static int Radius
        {
            get { return GMPlugin.UndeadHuntRadius?.Value ?? 25; }
        }

        internal static void Start()
        {
            GMPlugin.Every("undead hunt", SweepSeconds, Sweep);
        }

        private static void Sweep()
        {
            if (!(GMPlugin.UndeadChaseFirst?.Value ?? true)) return;

            Forget();
            CountClaims();

            foreach (var walker in RisenRoll.Walkers())
            {
                Look(walker);
            }
        }

        // --- 0. el reparto -----------------------------------------------------

        /// <summary>
        /// Cuantos alzados van ya a por cada presa, por UniqueId de la presa.
        ///
        /// <b>Por que hace falta.</b> Cuerpo a cuerpo, cada casilla alrededor
        /// del objetivo la reserva un solo atacante
        /// (<c>CombatAttackTracker.CanBeReservedBy</c>), y alrededor de un cuerpo
        /// hay ocho como mucho. La horda entera iba a por la misma presa - en el
        /// log del 20, nueve alzados contra dos saqueadores - y los que no
        /// cabian fallaban la secuencia de inicio de <c>AttackGoal</c>
        /// ("Error starting goal 'AttackGoal' init sequence"), perdian el
        /// objetivo, se les volvia a dar y volvian a fallar. Eso era el "pegan
        /// una vez de cada n".
        /// </summary>
        private static readonly Dictionary<int, int> Claims = new Dictionary<int, int>();

        private static int MaxPerPrey
        {
            get { return Mathf.Max(1, GMPlugin.UndeadMaxPerPrey?.Value ?? 4); }
        }

        private static void CountClaims()
        {
            Claims.Clear();
            foreach (var walker in RisenRoll.Walkers())
            {
                var held = walker.CombatAi?.GetState<IDamageTakingAgent>(CombatAiState.PreferedTarget) as CreatureBase;
                if (held != null) Claim(held, 1);
            }
        }

        private static void Claim(CreatureBase prey, int delta)
        {
            if (prey == null) return;
            int n;
            Claims.TryGetValue(prey.UniqueId, out n);
            Claims[prey.UniqueId] = Mathf.Max(0, n + delta);
        }

        private static int ClaimsOn(CreatureBase prey)
        {
            int n;
            return prey != null && Claims.TryGetValue(prey.UniqueId, out n) ? n : 0;
        }

        /// <summary>
        /// Si el alzado esta de verdad con esa presa: con la orden de atacarla
        /// puesta y andando hacia ella o pegandole. El estado de la IA de combate
        /// solo no basta - cuando <c>AttackGoal</c> falla, el juego borra el
        /// objetivo de <c>CombatTargetManager</c> pero el de la IA se queda, y
        /// con el "ya esta con alguien" de antes el alzado se quedaba mirando a
        /// su presa para siempre. Ese era el "pegan una vez y se quedan quietos".
        /// </summary>
        private static bool Busy(HumanoidInstance walker, IDamageTakingAgent held)
        {
            var current = walker.EnemyBehaviour?.CurrentOrder;

            // Contra otro enemigo la orden es seguirle (ver Shadow): mientras la
            // lleve puesta, esta con el, ande o este pegandole via RisenBrawl.
            var follow = current as MoveOrder;
            if (follow != null) return ReferenceEquals(follow.FollowCreature, held);

            var order = current as AttackOrder;
            if (order == null || !ReferenceEquals(order.Target, held)) return false;

            var goal = walker.GoapAgent?.CurrentGoalName;
            return goal == "FollowAttackOrderGoal" || goal == "AttackGoal";
        }

        /// <summary>
        /// Si esta presa es de las que el <c>AttackGoal</c> del juego no sabe
        /// atacar: otro enemigo. El log del 24 lo dice con dos lineas que van
        /// siempre juntas - "Tried to execute new sequence while previous was
        /// already running" y "Error starting goal 'AttackGoal' init sequence" -
        /// y con "Stanley Bundy va a por Oswald Howe" 25 veces sin un golpe: el
        /// juego no encuentra casilla de ataque junto a un enemigo, y cada
        /// reintento nuestro le cortaba el arranque al anterior.
        /// </summary>
        internal static bool IsFellowEnemy(CreatureBase prey)
        {
            var human = prey as HumanoidInstance;
            return human != null && human.IsEnemy();
        }

        /// <summary>
        /// Contra otro enemigo no se da orden de ataque, se da orden de
        /// <b>seguirle</b>: <c>MoveOrder(criatura)</c> es la misma que usa el
        /// comandante para escoltar y lleva al cuerpo a dos casillas de la
        /// presa y lo mantiene ahi. El golpe lo pone <see cref="RisenBrawl"/>,
        /// que pega a quien este a 2,2 o menos. El setter de CurrentOrder ya
        /// aborta el plan si hace falta; abortar a mano encima es lo que
        /// rompia el arranque.
        /// </summary>
        internal static void Shadow(HumanoidInstance hunter, CreatureBase prey)
        {
            var current = hunter?.EnemyBehaviour?.CurrentOrder as MoveOrder;
            if (current != null && ReferenceEquals(current.FollowCreature, prey)) return;

            Order(hunter, new MoveOrder(prey, 0f));
            if (hunter != null) Shadowing.Add(hunter);
        }

        /// <summary>Quien lleva una orden de seguir puesta por <see cref="Shadow"/>.</summary>
        private static readonly HashSet<HumanoidInstance> Shadowing = new HashSet<HumanoidInstance>();

        /// <summary>
        /// Quita la orden de seguir a quien ya no tiene a quien seguir. Si no,
        /// un saqueador cuyo alzado ha caido se quedaba con una orden que no
        /// se puede cumplir hasta que su comandante le diera otra.
        /// </summary>
        internal static void ReleaseShadows()
        {
            if (Shadowing.Count == 0) return;

            foreach (var hunter in new List<HumanoidInstance>(Shadowing))
            {
                var order = hunter?.EnemyBehaviour?.CurrentOrder as MoveOrder;
                var prey = order?.FollowCreature;

                if (hunter == null || hunter.HasDisposed || hunter.HasDied || prey == null)
                {
                    Shadowing.Remove(hunter);
                    continue;
                }

                if (!prey.HasDisposed && !prey.HasDiedOrFainted) continue;

                Order(hunter, null);
                Shadowing.Remove(hunter);
            }
        }

        private static void Look(HumanoidInstance walker)
        {
            var ai = walker.CombatAi;
            if (ai == null) return;

            var prey = Nearest(walker);

            if (prey != null)
            {
                Engage(walker, ai, prey);
                return;
            }

            // Nada en el radio. Primero lo que otro haya visto, y si nadie ha
            // visto nada, andar.
            if (Answer(walker, ai)) return;

            Roam(walker, ai);
        }

        // --- 1. el radio -------------------------------------------------------

        /// <summary>
        /// Lo mas cercano que este alzado puede atacar dentro del radio, con el
        /// colono ganando los empates.
        ///
        /// Se recorren las tres listas que juntas son "todo lo vivo que hay en el
        /// mapa" - el censo de la aldea, el de los NPC y el de los animales -
        /// porque el sensor del juego no es ninguna de ellas, y era justamente lo
        /// que se quedaba corto.
        /// </summary>
        private static CreatureBase Nearest(HumanoidInstance walker)
        {
            var limit = Radius * Radius;

            var mine = walker.CombatAi?.GetState<IDamageTakingAgent>(CombatAiState.PreferedTarget);

            CreatureBase best = null;
            var bestRank = int.MaxValue;
            var bestReach = int.MaxValue;

            // Lo mas cercano que aun tenga sitio; si todo esta lleno, lo mas
            // cercano igual, para que el alzado se acerque y espere turno en
            // vez de quedarse donde esta.
            CreatureBase crowded = null;
            var crowdedReach = int.MaxValue;

            foreach (var body in Census.Everything())
            {
                if (!Worth(walker, body)) continue;

                var reach = Reach(walker, body);
                if (reach > limit) continue;

                if (!ReferenceEquals(body, mine) && ClaimsOn(body) >= MaxPerPrey)
                {
                    if (reach < crowdedReach)
                    {
                        crowded = body;
                        crowdedReach = reach;
                    }
                    continue;
                }

                var rank = Rank(body);
                if (rank > bestRank) continue;
                if (rank == bestRank && reach >= bestReach) continue;

                best = body;
                bestRank = rank;
                bestReach = reach;
            }

            return best ?? crowded;
        }

        /// <summary>Colono primero, lo demas despues. Ese es todo el orden.</summary>
        private static int Rank(CreatureBase body)
        {
            var human = body as HumanoidInstance;
            return human != null && human.WorkerBehaviour != null ? 0 : 1;
        }

        /// <summary>
        /// Si esto es presa para este alzado.
        ///
        /// Vivo, que no sea el mismo, que no sea de los nuestros, y que la
        /// mascara de ataque lo admita - la mascara sigue teniendo la ultima
        /// palabra, que es lo que deja a un alzado inconsciente sin atacar nada.
        /// </summary>
        private static bool Worth(HumanoidInstance walker, CreatureBase body)
        {
            if (body == null || body.HasDisposed || body.HasDiedOrFainted) return false;
            if (ReferenceEquals(body, walker)) return false;

            var human = body as HumanoidInstance;
            if (human != null && Census.IsUndead(human)) return false;

            var prey = body as IDamageTakingAgent;
            if (prey == null) return false;

            return (walker.CanAttackTypes() & prey.DamageAgentType) != DamageTakingAgentType.None;
        }

        /// <summary>
        /// Le pone la presa delante y le quita lo que tuviera entre manos.
        ///
        /// No se toca a quien ya esta con algo vivo y valido: cambiarle el
        /// objetivo en cada barrido es un alzado que gira sobre si mismo y no
        /// pega nunca. La excepcion es el desempate que se pidio - si esta con
        /// otra cosa y aparece un colono, se cambia.
        /// </summary>
        private static void Engage(HumanoidInstance walker, CombatAiAgent ai, CreatureBase prey)
        {
            var held = ai.GetState<IDamageTakingAgent>(CombatAiState.PreferedTarget);
            var body = held as CreatureBase;

            if (body != null && Worth(walker, body) && Reach(walker, body) <= Radius * Radius
                && Busy(walker, held))
            {
                // Ya esta con alguien, de verdad. Solo se le mueve por un colono.
                if (Rank(body) <= Rank(prey)) return;
            }

            if (!Due(walker, prey)) return;

            Claim(body, -1);
            Claim(prey, 1);

            RisenGoalState.Set(ai, CombatAiState.PreferedTarget, prey as IDamageTakingAgent);
            RisenGoalState.Set(ai, CombatAiState.NextTarget, null);
            RisenGoalState.Set(ai, CombatAiState.NextTargetValidated, false);

            // La orden tambien, o el "ataca esa habitacion" del comandante lo
            // devuelve al muro del que acaba de salir.
            if (IsFellowEnemy(prey))
            {
                Shadow(walker, prey);
            }
            else
            {
                Order(walker, new AttackOrder(prey as IDamageTakingAgent, false));
                walker.GoapAgent?.Abort();
            }

            Shout(walker);

            GMPlugin.Log?.LogInfo(
                $"[risen] {GameAccess.Name(walker)} va a por {GameAccess.Name(prey)}"
                + (held is BaseBuildingInstance ? " y suelta lo que estaba rompiendo" : ""));
        }

        // --- 2. andar a proposito ---------------------------------------------

        /// <summary>Cada cuanto se le repite el rumbo a un alzado ocioso.</summary>
        private const float RoamSeconds = 12f;

        /// <summary>Casillas por tramo cuando no hay aldea a la que ir.</summary>
        private const float RoamStride = 14f;

        private static readonly Dictionary<int, float> Roamed = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> Heading = new Dictionary<int, float>();

        /// <summary>
        /// Un alzado sin nada que hacer camina, y camina a algun sitio.
        ///
        /// Hacia la aldea si hay aldea: un colono cualquiera sirve de faro y asi
        /// no hace falta saber donde acaba el pueblo. Si no queda nadie, un rumbo
        /// propio que gira ochenta grados cada vez que se repite, que es la
        /// diferencia entre patrullar y temblar en el sitio.
        /// </summary>
        private static void Roam(HumanoidInstance walker, CombatAiAgent ai)
        {
            if (!(GMPlugin.UndeadRoam?.Value ?? true)) return;
            if (Busy(walker, ai)) return;

            float last;
            if (Roamed.TryGetValue(walker.UniqueId, out last)
                && Heartbeat.Now - last < RoamSeconds) return;

            Roamed[walker.UniqueId] = Heartbeat.Now;

            var beacon = Village(walker);
            if (beacon.HasValue)
            {
                Order(walker, new MoveOrder(beacon.Value, 0f));
                return;
            }

            float bearing;
            if (!Heading.TryGetValue(walker.UniqueId, out bearing))
            {
                bearing = walker.UniqueId % 360;
            }

            bearing = (bearing + 80f) % 360f;
            Heading[walker.UniqueId] = bearing;

            var radians = bearing * Mathf.Deg2Rad;
            var to = walker.GetPosition()
                     + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * RoamStride;

            Order(walker, new MoveOrder(to, 0f));
        }

        /// <summary>Donde esta el colono mas cercano, sin limite de distancia.</summary>
        private static Vector3? Village(HumanoidInstance walker)
        {
            if (!NSEipix.Base.MonoSingleton<WorkerManager>.IsInstantiated()) return null;

            CreatureBase best = null;
            var bestReach = int.MaxValue;

            foreach (var settler in WorkerManager.WorkersHere)
            {
                if (!Worth(walker, settler)) continue;

                var reach = Reach(walker, settler);
                if (reach >= bestReach) continue;

                best = settler;
                bestReach = reach;
            }

            return best == null ? (Vector3?)null : best.GetPosition();
        }

        // --- 3. avisar ---------------------------------------------------------

        private sealed class Alert
        {
            internal Vector3 Where;
            internal float When;
        }

        private static readonly Dictionary<int, Alert> Alerts = new Dictionary<int, Alert>();

        /// <summary>Cuanto vale un aviso, en segundos reales.</summary>
        private static float AlertSeconds
        {
            get { return GMPlugin.UndeadAlertSeconds?.Value ?? 45f; }
        }

        /// <summary>
        /// Hasta donde se oye, en casillas. Mas ancho que el radio de caza a
        /// proposito: el aviso es para los que **no** ven nada, y si solo llegase
        /// a quien ya esta dentro del radio no diria nada que ese no supiera.
        /// </summary>
        private static int AlertReach
        {
            get { return GMPlugin.UndeadAlertReach?.Value ?? 60; }
        }

        private static void Shout(HumanoidInstance walker)
        {
            Alerts[walker.UniqueId] = new Alert
            {
                Where = walker.GetPosition(),
                When = Heartbeat.Now,
            };
        }

        /// <summary>Se tiran los avisos viejos, que si no la lista solo crece.</summary>
        private static void Forget()
        {
            if (Alerts.Count == 0) return;

            var stale = new List<int>();
            foreach (var pair in Alerts)
            {
                if (Heartbeat.Now - pair.Value.When > AlertSeconds) stale.Add(pair.Key);
            }

            for (var i = 0; i < stale.Count; i++) Alerts.Remove(stale[i]);
        }

        /// <summary>
        /// Ir a donde otro esta pegandose. Devuelve si se ha hecho algo.
        ///
        /// Se va a la posicion y no al objetivo: para cuando este llegue, la
        /// presa del otro puede estar muerta, huida o ser otra. Lo que no cambia
        /// es que alli habia algo, y al llegar, el radio de la regla 1 lo dira.
        /// </summary>
        private static bool Answer(HumanoidInstance walker, CombatAiAgent ai)
        {
            if (!(GMPlugin.UndeadAlert?.Value ?? true)) return false;
            if (Alerts.Count == 0) return false;
            if (Busy(walker, ai)) return false;

            float last;
            if (Roamed.TryGetValue(walker.UniqueId, out last)
                && Heartbeat.Now - last < RoamSeconds) return false;

            var limit = (float)AlertReach * AlertReach;
            var here = walker.GetPosition();

            Alert best = null;
            var bestGap = float.MaxValue;

            foreach (var pair in Alerts)
            {
                if (pair.Key == walker.UniqueId) continue;

                var gap = (pair.Value.Where - here).sqrMagnitude;
                if (gap > limit || gap >= bestGap) continue;

                best = pair.Value;
                bestGap = gap;
            }

            if (best == null) return false;

            // Ya esta encima: no hay nada que andar, y si hubiera algo vivo alli
            // la regla 1 lo habria cogido antes de llegar hasta aqui.
            if (bestGap < 4f) return false;

            Roamed[walker.UniqueId] = Heartbeat.Now;
            Order(walker, new MoveOrder(best.Where, 0f));

            GMPlugin.Log?.LogInfo(
                $"[risen] {GameAccess.Name(walker)} acude al aviso, a "
                + $"{Mathf.Sqrt(bestGap):0.#} casillas");

            return true;
        }

        // --- lo comun ----------------------------------------------------------

        /// <summary>
        /// Si este alzado ya tiene algo entre manos que no conviene pisar: una
        /// presa elegida, o una orden de ataque en marcha.
        /// </summary>
        private static bool Busy(HumanoidInstance walker, CombatAiAgent ai)
        {
            var held = ai.GetState<IDamageTakingAgent>(CombatAiState.PreferedTarget) as CreatureBase;
            if (held != null && !held.HasDisposed && !held.HasDiedOrFainted) return true;


            var enemy = walker.ActiveBehaviour as EnemyBehaviour;
            return enemy != null && enemy.CurrentOrder is AttackOrder;
        }

        private static void Order(HumanoidInstance walker, OrderBase order)
        {
            var enemy = walker.ActiveBehaviour as EnemyBehaviour;
            if (enemy == null) return;

            try
            {
                enemy.CurrentOrder = order;
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[risen] no se le pudo dar la orden: {e.Message}");
            }
        }

        private static int Reach(CreatureBase walker, CreatureBase prey)
        {
            var a = walker.GetGridPosition();
            var b = prey.GetGridPosition();

            var dx = a.x - b.x;
            var dy = a.y - b.y;
            var dz = a.z - b.z;

            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>A quien se le cambio el objetivo, por quien y cuando.</summary>
        private static readonly Dictionary<int, System.Tuple<int, float>> Engaged =
            new Dictionary<int, System.Tuple<int, float>>();

        /// <summary>
        /// Cuanto se deja en paz a un alzado despues de senalarle una presa.
        ///
        /// Senalarla es una peticion, no un resultado: el planificador vuelve a
        /// elegir en el siguiente tic y puede elegir lo mismo de antes. Repetirlo
        /// dos segundos despues no ayuda - si la caza iba a prender, prendio - y
        /// en una partida llego a repetirse 234 veces seguidas para un alzado y
        /// una rata. Una presa **distinta** si es un hecho nuevo y tiene su turno.
        /// </summary>
        /// <summary>
        /// Cada cuanto se le puede repetir la misma presa a un alzado que no
        /// esta con ella. Eran 12 s; con <see cref="Busy"/> ya no se le repite a
        /// uno que esta andando hacia ella, asi que esto solo mide cuanto tarda
        /// en volver a intentarlo uno al que el ataque se le cayo.
        /// </summary>
        private const float RetrySeconds = 4f;

        private static bool Due(HumanoidInstance walker, CreatureBase prey)
        {
            var now = Heartbeat.Now;
            var id = walker.UniqueId;

            System.Tuple<int, float> last;
            if (Engaged.TryGetValue(id, out last)
                && last.Item1 == prey.UniqueId
                && now - last.Item2 < RetrySeconds)
            {
                return false;
            }

            Engaged[id] = System.Tuple.Create(prey.UniqueId, now);
            return true;
        }
    }
}
