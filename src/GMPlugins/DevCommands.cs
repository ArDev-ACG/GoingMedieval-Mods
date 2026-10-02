using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NSEipix.Base;
using NSEipix.Repository;
using NSMedieval;
using NSMedieval.DevConsole;
using NSMedieval.Manager;
using NSMedieval.Model;
using NSMedieval.Repository;
using NSMedieval.State;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Los comandos de consola que faltaban para probar los mods sin esperar a
    /// que el juego decida.
    ///
    /// <b>Lo que vanilla ya trae, y por eso no se repite aqui.</b> Antes de
    /// escribir uno solo se leyo la lista entera, 131 comandos:
    ///
    ///   - <c>spawnAnimal &lt;id&gt; &lt;n&gt; &lt;sexo&gt; &lt;fase&gt; &lt;%&gt;</c>
    ///     ya invoca al xeno, pero no lo <em>coloca</em>: arma un modo de clic y
    ///     hay que cerrar la consola y hacer clic una vez por bicho. De ahi que
    ///     <c>aldrichXeno</c> deje de delegar en el y ponga el corro donde
    ///     apunta el raton, igual que <c>aldrichHorde</c>.
    ///   - <c>spawnRandomResources &lt;id&gt; &lt;n&gt;</c> deja montones al
    ///     hacer clic y vale para los nuestros.
    ///   - <c>autoconstruct</c> con el menu de construccion pone cualquiera de
    ///     nuestros edificios al instante, asi que **no hay comando de
    ///     edificios**: el que ya existe es mejor que uno nuestro.
    ///
    /// <b>Y lo que no trae.</b> La consola puede <em>desbloquear</em> un evento
    /// (<c>unlockEvent</c>) y no puede <em>lanzarlo</em>: no hay un solo
    /// comando que llame a <c>GameEventSystem.StartEvent</c>, asi que la
    /// incursion de xenos y la de ratas solo se veian esperando a que el reloj
    /// del juego las eligiera. Y <c>spawnNPC</c> invoca de uno en uno, con el
    /// nombre de la clase de comportamiento escrito a mano y en la faccion del
    /// <em>sitio</em>, que es la del jugador: una horda de ocho asi son ocho
    /// comandos y ninguno hostil.
    ///
    /// <b>Como entran.</b> <c>AddAllCommands</c> es una lista escrita a mano en
    /// el juego, no un barrido por reflexion, asi que declarar la clase no
    /// basta: hay que anadirse detras, que es lo que hace el postfijo. El
    /// diccionario rechaza duplicados en silencio, de modo que volver a entrar
    /// a partida no rompe nada.
    ///
    /// Todos empiezan por <c>aldrich</c> para que <c>list aldrich</c> los saque
    /// de una vez, y todos escriben en el log de BepInEx ademas de en la
    /// consola: lo que se ve por pantalla se pierde al cerrarla.
    /// </summary>
    [HarmonyPatch]
    internal static class DevCommands
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(DeveloperConsoleController), "AddAllCommands");
        }

        private static void Postfix(DeveloperConsoleController __instance)
        {
            if (__instance == null) return;

            try
            {
                Add(__instance, new AldrichList());
                Add(__instance, new AldrichEvent());
                Add(__instance, new AldrichHorde());
                Add(__instance, new AldrichXeno());
                Add(__instance, new AldrichGive());
                Add(__instance, new AldrichHive());

                GMPlugin.Log?.LogInfo("[dev] comandos aldrich* anadidos a la consola");
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[dev] no se pudieron anadir los comandos: {e}");
            }
        }

        /// <summary>
        /// Mete un comando en el diccionario del juego.
        ///
        /// Por la sobrecarga de dos argumentos y no por la de uno: la corta no
        /// es accesible desde fuera del ensamblado, y esta ademas deja escrito
        /// con que nombre entra cada uno. El diccionario ignora duplicados en
        /// silencio, asi que volver a cargar partida no apila comandos.
        /// </summary>
        private static void Add(DeveloperConsoleController console, ConsoleCommand command)
        {
            console.AddCommand(command.Command, command);
        }

        /// <summary>
        /// Escribe en la consola y en el log a la vez.
        ///
        /// La consola se vacia al cerrarla y el log sobrevive a la partida, que
        /// es donde se mira cuando algo no salio. El metodo que pinta la linea
        /// no es publico, asi que va por reflexion y **sin morirse si no
        /// esta**: la consola es un lujo, el log es el registro.
        /// </summary>
        internal static void Say(string line)
        {
            GMPlugin.Log?.LogInfo("[dev] " + line);

            try
            {
                if (!MonoSingleton<DeveloperConsoleController>.IsInstantiated()) return;
                var controller = MonoSingleton<DeveloperConsoleController>.Instance;
                if (controller == null) return;

                controller.ReturnCommandResult(line, ConsoleMessageType.Standard);
            }
            catch
            {
                // Ver arriba: la linea ya esta en el log.
            }
        }

        /// <summary>Donde apunta el raton, sobre el suelo o sobre lo construido.</summary>
        internal static bool Aim(out Vector3 point)
        {
            point = Vector3.zero;

            var camera = Camera.main;
            if (camera == null) return false;

            // Las mismas cuatro capas que mira `spawnNPC`, y por la misma
            // razon: un clic sobre un tejado tiene que valer igual que uno
            // sobre la hierba.
            var mask = (1 << LayerMask.NameToLayer("VoxelMap"))
                       | (1 << LayerMask.NameToLayer("BuildingWalkable"))
                       | (1 << LayerMask.NameToLayer("RaycastPlaneHelper"))
                       | (1 << LayerMask.NameToLayer("VoxelMapPathfinding"));

            RaycastHit hit;
            if (!Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition), out hit,
                                 Mathf.Infinity, mask)) return false;

            point = hit.point;
            return true;
        }
    }

    // ----------------------------------------------------------------------

    /// <summary>
    /// Escupe los ids de los mods, que es lo que nadie se acuerda.
    ///
    /// Nace de una perdida de tiempo concreta: <c>game_event_xeno_runner_raid</c>
    /// no se escribe de memoria, y equivocarse en una letra da el mismo
    /// silencio que un evento que no arranca.
    /// </summary>
    internal sealed class AldrichList : ConsoleCommand
    {
        public override string Command { get; protected set; }
        public override string Description { get; protected set; }
        public override string Help { get; protected set; }

        public AldrichList()
        {
            Command = "aldrichList";
            Description = "Lista los ids de los mods de Aldrich.";
            Help = "aldrichList [eventos|horda|objetos|animales] - sin argumento los saca todos";
        }

        public void CommandMethod()
        {
            CommandMethod("");
        }

        public void CommandMethod(string what)
        {
            what = (what ?? "").ToLowerInvariant();
            var all = what.Length == 0;

            if (all || what.StartsWith("ev"))
            {
                DevCommands.Say("eventos:   game_event_xeno_runner_raid, game_event_rat_raid, "
                                + "game_event_raid_new (el de vanilla, del que salen las hordas)");
                DevCommands.Say("           player_triggered_event_blood_rite va por celebraciones, "
                                + "no por aqui");
            }

            if (all || what.StartsWith("ho") || what.StartsWith("np"))
            {
                DevCommands.Say("horda:     undead_horde_walker_easy, undead_horde_walker_med, "
                                + "undead_horde_walker_hard");
                DevCommands.Say("           undead_horde_brute_easy|med|hard (pesados), "
                                + "undead_horde_lurker_easy|med|hard (los del hueco de arquero)");
            }

            if (all || what.StartsWith("an") || what.StartsWith("xe"))
            {
                DevCommands.Say("animales:  xeno_runner");
            }

            if (all || what.StartsWith("ob") || what.StartsWith("re"))
            {
                // Las garras van con calidad y material por delante, que es como el
                // juego nombra un arma equipable de verdad: el proto `undead_claws`
                // existe, pero `EquipmentView` no sabe leerle la calidad y se queda
                // con la malla que trae el prefab, que es la daga.
                DevCommands.Say("objetos:   blood_draught, sturdy_bone_undead_claws, plague_mask, plague_hood");
                DevCommands.Say("edificios: autoconstruct + el menu de construccion; salen todos ahi");
            }
        }
    }

    // ----------------------------------------------------------------------

    /// <summary>
    /// Lanza un evento de juego ahora mismo, que es lo que la consola no sabia
    /// hacer.
    ///
    /// <c>unlockEvent</c> solo lo <em>desbloquea</em>: lo deja elegible para
    /// cuando al reloj le toque. Probar asi la incursion de xenos es esperar
    /// entre diez y veinte horas de partida y que ademas gane el sorteo de su
    /// grupo. <c>GameEventSystem.StartEvent</c> es la puerta que usa el arnes
    /// de pruebas del propio juego, <c>Testing.Autoplay.GameEventActions</c>,
    /// asi que no es una via inventada aqui.
    ///
    /// "Ya esta corriendo", "no existe" y "hay otro que bloquea" se dicen por
    /// separado, porque en pantalla los tres se parecen demasiado: nada.
    /// </summary>
    internal sealed class AldrichEvent : ConsoleCommand
    {
        public override string Command { get; protected set; }
        public override string Description { get; protected set; }
        public override string Help { get; protected set; }

        public AldrichEvent()
        {
            Command = "aldrichEvent";
            Description = "Lanza un evento de juego por id, ahora.";
            Help = "aldrichEvent <eventId>   p.ej. aldrichEvent game_event_xeno_runner_raid. "
                   + "unlockEvent solo desbloquea; esto lanza.";
        }

        public void CommandMethod()
        {
            DevCommands.Say("aldrichEvent <eventId>. Los nuestros:");
            new AldrichList().CommandMethod("eventos");
        }

        public void CommandMethod(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                CommandMethod();
                return;
            }

            if (!MonoSingleton<NSMedieval.GameEventSystem.GameEventSystem>.IsInstantiated())
            {
                DevCommands.Say("no hay partida cargada: el sistema de eventos aun no existe");
                return;
            }

            var system = MonoSingleton<NSMedieval.GameEventSystem.GameEventSystem>.Instance;

            if (system.IsEventRunning(eventId))
            {
                DevCommands.Say($"'{eventId}' ya esta corriendo");
                return;
            }

            bool started;
            try
            {
                started = system.StartEvent(eventId);
            }
            catch (Exception e)
            {
                DevCommands.Say($"'{eventId}' reviento al arrancar: {e.Message}");
                return;
            }

            DevCommands.Say(started
                ? $"'{eventId}' lanzado"
                : $"'{eventId}' no arranco - id que no existe, o hay un evento que bloquea");
        }
    }

    // ----------------------------------------------------------------------

    /// <summary>
    /// Una horda entera donde apunte el raton, y hostil de verdad.
    ///
    /// <b>Por que no vale <c>spawnNPC</c>.</b> Invoca de uno en uno, hay que
    /// escribirle el nombre de la clase de comportamiento, y - lo que importa -
    /// lo mete en la faccion del <em>sitio</em>, que es la del jugador. Un
    /// alzado en la faccion del jugador no ataca a nadie: se queda de pie.
    /// Aqui la faccion se busca por su id, <c>undead_horde</c>, que es la misma
    /// que <c>RisenPallor.IsUndead</c> reconoce, de modo que los invocados a
    /// mano heredan todo lo demas que el mod les hace: el color, la hostilidad,
    /// no retirarse, no desmayarse.
    ///
    /// Si esa faccion no esta colocada en este mundo - que es lo que pasaba
    /// cuando la Horda compartia tipo con los canibales - se dice, en vez de
    /// invocar ocho cadaveres amistosos y dejar que parezca que el mod esta
    /// roto.
    /// </summary>
    internal sealed class AldrichHorde : ConsoleCommand
    {
        private const string FactionId = "undead_horde";

        public override string Command { get; protected set; }
        public override string Description { get; protected set; }
        public override string Help { get; protected set; }

        public AldrichHorde()
        {
            Command = "aldrichHorde";
            Description = "Invoca una horda de alzados donde apunte el raton.";
            Help = "aldrichHorde [cuantos] [easy|med|hard]   por defecto 6 easy";
        }

        public void CommandMethod()
        {
            CommandMethod(6, "easy");
        }

        public void CommandMethod(int count)
        {
            CommandMethod(count, "easy");
        }

        public void CommandMethod(int count, string tier)
        {
            count = Mathf.Clamp(count, 1, 40);
            var blueprint = "undead_horde_walker_" + Tier(tier);

            if (!MonoSingleton<NPCManager>.IsInstantiated())
            {
                DevCommands.Say("no hay partida cargada");
                return;
            }

            if (Repository<NPCRepository, NPC>.Instance?.GetByID(blueprint) == null)
            {
                DevCommands.Say($"'{blueprint}' no esta en NPCs.json - mira aldrichList horda");
                return;
            }

            var faction = GameAccess.FactionByBlueprint(FactionId);
            if (faction == null)
            {
                DevCommands.Say($"la faccion '{FactionId}' no esta en este mundo, asi que los "
                                + "alzados saldrian amistosos. Hace falta partida nueva con el "
                                + "mod puesto - mira villagePlacement");
                return;
            }

            Vector3 at;
            if (!DevCommands.Aim(out at))
            {
                DevCommands.Say("apunta al mapa con el raton y vuelve a lanzarlo");
                return;
            }

            var village = MonoSingleton<NSMedieval.WorldMap.WorldMap>.Instance?
                .Data?.VillagePlaces?.FirstOrDefault();

            var setter = typeof(HumanoidInstance).GetMethod("SetActiveBehaviour");
            var born = 0;

            for (var i = 0; i < count; i++)
            {
                // En corro alrededor del punto y no todos sobre el mismo voxel:
                // apilados, el pathfinding los empuja y la mitad acaba dentro
                // de una pared.
                var angle = i * (2f * Mathf.PI / count);
                var radius = 0.8f + i * 0.12f;
                var spot = at + new Vector3(Mathf.Cos(angle) * radius, 0f,
                                            Mathf.Sin(angle) * radius);

                try
                {
                    var walker = MonoSingleton<NPCManager>.Instance.SpawnBlank(
                        blueprint, BodyType.Male, spot, village, faction, null);

                    if (walker == null) continue;

                    // Igual que `spawnNPC`: el comportamiento se pone por un
                    // metodo generico, asi que va por reflexion.
                    setter?.MakeGenericMethod(typeof(EnemyBehaviour)).Invoke(walker, null);
                    born++;
                }
                catch (Exception e)
                {
                    DevCommands.Say($"uno no salio: {e.Message}");
                    break;
                }
            }

            DevCommands.Say(born == 0
                ? "no salio ninguno"
                : $"{born} alzado(s) '{blueprint}' de la faccion {FactionId}");
        }

        private static string Tier(string tier)
        {
            tier = (tier ?? "").ToLowerInvariant();
            if (tier.StartsWith("h")) return "hard";
            if (tier.StartsWith("m")) return "med";
            return "easy";
        }
    }

    // ----------------------------------------------------------------------

    /// <summary>
    /// El Corredor, adulto, en numero y **donde apunta el raton**, igual que los
    /// alzados.
    ///
    /// Antes delegaba en <c>spawnAnimal</c> de vanilla, y eso traia dos cosas
    /// que se pidieron cambiar. La primera es la que se ve: el comando de
    /// vanilla no coloca nada, arma un modo de clic y espera - hay que cerrar la
    /// consola, apuntar y hacer clic una vez por bicho -, mientras que
    /// <c>aldrichHorde</c> los pone en corro de una vez donde este el raton.
    /// Ahora los dos se usan igual. La segunda no se veia: se le pasaba la fase
    /// de vida **2**, y el Corredor solo tiene dos fases - joven (0) y adulto
    /// (1) -, asi que el numero se salia de la lista.
    ///
    /// Se invoca con <c>AnimalType.WildAggressive</c> y no con <c>Wild</c>, que
    /// es lo que pasa el comando de vanilla: en <c>AnimalBase.json</c> las dos
    /// llevan el mismo <c>AnimalRaidAggressive</c>, pero solo la agresiva trae
    /// el <c>AnimalWildAggressiveEffector</c> que hace que ataque sin que le
    /// hayan dado primero.
    ///
    /// Y el ticker al final: el juego arranca el GOAP de un animal recien puesto
    /// medio segundo despues, fuera del spawn - <c>CommandSpawnAnimal</c> hace
    /// exactamente esto -, y sin esa llamada el bicho aparece y se queda quieto.
    /// </summary>
    /// <summary>
    /// Huevos o abrazacaras donde apunte el raton, que es la unica forma de
    /// probar las 48 horas sin esperar a que los traiga una incursion.
    ///
    /// Un huevo tiene `MovementSpeed` 0 y se queda donde cae; el abrazacaras
    /// salta a la cara de lo primero vivo que pase a casilla y media. Lo que
    /// pasa despues lo lleva <see cref="Facehugger"/>.
    /// </summary>
    internal sealed class AldrichHive : ConsoleCommand
    {
        public override string Command { get; protected set; }
        public override string Description { get; protected set; }
        public override string Help { get; protected set; }

        public AldrichHive()
        {
            Command = "aldrichHive";
            Description = "Pone huevos de alien araña o aliens araña donde apunte el raton.";
            Help = "aldrichHive [huevo|bicho] [cuantos]   por defecto huevo y 1.";
        }

        public void CommandMethod()
        {
            CommandMethod("huevo", 1);
        }

        public void CommandMethod(string what)
        {
            CommandMethod(what, 1);
        }

        public void CommandMethod(string what, int count)
        {
            var id = what != null && what.StartsWith("b")
                ? "aldrich_facehugger"
                : "aldrich_xeno_egg";

            count = Mathf.Clamp(count, 1, 20);

            if (Repository<AnimalBaseRepository, Animal>.Instance?.GetByID(id) == null)
            {
                DevCommands.Say($"'{id}' no esta cargado - falta el AnimalBase.json "
                                + "del mod XenomorphRunner, o no entro");
                return;
            }

            if (!MonoSingleton<AnimalManager>.IsInstantiated())
            {
                DevCommands.Say("no hay partida cargada");
                return;
            }

            Vector3 at;
            if (!DevCommands.Aim(out at))
            {
                DevCommands.Say("apunta al mapa con el raton y vuelve a lanzarlo");
                return;
            }

            var born = 0;

            for (var i = 0; i < count; i++)
            {
                var angle = i * (2f * Mathf.PI / count);
                var spot = at + new Vector3(Mathf.Cos(angle) * (0.8f + i * 0.12f), 0f,
                                            Mathf.Sin(angle) * (0.8f + i * 0.12f));

                try
                {
                    var beast = MonoSingleton<AnimalManager>.Instance.SpawnAnimal(
                        id, spot, BodyType.Male, 0, 0.5f, false,
                        NSMedieval.Types.AnimalType.WildAggressive, null, null, null, "", 0, 0);

                    if (beast == null) continue;

                    GMPlugin.RunAfter(0.5f, () => beast.GetGoapAgent()?.StartTicker());
                    born++;
                }
                catch (Exception e)
                {
                    DevCommands.Say($"uno no salio: {e.Message}");
                    break;
                }
            }

            DevCommands.Say(born == 0 ? "no salio ninguno" : $"{born} de '{id}'");
        }
    }

    // ----------------------------------------------------------------------

    internal sealed class AldrichXeno : ConsoleCommand
    {
        private const string AnimalId = "xeno_runner";

        /// <summary>Adulto. El Corredor solo tiene joven (0) y adulto (1).</summary>
        private const int MaturePhase = 1;

        public override string Command { get; protected set; }
        public override string Description { get; protected set; }
        public override string Help { get; protected set; }

        public AldrichXeno()
        {
            Command = "aldrichXeno";
            Description = "Invoca Corredores adultos donde apunte el raton.";
            Help = "aldrichXeno [cuantos]   por defecto 3, adultos y salvajes agresivos.";
        }

        public void CommandMethod()
        {
            CommandMethod(3);
        }

        public void CommandMethod(int count)
        {
            count = Mathf.Clamp(count, 1, 20);

            if (Repository<AnimalBaseRepository, Animal>.Instance?.GetByID(AnimalId) == null)
            {
                DevCommands.Say($"'{AnimalId}' no esta cargado - el mod XenomorphRunner no esta "
                                + "puesto, o su AnimalBase.json no entro");
                return;
            }

            if (!MonoSingleton<AnimalManager>.IsInstantiated())
            {
                DevCommands.Say("no hay partida cargada");
                return;
            }

            Vector3 at;
            if (!DevCommands.Aim(out at))
            {
                DevCommands.Say("apunta al mapa con el raton y vuelve a lanzarlo");
                return;
            }

            var born = 0;

            for (var i = 0; i < count; i++)
            {
                // En corro y no todos sobre el mismo voxel, por lo mismo que la
                // horda: apilados, el pathfinding los empuja y la mitad acaba
                // dentro de una pared.
                var angle = i * (2f * Mathf.PI / count);
                var radius = 0.8f + i * 0.12f;
                var spot = at + new Vector3(Mathf.Cos(angle) * radius, 0f,
                                            Mathf.Sin(angle) * radius);

                try
                {
                    var beast = MonoSingleton<AnimalManager>.Instance.SpawnAnimal(
                        AnimalId, spot, BodyType.Male, MaturePhase, 0.1f, false,
                        NSMedieval.Types.AnimalType.WildAggressive, null, null, null, "", 0, 0);

                    if (beast == null) continue;

                    GMPlugin.RunAfter(0.5f, () => beast.GetGoapAgent()?.StartTicker());
                    born++;
                }
                catch (Exception e)
                {
                    DevCommands.Say($"uno no salio: {e.Message}");
                    break;
                }
            }

            DevCommands.Say(born == 0
                ? "no salio ninguno"
                : $"{born} Corredor(es) adultos");
        }
    }

    // ----------------------------------------------------------------------

    /// <summary>
    /// Un monton de uno de nuestros objetos a los pies del raton.
    ///
    /// Vanilla tiene <c>spawnRandomResources</c>, que hace esto y se llama de
    /// una forma que no se parece a lo que hace. Este ademas valida el id
    /// contra el repositorio antes de nada, que es la diferencia entre "no
    /// existe" y "no paso nada" - dos cosas que en la consola se ven igual.
    /// </summary>
    internal sealed class AldrichGive : ConsoleCommand
    {
        public override string Command { get; protected set; }
        public override string Description { get; protected set; }
        public override string Help { get; protected set; }

        public AldrichGive()
        {
            Command = "aldrichGive";
            Description = "Deja un monton de un objeto nuestro donde apunte el raton.";
            Help = "aldrichGive <resourceId> [cuantos]   p.ej. aldrichGive blood_draught 10";
        }

        public void CommandMethod()
        {
            DevCommands.Say("aldrichGive <resourceId> [cuantos]. Los nuestros:");
            new AldrichList().CommandMethod("objetos");
        }

        public void CommandMethod(string resourceId)
        {
            CommandMethod(resourceId, 1);
        }

        public void CommandMethod(string resourceId, int amount)
        {
            if (string.IsNullOrEmpty(resourceId))
            {
                CommandMethod();
                return;
            }

            amount = Mathf.Clamp(amount, 1, 200);

            var blueprint = Repository<ResourceRepository, Resource>.Instance?.GetByID(resourceId);
            if (blueprint == null)
            {
                DevCommands.Say($"'{resourceId}' no esta en ningun Resources.json cargado");
                return;
            }

            if (!MonoSingleton<ResourcePileManager>.IsInstantiated())
            {
                DevCommands.Say("no hay partida cargada");
                return;
            }

            Vector3 at;
            if (!DevCommands.Aim(out at))
            {
                DevCommands.Say("apunta al mapa con el raton y vuelve a lanzarlo");
                return;
            }

            var made = 0;
            for (var i = 0; i < amount; i++)
            {
                try
                {
                    var spot = at + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), 0f,
                                                UnityEngine.Random.Range(-0.4f, 0.4f));

                    if (MonoSingleton<ResourcePileManager>.Instance
                            .SpawnPile(blueprint, spot, (string)null) != null) made++;
                }
                catch (Exception e)
                {
                    DevCommands.Say($"uno no salio: {e.Message}");
                    break;
                }
            }

            DevCommands.Say($"{made} x {resourceId}");
        }
    }
}
