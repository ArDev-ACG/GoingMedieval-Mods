using System.Reflection;
using HarmonyLib;
using NSMedieval.CombatAi;
using NSMedieval.State;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Los Alzados no se retiran. Nunca, y por ninguna de las dos puertas.
    ///
    /// <b>Que es retirarse aqui.</b> Una incursion normal acaba yendose: cuando
    /// <c>ActiveRaidInfo.HasEnded</c> se pone - porque el asalto fracaso, porque
    /// el reloj se agoto o porque el comandante lo dio por perdido -
    /// <c>EnemyRetreatAiActionGoal</c> toma el mando de cada enemigo y lo saca
    /// del mapa. Eso es lo correcto para saqueadores, que vinieron por botin y
    /// saben contar. Un cadaver que camina no vino por botin.
    ///
    /// <b>Y la segunda puerta, que es la que sorprende.</b> Leido el
    /// <c>CanStart</c> del juego: si el <c>ActiveRaidInfo</c> del raid no se
    /// encuentra - <c>GetById</c> devuelve null - la meta contesta <b>true</b>,
    /// es decir, se retira. Asi que un alzado que pierda su raid de vista - el
    /// objeto se limpia cuando el evento termina - se marcha solo, haya o no
    /// haya alguien delante. Los que sobreviven a una horda derrotada tienen
    /// que quedarse en el mapa: eso es lo que hace que la Horda no sea una
    /// incursion mas.
    ///
    /// <b>Por que un postfijo que escribe false y no un prefijo que salta.</b>
    /// Un prefijo que devuelve false sobre <c>CanStart</c> se salta tambien el
    /// cuerpo del metodo, y ese cuerpo <em>rellena el campo <c>raidInfo</c></em>
    /// que el juego usa despues. Dejar correr el metodo entero y contestar al
    /// final es la diferencia entre negar una decision y romper el estado con
    /// el que se toma.
    ///
    /// Lo que esto <b>no</b> toca: huir de miedo ya estaba cubierto por otro
    /// lado - <see cref="UndeadComposure"/> no deja que un alzado se desmaye ni
    /// se espante, y <c>CombatCalculator.GetFleeChance</c> solo devuelve algo
    /// distinto de cero para <c>DamageTakingAgentType</c> 1, que es el animal.
    /// Un alzado es humanoide, asi que esa rama ya contestaba cero.
    ///
    /// El JSON decia la mitad de esto - <c>raidWillNotSurrender: true</c> en la
    /// faccion - pero rendirse y retirarse son dos cosas distintas: lo primero
    /// es aceptar una negociacion y lo segundo es irse andando.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenNeverRetreats
    {
        /// <summary>Una linea la primera vez que se le niega a alguien, no una por tic.</summary>
        private static bool told;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EnemyRetreatAiActionGoal), "CanStart");
        }

        private static void Postfix(EnemyRetreatAiActionGoal __instance, ref bool __result)
        {
            if (!__result || __instance == null) return;
            if (!(GMPlugin.RisenNeverRetreats?.Value ?? true)) return;

            var owner = __instance.AgentOwner as HumanoidInstance;
            if (owner == null || !Census.IsUndead(owner)) return;

            __result = false;

            if (!told)
            {
                GMPlugin.Log?.LogInfo(
                    $"[risen] {GameAccess.Name(owner)} no se retira: los alzados no se van del mapa");
                told = true;
            }
        }
    }
}
