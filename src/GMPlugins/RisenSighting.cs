using System;
using NSMedieval;
using NSMedieval.CombatAi;
using NSMedieval.Goap;
using NSMedieval.State;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Un alzado y un vivo que no es colono se atacan en cuanto se ven, sin
    /// esperar a que uno golpee primero.
    ///
    /// <b>Lo que faltaba.</b> <see cref="RisenProvokes"/> hace que el saqueador
    /// devuelva el golpe, y el log del 22 lo confirma. Pero devolver el golpe
    /// es lo unico que hacia: quien no ha sido golpeado todavia no tiene nada
    /// que devolver, asi que las dos bandas se cruzaban sin mirarse hasta que
    /// alguien pegaba. Lo que se pidio es lo otro - "que ambos se ataquen en
    /// cuanto se vean" -, y eso es esto.
    ///
    /// <b>Que hace.</b> Cada segundo, para cada alzado en pie busca el vivo no
    /// colono mas cercano dentro del alcance de vista, y escribe el objetivo en
    /// los dos: al alzado como <c>PreferedTarget</c>, que es lo que su IA de
    /// combate mira, y al vivo como <c>NextTarget</c>, que es la misma puerta
    /// que abre el golpe devuelto - con
    /// <see cref="RisenProvokesUnderOrders"/> detras para que la orden del
    /// comandante no la cierre -. Ademas se apunta el rencor en
    /// <see cref="RisenBrawl"/>, que es quien pega cuando el juego no encuentra
    /// casilla libre.
    ///
    /// <b>Lo que no toca.</b> Los colonos, de los dos lados: su combate
    /// funciona y lo manda el jugador. Y a quien ya tiene de objetivo algo de
    /// la otra banda no se le cambia nada.
    /// </summary>
    internal static class RisenSighting
    {
        private const float SweepSeconds = 1f;

        private static bool announced;

        internal static void Start()
        {
            GMPlugin.Every("undead sighting", SweepSeconds, Sweep);
        }

        private static void Sweep()
        {
            if (!(GMPlugin.UndeadSight?.Value ?? true)) return;

            RisenHunt.ReleaseShadows();

            var reach = Mathf.Max(2f, GMPlugin.UndeadSightRange?.Value ?? 14f);
            var npcs = GlobalSaveController.CurrentVillageData?.NPCs;
            if (npcs == null) return;

            foreach (var walker in RisenRoll.Walkers())
            {
                if (walker == null || walker.HasDisposed || walker.HasDiedOrFainted) continue;

                HumanoidInstance seen = null;
                var closest = reach;
                var from = walker.GetPosition();

                for (var i = 0; i < npcs.Count; i++)
                {
                    var npc = npcs[i];
                    if (npc == null || npc.HasDisposed || npc.HasDiedOrFainted) continue;
                    if (npc.IsWorker() || npc.IsPrisoner() || Census.IsUndead(npc)) continue;

                    var far = Vector3.Distance(from, npc.GetPosition());
                    if (far >= closest) continue;

                    closest = far;
                    seen = npc;
                }

                if (seen == null) continue;
                Introduce(walker, seen);
            }
        }

        /// <summary>
        /// Los dos se apuntan. Cada lado se deja como esta si ya mira a alguien
        /// de la otra banda: cambiarle el objetivo a quien ya esta peleando es
        /// lo que hacia que se quedaran quietos.
        /// </summary>
        private static void Introduce(HumanoidInstance walker, HumanoidInstance living)
        {
            try
            {
                RisenBrawl.Provoked(living, walker);

                Aim(walker.CombatAi, CombatAiState.PreferedTarget, living, IsLivingPrey);
                Aim(living.CombatAi, CombatAiState.NextTarget, walker, Census.IsUndead);

                // Apuntar no basta: entre dos enemigos el AttackGoal del juego
                // no arranca (ver RisenHunt.IsFellowEnemy), asi que cada uno
                // recibe orden de ir a por el otro y el golpe lo da RisenBrawl.
                var walkerAim = walker.CombatAi?.GetState<IDamageTakingAgent>(CombatAiState.PreferedTarget);
                if (ReferenceEquals(walkerAim, living) && RisenHunt.IsFellowEnemy(living))
                {
                    RisenHunt.Shadow(walker, living);
                }

                if (living.IsEnemy() && !ChasingUndead(living)) RisenHunt.Shadow(living, walker);

                if (!announced)
                {
                    announced = true;
                    GMPlugin.Log?.LogInfo(
                        $"[risen] {GameAccess.Name(walker)} y {GameAccess.Name(living)} se ven "
                        + $"a {Vector3.Distance(walker.GetPosition(), living.GetPosition()):0.0} "
                        + "y se buscan");
                }
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[risen] sighting failed: {e.Message}");
            }
        }

        /// <summary>Si este vivo ya va detras de algun alzado en pie.</summary>
        private static bool ChasingUndead(HumanoidInstance living)
        {
            var follow = living.EnemyBehaviour?.CurrentOrder as NSMedieval.CommanderAI.Orders.MoveOrder;
            var chased = follow?.FollowCreature as HumanoidInstance;
            return chased != null && !chased.HasDisposed && !chased.HasDiedOrFainted
                   && Census.IsUndead(chased);
        }

        private static bool IsLivingPrey(HumanoidInstance who)
        {
            return who != null && !Census.IsUndead(who) && !who.IsWorker() && !who.IsPrisoner();
        }

        private static void Aim(CombatAiAgent ai, CombatAiState slot, HumanoidInstance at,
            Func<HumanoidInstance, bool> alreadyRight)
        {
            if (ai == null || ai.HasDisposed) return;

            var current = ai.GetState<IDamageTakingAgent>(slot) as HumanoidInstance;
            if (current != null && !current.HasDisposed && !current.HasDiedOrFainted
                && alreadyRight(current)) return;

            RisenGoalState.Set(ai, slot, at);
        }
    }
}
