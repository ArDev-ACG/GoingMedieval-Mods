using System;
using HarmonyLib;
using NSMedieval.Model;
using NSMedieval.State;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Lo que tiraba el juego al soltar cadaveres a punados.
    ///
    /// <b>El parte.</b> "Spawne muchos cadaveres y trono, me saco del juego".
    /// El log de la sesion que murio - la del 20 a las 13:23 - acaba asi: a las
    /// 13:23:17.350 se crea un humanoide en blanco, a las 13:23:17.368 el juego
    /// lo desecha (<c>FinalizeDispose</c>), y **1,2 segundos despues**, a las
    /// 13:23:18.604, salta una <c>NullReferenceException</c> en
    /// <c>HumanoidInstance.HandleAgeEffectors</c>. Un segundo y medio mas tarde
    /// la partida se esta cerrando.
    ///
    /// <b>La causa.</b> El comando de consola que reparte cadaveres
    /// (<c>CommandSpawnResourceCategory.SpawnPile</c>) no fabrica un cadaver:
    /// **saca un NPC al azar**, lo mata y se queda con el cuerpo. Pero
    /// <c>HumanoidInstance.Spawn</c> deja encargado un paso diferido - el
    /// <c>StepAction</c> de la linea 934, que es este metodo - y ese paso lo
    /// ejecuta el <c>TaskController</c> **en un frame posterior**, cuando el
    /// cuerpo ya esta desechado y su <c>activeBehaviour</c> es null. El getter
    /// <c>CurrentHumanType</c> es <c>activeBehaviour?.HumanType</c>, asi que
    /// devuelve null, y la primera linea del metodo le pide
    /// <c>.AgeEffectors</c>. Con un cadaver no se nota; con treinta, treinta
    /// excepciones dentro de <c>TaskController.Update</c>, que es el bucle del
    /// que cuelgan todos los pasos diferidos del juego.
    ///
    /// Prefijo y no finalizador a proposito: no hay nada del metodo que merezca
    /// correr sobre un cuerpo sin comportamiento - los efectores de edad se
    /// aplican a los <c>Stats</c> de alguien que va a vivir -, y lo que hay que
    /// evitar es justamente que entre.
    ///
    /// Verificar: <c>spawnResourceCategory</c> con cadaveres, treinta de golpe,
    /// y que el juego siga en pie y no salga una sola linea
    /// <c>HandleAgeEffectors</c> en el log.
    /// </summary>
    [HarmonyPatch(typeof(HumanoidInstance), "HandleAgeEffectors")]
    internal static class AgeEffectorsGuard
    {
        private static int skipped;

        private static bool Prefix(HumanoidInstance __instance)
        {
            if (__instance == null) return false;
            if (!__instance.HasDisposed && __instance.CurrentHumanType != null) return true;

            // Contado y no una linea por cuerpo: son treinta de golpe y el
            // motivo es siempre el mismo.
            skipped++;
            if (skipped == 1 || skipped % 25 == 0)
            {
                GMPlugin.Log?.LogInfo(
                    $"[spawn] {skipped} cuerpo(s) desechados antes de su paso de edad; "
                    + "saltado, que es lo que cerraba la partida al soltar cadaveres");
            }

            return false;
        }
    }

    /// <summary>
    /// Lo que sacaba del juego al pasar el raton por un monton de equipo.
    ///
    /// <b>El parte.</b> Otra sesion del mismo dia, a las 13:27:06: el reportero
    /// de errores del juego se abre encima de la partida con una
    /// <c>NullReferenceException</c> que nace en
    /// <c>Equipment.get_AgentFlammability</c> y baja por
    /// <c>EquipmentUtils.GetTooltipLines</c> -&gt;
    /// <c>EquipmentPileTooltipView</c> -&gt; <c>TooltipViewNew.OnPointerEnter</c>.
    /// Es decir: el raton se poso sobre un monton de equipo en el suelo.
    ///
    /// <b>La causa, que es la vieja conocida por otra puerta.</b> El getter es
    /// <c>agentFlammability * itemQuality.AgentFlammability *
    /// materialSettings.AgentFlammability</c>, y esos dos campos solo los
    /// rellena <c>EquipmentRepository.InitializeEquipmentItems</c> en las
    /// variantes <c>&lt;calidad&gt;_&lt;material&gt;_&lt;pieza&gt;</c>. En el
    /// **proto** quedan nulos. Ponerle variantes al preset - lo que arreglo el
    /// tick del alzado - no alcanza aqui: el proto sigue existiendo, es lo que
    /// se suelta al morir, y un monton en el suelo se puede mirar.
    ///
    /// El guardia esta en el getter y no en los datos porque un
    /// <c>MaterialSettings</c> neutro inventado lo leen tambien el dano y el
    /// desgaste, y ahi si cambiaria numeros. Lo que devuelve es el valor propio
    /// de la pieza sin multiplicadores, que es exactamente lo que vale una pieza
    /// que no tiene ni calidad ni material.
    ///
    /// Verificar: matar a un alzado, pasar el raton por lo que suelta, y que
    /// salga el recuadro en vez del reportero de errores.
    /// </summary>
    [HarmonyPatch]
    internal static class FlammabilityGuard
    {
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertyGetter(typeof(Equipment), "AgentFlammability");
            yield return AccessTools.PropertyGetter(typeof(Equipment), "AgentFireDamageMultiplier");
        }

        private static readonly System.Collections.Generic.HashSet<string> Named =
            new System.Collections.Generic.HashSet<string>();

        private static bool Prefix(Equipment __instance, ref float __result,
                                   System.Reflection.MethodBase __originalMethod)
        {
            if (__instance == null) { __result = 1f; return false; }
            if (Read(__instance, "itemQuality") != null
                && Read(__instance, "materialSettings") != null) return true;

            __result = Raw(__instance, __originalMethod.Name);

            // Nombrada y una sola vez: toda la dificultad de la primera version
            // de esto fue no saber que pieza era la que contestaba.
            var id = __instance.GetID();
            if (id != null && Named.Add(id))
            {
                GMPlugin.Log?.LogInfo(
                    $"[equipo] '{id}' es un proto sin calidad ni material; se le da su "
                    + "propio valor de fuego sin multiplicar, que es lo que hacia saltar "
                    + "el reportero al mirar un monton");
            }

            return false;
        }

        private static object Read(Equipment item, string field)
        {
            var info = AccessTools.Field(typeof(Equipment), field);
            return info == null ? null : info.GetValue(item);
        }

        /// <summary>
        /// El campo serializado que el getter habria multiplicado, con la misma
        /// salida de <c>-1</c> que trae el juego: por debajo de eso la pieza
        /// dice "no me preguntes" y vale 1.
        /// </summary>
        private static float Raw(Equipment item, string getter)
        {
            var field = getter == "get_AgentFlammability"
                ? "agentFlammability"
                : "agentFireDamageMultiplier";

            var info = AccessTools.Field(typeof(Equipment), field);
            if (info == null) return 1f;

            var value = (float)info.GetValue(item);
            return value <= -1f ? 1f : value;
        }
    }
}
