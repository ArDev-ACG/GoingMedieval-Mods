using System;
using System.Collections.Generic;
using NSEipix.Base;
using NSMedieval;
using NSMedieval.Manager;
using NSMedieval.State;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// El Corredor pega tambien con la cola, y al segundo que tiene detras.
    ///
    /// <b>"Solo si se puede" - se puede, y esto es lo que hay debajo.</b> El
    /// rig del Corredor se fabrico a partir de 89 piezas rigidas, asi que sus
    /// 157 huesos se llaman <c>b000</c>..<c>b156</c> y no dicen nada. La cola es
    /// la cadena en serie mas larga del esqueleto - <c>b073</c>, <c>b075</c>,
    /// <c>b077</c>, <c>b079</c>, <c>b081</c>, <c>b083</c>, <c>b085</c>,
    /// <c>b087</c>, <c>b088</c> -, nueve huesos en la linea media (x = 0)
    /// colgando uno de otro. No hay forma de encontrarla por nombre: se
    /// encontro midiendo la jerarquia, y por eso los nombres estan escritos
    /// aqui y no adivinados en partida.
    ///
    /// <b>Que hace.</b> Cada medio segundo, un Corredor que tenga algo vivo a
    /// dos casillas y media que **no sea su objetivo** le da un coletazo: dano
    /// directo por <c>CombatHitManager.DealDamage</c> - la misma puerta por la
    /// que entra la flecha, asi que la sangre acida tambien salta - y un
    /// latigazo de la cola de un tercio de segundo. Lo de "que no sea su
    /// objetivo" es lo que hace la escena: el bicho muerde a uno de frente y
    /// sacude al que se le acerca por detras, en vez de pegar dos veces al
    /// mismo.
    ///
    /// <b>La animacion va en LateUpdate</b> - ver <c>XenoBody</c> -, que es
    /// despues de que el <c>Animation</c> legacy haya escrito los huesos: en
    /// Update la pisaria el clip en el mismo fotograma.
    /// </summary>
    internal static class XenoTail
    {
        private const float SweepSeconds = 0.5f;

        /// <summary>Hasta donde llega un coletazo, en casillas.</summary>
        private const float Reach = 2.5f;

        /// <summary>Lo que tarda en volver a usarla, en segundos.</summary>
        private const float Cooldown = 3.5f;

        /// <summary>Los nueve huesos de la cola, de la base a la punta.</summary>
        internal static readonly string[] Bones =
        {
            "b073", "b075", "b077", "b079", "b081", "b083", "b085", "b087", "b088",
        };

        private static readonly Dictionary<int, float> Next = new Dictionary<int, float>();
        private static bool said;

        internal static void Start()
        {
            GMPlugin.Every("xeno tail", SweepSeconds, Sweep);
        }

        private static void Sweep()
        {
            if (!(GMPlugin.XenoTailEnabled?.Value ?? true)) return;

            var now = Time.time;

            foreach (var body in Census.Everything())
            {
                var beast = body as AnimalInstance;
                if (beast == null || beast.HasDisposed || beast.HasDiedOrFainted) continue;
                if (beast.Id != XenoRunnerModel.AnimalId
                    && beast.Blueprint?.GetID() != XenoRunnerModel.AnimalId) continue;

                float ready;
                if (Next.TryGetValue(beast.UniqueId, out ready) && now < ready) continue;

                var victim = Behind(beast);
                if (victim == null) continue;

                Next[beast.UniqueId] = now + Cooldown;
                Lash(beast, victim);
            }
        }

        /// <summary>
        /// Lo mas cercano que este a distancia de cola y no sea su presa de
        /// frente. Entre xenos no se pegan.
        /// </summary>
        private static CreatureBase Behind(AnimalInstance beast)
        {
            var aimed = beast.GetTarget() as CreatureBase;
            var from = beast.GetPosition();

            CreatureBase found = null;
            var closest = Reach;

            foreach (var body in Census.Everything())
            {
                var other = body as CreatureBase;
                if (other == null || other.HasDisposed || other.HasDied) continue;
                if (ReferenceEquals(other, beast) || ReferenceEquals(other, aimed)) continue;
                if (XenoAcid.IsHive(other)) continue;
                // Quien lleva un abrazacaras es la incubadora de la colmena: el
                // 26 los coletazos remataban a las gallinas tumbadas.
                if (Facehugger.IsHost(other)) continue;

                var far = Vector3.Distance(from, other.GetPosition());
                if (far >= closest) continue;

                closest = far;
                found = other;
            }

            return found;
        }

        private static void Lash(AnimalInstance beast, CreatureBase victim)
        {
            try
            {
                // CombatHitManager es estatico - no es un MonoSingleton -, y
                // esta es la misma sobrecarga de tres parametros que parchea
                // XenoAcid: por aqui pasa tambien la flecha, asi que la sangre
                // acida contesta al coletazo como a cualquier otro golpe.
                var damage = Mathf.Max(0f, GMPlugin.XenoTailDamage?.Value ?? 6f);
                if (damage > 0f) CombatHitManager.DealDamage(victim, damage, beast);

                XenoNoise.Lash(beast.GetPosition());

                var view = beast.GetAgentView<NSMedieval.View.Animals.AnimalView>();
                if (view != null)
                {
                    var body = view.GetComponent<XenoBody>();
                    if (body != null) body.Lash();
                }

                if (!said)
                {
                    said = true;
                    GMPlugin.Log?.LogInfo(
                        $"[xeno] coletazo de {GameAccess.Name(beast)} a {GameAccess.Name(victim)} "
                        + $"({damage:0} de dano)");
                }
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[xeno] tail failed: {e.Message}");
            }
        }
    }
}
