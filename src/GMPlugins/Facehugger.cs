using System;
using System.Collections.Generic;
using NSEipix.Base;
using NSEipix.Repository;
using NSMedieval;
using NSMedieval.Manager;
using NSMedieval.Model;
using NSMedieval.Repository;
using NSMedieval.State;
using NSMedieval.StatsSystem;
using NSMedieval.Types;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// El huevo, el abrazacaras y lo que sale de la victima 48 horas despues.
    ///
    /// <b>Las tres decisiones, tomadas por quien manda y escritas aqui para no
    /// volver a discutirlas.</b> El huevo es un <b>animal</b> - asi se le puede
    /// disparar de lejos sin inventar nada, porque a un animal el juego ya sabe
    /// dispararle -. La victima se mantiene viva <b>parando las estadisticas
    /// que bajan</b> - hambre, sed, sueno, frio y calor -, no curandola: se le
    /// devuelven al lado sano cada barrido, que es lo que impide que se muera
    /// de hambre debajo del bicho. Y el abrazacaras <b>muere con ella</b> a las
    /// 48 horas, que es cuando sale el xeno.
    ///
    /// <b>El reloj es de juego, no de reloj.</b> <c>WorldDate.Current</c> lleva
    /// <c>days</c> y <c>minutes</c> y dice cuantos minutos tiene un dia, asi
    /// que las 48 horas son las mismas vaya la velocidad a la que vaya, y una
    /// partida guardada y vuelta a cargar no reinicia la cuenta.
    ///
    /// <b>Lo que hace falta y todavia no esta:</b> los dos animales. Este
    /// fichero no los inventa - los busca por id, y si no estan no hace nada y
    /// lo dice una vez -. Los modelos estan en `Assets globales/Modelo 3d`
    /// (`alien-egg.zip`, `facehugger-ps1.zip`) y entran por la misma tuberia
    /// que el Corredor: Blender -> bundle -> <c>MeshRepository</c>.
    /// </summary>
    internal static class Facehugger
    {
        internal const string EggId = "aldrich_xeno_egg";
        internal const string HuggerId = "aldrich_facehugger";

        /// <summary>Lo que sale de la victima. El Corredor ya es el xeno de cuatro patas.</summary>
        internal const string HatchlingId = "xeno_runner";

        /// <summary>
        /// Medio segundo y no uno: a un segundo el abrazacaras llegaba a dar un
        /// par de mordiscos con su IA de lobo antes de que el barrido lo viera
        /// al lado, y eso era el "se pone a pegar al enemigo".
        /// </summary>
        private const float SweepSeconds = 0.5f;

        /// <summary>
        /// A esto salta a la cara, en casillas. Por encima del alcance de un
        /// mordisco, para que salte antes de pelear.
        /// </summary>
        private const float Pounce = 2.4f;

        /// <summary>Y a esto nota un huevo que se acerca alguien.</summary>
        private const float EggSenses = 4f;

        /// <summary>
        /// Lo que tarda un huevo en decidirse, en segundos de juego (se para
        /// con la pausa). Si quien se acerco se va antes, la cuenta se pierde.
        /// </summary>
        private const float EggPatience = 10f;

        /// <summary>
        /// Lo que dura el meneo del huevo al abrirse. El abrazacaras sale al
        /// final: el huevo se mueve solo mientras sale, ni antes ni despues.
        /// </summary>
        internal const float HatchSeconds = 1.2f;

        /// <summary>El estado de la victima, en XenomorphRunner/Effectors.json.</summary>
        private const string LatchedId = "FacehuggerLatched";

        private static readonly StatType[] Frozen =
        {
            StatType.Hunger, StatType.Dryness, StatType.Sleep,
            StatType.TemperatureCold, StatType.TemperatureHot,
        };

        /// <summary>Victima -> el bicho que lleva encima y el minuto en que se le subio.</summary>
        private static readonly Dictionary<int, Ride> Riding = new Dictionary<int, Ride>();

        private sealed class Ride
        {
            internal AnimalInstance Hugger;
            internal long Since;
        }

        /// <summary>Huevo -> desde cuando (Time.time) tiene a alguien delante.</summary>
        private static readonly Dictionary<int, float> Watching = new Dictionary<int, float>();

        /// <summary>Huevos que ya se estan abriendo, para no abrirlos dos veces.</summary>
        private static readonly HashSet<int> Opening = new HashSet<int>();

        private static bool saidMissing;

        /// <summary>Si lleva un abrazacaras en la cara.</summary>
        internal static bool IsHost(CreatureBase body)
        {
            return body != null && Riding.ContainsKey(body.UniqueId);
        }

        internal static void Start()
        {
            GMPlugin.Every("facehugger sweep", SweepSeconds, Sweep);
        }

        /// <summary>El minuto de la partida, y -1 fuera de ella.</summary>
        private static long Now()
        {
            // MinutesTotal y no los campos: `days` y `minutes` estan ahi pero no
            // son publicos, y lo que el juego expone ya es la cuenta hecha.
            var date = WorldDate.Current;
            return date == null ? -1 : date.MinutesTotal;
        }

        /// <summary>Horas de juego que le quedan a la victima.</summary>
        private static float HoursLeft(long elapsed, long deadline)
        {
            var date = WorldDate.Current;
            var minutesInHour = date == null ? 60 : Mathf.Max(1, date.MinutesInHour);
            return Mathf.Max(0f, (deadline - elapsed) / (float)minutesInHour);
        }

        /// <summary>Las horas de gestacion, contadas en minutos de este juego.</summary>
        private static long Deadline()
        {
            var date = WorldDate.Current;
            var hours = Mathf.Max(1f, GMPlugin.FacehuggerHours?.Value ?? 48f);
            var minutesInHour = date == null ? 60 : Mathf.Max(1, date.MinutesInHour);

            return (long)(hours * minutesInHour);
        }

        private static void Sweep()
        {
            if (!(GMPlugin.FacehuggerEnabled?.Value ?? true)) return;

            var now = Now();
            if (now < 0) return;

            if (Repository<AnimalBaseRepository, Animal>.Instance?.GetByID(HuggerId) == null)
            {
                if (!saidMissing)
                {
                    saidMissing = true;
                    GMPlugin.Log?.LogInfo(
                        $"[hugger] '{HuggerId}' no esta en ningun AnimalBase.json cargado: "
                        + "el barrido corre pero no tiene con que trabajar");
                }
                return;
            }

            Hold(now, Deadline());
            Hunt();
            Hatch();
        }

        /// <summary>
        /// Lo que le pasa a quien ya lo lleva puesto: sigue inconsciente, no le
        /// bajan las barras, y a las 48 horas se acaba.
        /// </summary>
        private static void Hold(long now, long deadline)
        {
            if (Riding.Count == 0) return;

            foreach (var pair in new List<KeyValuePair<int, Ride>>(Riding))
            {
                var victim = Body(pair.Key);
                var ride = pair.Value;

                if (victim == null || victim.HasDisposed || victim.HasDied)
                {
                    // Sin cara a la que agarrarse, el bicho vuelve a ser un bicho.
                    Riding.Remove(pair.Key);
                    Loosen(ride.Hugger);
                    continue;
                }

                if (!Standing(ride.Hugger))
                {
                    // Le han matado el bicho de encima: se despierta y se queda
                    // como estaba, que es la unica forma de salvar a alguien.
                    Riding.Remove(pair.Key);
                    Wake(victim);
                    GMPlugin.Log?.LogInfo($"[hugger] a {GameAccess.Name(victim)} le han quitado el bicho");
                    continue;
                }

                Freeze(victim, HoursLeft(now - ride.Since, deadline));
                Still(ride.Hugger);
                Follow(ride.Hugger, victim);

                if (now - ride.Since < deadline) continue;

                Riding.Remove(pair.Key);
                Burst(victim, ride.Hugger);
            }
        }

        /// <summary>
        /// Las barras que bajan, quietas, y la consciencia en el suelo. No se
        /// cura nada: quien llega herido sigue herido, lo que no puede es
        /// morirse de hambre, de sed, de sueno ni del ambiente mientras lo
        /// lleve puesto.
        /// </summary>
        private static void Sated(CreatureBase body)
        {
            var stats = GameAccess.Stats(body);
            if (stats == null) return;

            foreach (var type in Frozen)
            {
                var stat = stats.GetStat(type);
                if (stat != null && stat.Current < stat.Max) stat.ForceCurrentValue(stat.Max);
            }
        }

        /// <summary>Lo que dura FacehuggerLatched en su JSON, en horas.</summary>
        private const float LatchedHours = 48f;

        private static void Freeze(CreatureBase victim, float hoursLeft)
        {
            Sated(victim);
            var stats = GameAccess.Stats(victim);
            if (stats != null)
            {
                var awake = stats.GetStat(StatType.Consciousness);
                if (awake != null && awake.Current > 0f) awake.ForceCurrentValue(0f);

                // Lo que se lee en su ficha: inconsciente, por que, y cuanto le
                // queda. La cuenta atras es la del propio efector: dura 48 h en
                // el JSON y se arranca escalado a lo que falta de verdad.
                if (!stats.IsEffectorActive(LatchedId) && hoursLeft > 0f)
                {
                    stats.StartEffector(LatchedId, hoursLeft / LatchedHours, false, -1, null);
                }
            }

            // La consciencia a cero tumba a un colono, pero no a un saqueador
            // ni a un animal: esos no leen esa barra para caerse. El desmayo
            // del juego si los tumba a todos - GameAccess.Faint explica por
            // que hay que llamarlo por el tipo real -, y se repite si alguien
            // se levanta con el bicho aun puesto.
            if (!victim.HasFainted) GameAccess.Faint(victim);

            // Y un animal, ademas, quieto: el 27 los efectos estaban puestos y
            // algunos seguian andando - su IA de huida y su camino a medias no
            // leen el desmayo. Se les para igual que al bicho.
            if (victim is AnimalInstance) Still(victim);
        }

        /// <summary>
        /// El bicho subido no hace nada mas: sin plan, sin IA de combate, sin
        /// camino, sin objetivo. Si no, su IA de lobo seguia mordiendo a quien
        /// tuviera al lado con la vista pegada a otra cara.
        /// </summary>
        private static void Still(CreatureBase hugger)
        {
            try
            {
                hugger.GetGoapAgent()?.StopTicker();
                hugger.CombatAi?.StopTicker();
                hugger.PathDriver?.Abort();
                if (hugger.GetTarget() != null) hugger.SetTarget(null);
            }
            catch (Exception)
            {
                // Uno que el juego esta quitando: el barrido siguiente lo suelta.
            }
        }

        /// <summary>Lo contrario de <see cref="Still"/>, para uno que se queda sin cara.</summary>
        private static void Loosen(CreatureBase hugger)
        {
            if (hugger == null || hugger.HasDisposed || hugger.HasDied) return;

            try
            {
                hugger.GetGoapAgent()?.StartTicker();
                hugger.CombatAi?.StartTicker();
            }
            catch (Exception)
            {
                // Idem.
            }
        }

        /// <summary>
        /// Quien se libra del bicho se levanta. Un colono se levantaba solo al
        /// volverle la consciencia; a un saqueador o a un animal tumbado con
        /// el desmayo del juego hay que levantarlo, o se queda en el suelo
        /// hasta que acabe la incursion.
        /// </summary>
        private static void Wake(CreatureBase victim)
        {
            if (victim == null || victim.HasDisposed || victim.HasDied) return;
            GameAccess.ForceEndEffector(victim, LatchedId);
            if (victim is HumanoidInstance human && human.IsWorker()) return;
            if (victim.HasFainted) GameAccess.ForceUnFaint(victim);
            if (victim is AnimalInstance) Loosen(victim);
        }

        /// <summary>
        /// El bicho va donde va la cara, el cuerpo y la vista. El cuerpo
        /// tambien, porque es al cuerpo a lo que se le pega: con solo la vista
        /// movida, quien queria salvar a la victima se iba a pegarle al suelo
        /// donde el bicho habia saltado.
        /// </summary>
        private static void Follow(AnimalInstance hugger, CreatureBase victim)
        {
            try
            {
                var at = victim.GetPosition();
                if (Vector3.Distance(hugger.GetPosition(), at) > 0.5f) hugger.PathDriver?.Teleport(at);

                var face = victim is HumanoidInstance ? 0.9f : 0.5f;
                var view = hugger.GetAgentView<NSMedieval.View.AnimatedAgentView>();
                if (view != null) view.transform.position = at + Vector3.up * face;
            }
            catch (Exception)
            {
                // Un bicho sin vista es un bicho que el juego esta quitando.
            }
        }

        /// <summary>
        /// Los abrazacaras sueltos saltan a la cara de lo primero vivo que
        /// tengan al lado - colono, saqueador, visita o animal -, y uno por
        /// cara. Nunca a uno de la colmena.
        /// </summary>
        private static void Hunt()
        {
            foreach (var beast in Loose(HuggerId))
            {
                CreatureBase prey = null;
                var closest = Pounce;
                var from = beast.GetPosition();

                foreach (var body in Census.Everything())
                {
                    if (body == null || body.HasDisposed || body.HasDied) continue;
                    if (ReferenceEquals(body, beast) || XenoAcid.IsHive(body)) continue;
                    // Un alzado no se desmaya (UndeadNeverFaints): el bicho iria
                    // de paseo en la cara de algo que sigue andando.
                    if (body is HumanoidInstance walker && Census.IsUndead(walker)) continue;
                    // Solo colonos e incursores. Un negociador tumbado (el 27,
                    // Wluyua Court, sentado esperando) hace que el juego intente
                    // desterrarlo como a un colono: NRE en BanishGoal.OnStart.
                    // Comerciantes, visitas y prisioneros van por el mismo camino.
                    if (body is HumanoidInstance person
                        && !(person.ActiveBehaviour is WorkerBehaviour || person.ActiveBehaviour is EnemyBehaviour)) continue;
                    if (Riding.ContainsKey(body.UniqueId)) continue;

                    var far = Vector3.Distance(from, body.GetPosition());
                    if (far >= closest) continue;

                    closest = far;
                    prey = body;
                }

                if (prey == null) continue;

                Riding[prey.UniqueId] = new Ride { Hugger = beast, Since = Now() };
                Still(beast);
                Freeze(prey, HoursLeft(0, Deadline()));
                Follow(beast, prey);
                GMPlugin.Log?.LogInfo($"[hugger] uno se le ha subido a la cara a {GameAccess.Name(prey)}");
            }
        }

        /// <summary>
        /// Un huevo con alguien delante espera <see cref="EggPatience"/>
        /// segundos. Si sigue habiendo alguien, se menea y del meneo sale el
        /// abrazacaras; si se ha ido, la cuenta vuelve a cero.
        /// </summary>
        private static void Hatch()
        {
            var now = Time.time;

            foreach (var egg in Loose(EggId))
            {
                var id = egg.UniqueId;

                // Un huevo no anda: con la IA de lobo se paseaba despacio. Sin
                // plan ni camino, y sin hambre ni sed, que un huevo no come.
                Still(egg);
                Sated(egg);

                if (Opening.Contains(id)) continue;

                if (!Someone(egg.GetPosition()))
                {
                    Watching.Remove(id);
                    continue;
                }

                float since;
                if (!Watching.TryGetValue(id, out since))
                {
                    Watching[id] = now;
                    GMPlugin.Log?.LogInfo("[hugger] un huevo ha notado a alguien");
                    continue;
                }

                if (now - since < EggPatience) continue;

                Watching.Remove(id);
                Open(egg);
            }
        }

        /// <summary>Si hay alguien vivo que no sea de la colmena al lado de esto.</summary>
        private static bool Someone(Vector3 at)
        {
            foreach (var body in Census.Everything())
            {
                var human = body as HumanoidInstance;
                if (human == null || human.HasDisposed || human.HasDied) continue;
                if (Vector3.Distance(at, human.GetPosition()) <= EggSenses) return true;
            }

            return false;
        }

        /// <summary>
        /// El meneo y, al final del meneo, el bicho. El huevo muere cuando el
        /// bicho ya esta fuera, no antes.
        /// </summary>
        private static void Open(AnimalInstance egg)
        {
            var id = egg.UniqueId;
            Opening.Add(id);

            try
            {
                var shell = egg.GetAgentView<NSMedieval.View.AnimatedAgentView>()?.GetComponent<HiveBody>();
                shell?.Hatch(HatchSeconds);
            }
            catch (Exception)
            {
                // Sin vista se abre igual, solo que sin meneo.
            }

            GMPlugin.RunAfter(HatchSeconds, () =>
            {
                Opening.Remove(id);
                if (!Standing(egg)) return;
                if (Spawn(HuggerId, egg.GetPosition()) == null) return;

                Kill(egg);
                GMPlugin.Log?.LogInfo("[hugger] un huevo se ha abierto");
            });
        }

        /// <summary>
        /// Las 48 horas: se muere la victima, se muere el bicho, y sale el xeno
        /// donde estaba el pecho.
        /// </summary>
        private static void Burst(CreatureBase victim, AnimalInstance hugger)
        {
            try
            {
                var at = victim.GetPosition();

                GameAccess.DrainStat(victim, StatType.Health, 1f);
                Kill(hugger);

                var born = Spawn(HatchlingId, at);

                GMPlugin.Log?.LogInfo(born == null
                    ? $"[hugger] {GameAccess.Name(victim)} ha muerto y el xeno no ha salido: "
                      + $"'{HatchlingId}' no esta cargado"
                    : $"[hugger] de {GameAccess.Name(victim)} ha salido un xeno");
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[hugger] burst failed: {e}");
            }
        }

        private static AnimalInstance Spawn(string id, Vector3 at)
        {
            try
            {
                if (!MonoSingleton<AnimalManager>.IsInstantiated()) return null;
                if (Repository<AnimalBaseRepository, Animal>.Instance?.GetByID(id) == null) return null;

                // Fase 0 al principio: lo que sale del pecho es una cria. Con
                // (1, 1f) el Corredor nacia al 100% de su ultima fase y el juego
                // lo mataba de viejo a los dos segundos ("salen muertos", el 25).
                var born = MonoSingleton<AnimalManager>.Instance.SpawnAnimal(
                    id, at, BodyType.Male, 0, 0.05f, false,
                    AnimalType.WildAggressive, null, null, null, "", 0, 0);

                if (born != null) GMPlugin.RunAfter(0.5f, () => born.GetGoapAgent()?.StartTicker());
                return born;
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[hugger] could not spawn '{id}': {e.Message}");
                return null;
            }
        }

        private static void Kill(AnimalInstance beast)
        {
            if (!Standing(beast)) return;
            GameAccess.DrainStat(beast, StatType.Health, 1f);
        }

        /// <summary>Los de ese id que estan en pie y no van subidos a nadie.</summary>
        private static IEnumerable<AnimalInstance> Loose(string id)
        {
            foreach (var body in Census.Everything())
            {
                var beast = body as AnimalInstance;
                if (!Standing(beast)) continue;
                if (beast.Id != id && beast.Blueprint?.GetID() != id) continue;
                if (Mounted(beast)) continue;

                yield return beast;
            }
        }

        private static bool Mounted(AnimalInstance beast)
        {
            foreach (var ride in Riding.Values)
            {
                if (ReferenceEquals(ride.Hugger, beast)) return true;
            }

            return false;
        }

        private static bool Standing(AnimalInstance beast)
        {
            return beast != null && !beast.HasDisposed && !beast.HasDied;
        }

        private static CreatureBase Body(int uniqueId)
        {
            foreach (var body in Census.Everything())
            {
                if (body != null && body.UniqueId == uniqueId) return body;
            }

            return null;
        }
    }
}
