using System.Collections.Generic;
using NSMedieval.State;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// La peste salta de colono a colono.
    ///
    /// Hasta hoy la unica forma de cogerla era el mordisco de una rata, y eso
    /// deja la epidemia en manos de cuantas ratas haya: en la partida del 12
    /// entraron **cinco** mordiscos en toda la partida. Una peste que solo
    /// viaja en los dientes de otro no se comporta como una peste; lo que la
    /// hace una es que el enfermo contagie al que duerme al lado.
    ///
    /// <b>Por que un barrido y no un parche.</b> No hay un metodo "estar
    /// cerca": la cercania es una condicion continua, no un evento, y el juego
    /// no llama a nadie cuando dos colonos se acercan. Asi que esto va en
    /// <c>Heartbeat</c>, como el terror de la estaca.
    ///
    /// <b>Las tres cosas que respeta, y ninguna es opcional.</b> Un inmune no
    /// se contagia - <c>PlagueImmunityMarker</c> es lo que
    /// <see cref="PlagueImmunity"/> pone al que sobrevive a la fiebre -, un
    /// enfermo no se contagia dos veces, y la careta cuenta: la misma
    /// <c>PlagueRatBite.Ward</c> que multiplica las odds de un mordisco las
    /// multiplica aqui, porque una mascara que para la peste de una rata y no
    /// la del vecino no es una mascara, es una excepcion.
    ///
    /// La probabilidad es por barrido y por pareja, asi que el numero por
    /// defecto es bajo a proposito: con seis colonos en una sala son cinco
    /// parejas por enfermo cada treinta segundos, y eso suma rapido.
    /// </summary>
    internal static class PlagueSpread
    {
        /// <summary>Cada cuanto se mira. Mas fino no cambia nada y cuesta.</summary>
        internal const float SweepSeconds = 30f;

        private const string ImmunityEffectorId = "PlagueImmunityMarker";

        /// <summary>Lo dice la primera vez, no en cada barrido.</summary>
        private static bool told;

        internal static void Start()
        {
            var beat = Heartbeat.Live();
            if (beat == null) return;

            beat.Repeat("plague spread", SweepSeconds, Sweep);
        }

        private static void Sweep()
        {
            var chance = GMPlugin.PlagueSpreadChance?.Value ?? 0f;
            if (chance <= 0f) return;

            var reach = GMPlugin.PlagueSpreadReach?.Value ?? 0;
            if (reach <= 0) return;

            var roster = Roster();
            if (roster.Count < 2) return;

            // Los enfermos se sacan una vez y no dentro del bucle: un colono
            // que se contagia en este mismo barrido no contagia ya a nadie
            // hasta el siguiente, que es lo que impide que una sala entera
            // caiga en un solo tic por el orden de la lista.
            var sick = new List<HumanoidInstance>();
            foreach (var who in roster)
            {
                var stats = GameAccess.Stats(who);
                if (stats != null && stats.IsEffectorActive(Carrion.FeverEffectorId))
                {
                    sick.Add(who);
                }
            }

            if (sick.Count == 0) return;

            var caught = 0;

            foreach (var carrier in sick)
            {
                var from = carrier.GetGridPosition();

                foreach (var other in roster)
                {
                    if (other == carrier) continue;
                    if (other.HasDisposed || other.HasDied) continue;

                    var stats = GameAccess.Stats(other);
                    if (stats == null) continue;
                    if (stats.IsEffectorActive(Carrion.FeverEffectorId)) continue;
                    if (stats.IsEffectorActive(ImmunityEffectorId)) continue;

                    var to = other.GetGridPosition();
                    if (Mathf.Abs(to.x - from.x) > reach) continue;
                    if (Mathf.Abs(to.z - from.z) > reach) continue;

                    // Un piso arriba o abajo no contagia: en esta casa la gente
                    // vive en tres alturas y un enfermo en la bodega no esta
                    // cerca de nadie del salon, por mucho que en el plano lo
                    // parezca.
                    if (Mathf.Abs(to.y - from.y) > 1) continue;

                    var odds = chance * PlagueRatBite.Ward(stats);
                    if (Random.value > odds) continue;

                    if (!stats.StartEffector(Carrion.FeverEffectorId, 1f, false, -1, null)) continue;

                    BiteFeedback.Bubble(other, PlagueRatBite.PlagueIcon);
                    GMPlugin.Log?.LogInfo(
                        $"[plague] {GameAccess.Name(other)} la cogio de "
                        + $"{GameAccess.Name(carrier)}, no de una rata");

                    caught++;
                    told = true;
                }
            }

            if (!told && caught == 0)
            {
                // Que haya enfermos y no se contagie nadie es informacion: dice
                // que la regla corre y que las odds son bajas, no que este rota.
                GMPlugin.Log?.LogInfo(
                    $"[plague] {sick.Count} enfermo(s) en el censo, ningun contagio este barrido");
                told = true;
            }
        }

        /// <summary>
        /// El censo de colonos, que es la lista de la barra de arriba.
        ///
        /// Es la misma que usa <see cref="RisenTurn"/> para quitar de ella al
        /// que se levanta, asi que un alzado que fue colono ya no esta aqui - y
        /// eso es justo lo que se quiere: la peste es cosa de vivos.
        /// </summary>
        private static List<HumanoidInstance> Roster()
        {
            var found = new List<HumanoidInstance>();

            if (!NSEipix.Base.MonoSingleton<global::Managers.WorkersViewManager>.IsInstantiated())
                return found;

            var workers = global::Managers.WorkersViewManager.Instance?.Workers;
            if (workers == null) return found;

            foreach (var who in workers)
            {
                if (who != null && !who.HasDisposed && !who.HasDied) found.Add(who);
            }

            return found;
        }
    }
}
