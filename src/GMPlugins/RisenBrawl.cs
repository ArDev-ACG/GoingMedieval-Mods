using System.Collections.Generic;
using NSEipix.Base;
using NSMedieval;
using NSMedieval.CombatAi;
using NSMedieval.Goap;
using NSMedieval.Goap.Actions;
using NSMedieval.Manager;
using NSMedieval.State;
using NSMedieval.Types;
using NSMedieval.View;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Cuando un alzado y un vivo que no es colono estan cuerpo a cuerpo y el
    /// juego no les deja pegarse, se pegan igual.
    ///
    /// <b>Lo que decia el log del 20 (22:57).</b> Los tres tickets que seguian
    /// abiertos - "pegan una vez y se quedan quietos", "sigue al enemigo y no le
    /// pega" y "pegan 1 de n veces y el saqueador no devuelve el golpe" - son la
    /// misma frase escrita desde tres sitios. Los saqueadores
    /// <c>non_partisan_nomads_*</c> #243-#252 fallan su <c>AttackGoal</c>
    /// contra los alzados con <c>Incompletable Action: /None</c> - la secuencia
    /// de inicio aborta antes de la primera accion - y los brutos de la horda
    /// (#211, #212) fallan <c>FollowAttackOrderGoal</c> en <c>GoToTarget</c>. A
    /// los dos lados les pasa lo mismo y a ninguno le pasa contra un colono.
    ///
    /// <b>Por que.</b> El ataque cuerpo a cuerpo del juego necesita una casilla
    /// libre al lado del objetivo (<c>FindMeleeViableAttackPosition</c>): la
    /// descarta si ya hay tres criaturas en ella o si otro la tiene reservada,
    /// y <c>IsInAttackPositionMelee</c> contesta que no a quien esta en un nodo
    /// con mas de tres. Una horda aparece entera en el mismo punto - en el log
    /// se crean treinta brutos en (351.5, 21, 304.9) - y una incursion tambien,
    /// asi que la pelea entre las dos es justo el caso en que no hay casilla.
    /// Contra colonos no se nota porque llegan de uno en uno.
    ///
    /// <b>Lo que hace esto.</b> Cada 0,4 s, para cada alzado con presa humana
    /// que ya este a distancia de golpe, y para cada vivo al que un alzado haya
    /// golpeado (o que tenga a un alzado de objetivo) y lo tenga al lado: si el
    /// juego no ha pegado por su cuenta en el ultimo ciclo de ataque, el golpe
    /// lo da este barrido, con la <b>misma</b> llamada que usa el juego -
    /// <c>CombatActions.AttackMelee</c> -, que tira acierto contra evasion, hace
    /// el dano, la sangre, el sonido y la reaccion al golpe. Y le pone la
    /// animacion de ataque. Nada de esto sustituye al ataque del juego: si el
    /// juego pega, <c>LastAttackTime</c> se mueve y aqui no se hace nada.
    ///
    /// Los colonos quedan fuera a proposito, de los dos lados: su combate
    /// funciona, y un colono que pega solo seria un colono que el jugador no
    /// manda.
    /// </summary>
    internal static class RisenBrawl
    {
        private const float SweepSeconds = 0.4f;

        /// <summary>Cuanto dura el "me ha pegado un alzado", en segundos.</summary>
        private const float GrudgeSeconds = 20f;

        /// <summary>Por encima de esto no se considera cuerpo a cuerpo.</summary>
        private const float MaxReach = 2.2f;

        private static readonly Dictionary<int, float> NextStrike = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> LastStrike = new Dictionary<int, float>();
        private static readonly Dictionary<int, KeyValuePair<HumanoidInstance, float>> Grudges =
            new Dictionary<int, KeyValuePair<HumanoidInstance, float>>();
        private static readonly HashSet<long> Said = new HashSet<long>();

        private static bool announcedStrike;

        internal static void Start()
        {
            GMPlugin.Every("undead brawl", SweepSeconds, Sweep);
        }

        /// <summary>Lo llama <see cref="RisenProvokes"/> cuando un alzado golpea.</summary>
        internal static void Provoked(HumanoidInstance victim, HumanoidInstance walker)
        {
            if (victim == null || walker == null) return;
            Grudges[victim.UniqueId] = new KeyValuePair<HumanoidInstance, float>(walker, Heartbeat.Now);
        }

        /// <summary>
        /// Si este barrido le ha hecho dar un golpe desde <paramref name="since"/>
        /// (en <c>Time.time</c>). <see cref="RisenRestless"/> lo cuenta como
        /// progreso, igual que un golpe del juego.
        /// </summary>
        internal static bool StruckSince(HumanoidInstance walker, float since)
        {
            float at;
            return walker != null && LastStrike.TryGetValue(walker.UniqueId, out at) && at >= since;
        }

        private static void Sweep()
        {
            if (!(GMPlugin.UndeadBrawl?.Value ?? true)) return;
            if (!MonoSingleton<CombatTargetManager>.IsInstantiated()) return;

            // 1. Los alzados contra su presa.
            foreach (var walker in RisenRoll.Walkers())
            {
                var prey = TargetOf(walker) as HumanoidInstance;
                if (prey == null || prey.IsWorker() || Census.IsUndead(prey)) continue;
                TryStrike(walker, prey);
            }

            // 2. Los vivos contra el alzado que les pega o al que miran.
            var npcs = GlobalSaveController.CurrentVillageData?.NPCs;
            if (npcs == null) return;

            for (var i = 0; i < npcs.Count; i++)
            {
                var npc = npcs[i];
                if (npc == null || npc.HasDisposed || npc.HasDiedOrFainted) continue;
                if (npc.IsWorker() || npc.IsPrisoner() || Census.IsUndead(npc)) continue;

                var walker = Foe(npc);
                if (walker == null) continue;
                TryStrike(npc, walker);
            }
        }

        /// <summary>
        /// A quien va: lo que el gestor de objetivos tiene apuntado, y si no, lo
        /// que tiene su IA de combate.
        /// </summary>
        private static IDamageTakingAgent TargetOf(HumanoidInstance agent)
        {
            var manager = MonoSingleton<CombatTargetManager>.Instance;
            return manager.GetPreferredTarget(agent)
                   ?? agent.CombatAi?.GetState<IDamageTakingAgent>(CombatAiState.PreferedTarget)
                   ?? agent.GetTarget();
        }

        /// <summary>
        /// El alzado con el que este vivo tiene cuentas: el que tiene de
        /// objetivo, o el que le ha pegado hace poco. Solo si sigue en pie.
        /// </summary>
        private static HumanoidInstance Foe(HumanoidInstance npc)
        {
            var aimed = TargetOf(npc) as HumanoidInstance;
            if (Standing(aimed)) return aimed;

            KeyValuePair<HumanoidInstance, float> grudge;
            if (!Grudges.TryGetValue(npc.UniqueId, out grudge)) return null;

            if (Heartbeat.Now - grudge.Value > GrudgeSeconds || !Standing(grudge.Key))
            {
                Grudges.Remove(npc.UniqueId);
                return null;
            }

            return grudge.Key;
        }

        private static bool Standing(HumanoidInstance walker)
        {
            return walker != null && !walker.HasDisposed && !walker.HasDiedOrFainted
                   && Census.IsUndead(walker);
        }

        private static void TryStrike(HumanoidInstance attacker, HumanoidInstance target)
        {
            if (attacker == null || attacker.HasDisposed || attacker.HasDiedOrFainted) return;
            if (target == null || target.HasDisposed || target.HasDiedOrFainted) return;
            if (attacker.IsOnFire || attacker.OperatingSiegeWeapon) return;

            var ai = attacker.CombatAi;
            if (ai == null || ai.HasDisposed) return;

            // Solo cuerpo a cuerpo: un arquero se queda con su arco.
            if (CombatUtils.GetAttackType(attacker) != AttackType.Melee) return;

            if (Vector3.Distance(attacker.GetPosition(), target.GetPosition()) > MaxReach) return;
            if (!CombatUtils.IsInAttackRange(attacker, target)) return;
            if (!CombatAttackerPositioningManager.IsInHeightDiffLimitRange(attacker, target)) return;
            if (!CombatUtils.IsAttackPossible(attacker, target)) return;

            var now = TimerController.TimeSinceStartup;
            var swing = Mathf.Max(0.4f, CombatCalculator.CalculateAttackSpeed(attacker));

            // El juego ha pegado por su cuenta en este ciclo: no hace falta.
            var native = ai.GetState<float>(CombatAiState.LastAttackTime);
            if (native > 0f && now - native < swing * 1.25f) return;

            float next;
            if (NextStrike.TryGetValue(attacker.UniqueId, out next) && now < next) return;
            NextStrike[attacker.UniqueId] = now + swing;

            Strike(attacker, target, swing);
        }

        private static void Strike(HumanoidInstance attacker, HumanoidInstance target, float swing)
        {
            try
            {
                attacker.SetTarget(target);
                attacker.FaceTarget();

                var view = attacker.GetAgentView<AnimatedAgentView>();
                if (view != null)
                {
                    view.TrySetParameter("AttackSpeed", 1f / swing);
                    view.TrySetParameter("AttackRnd", Random.Range(0, 3));
                    view.TrySetParameter("IsAttacking", true);
                    view.TrySetTrigger("Attack");
                }

                // El golpe cae a mitad del gesto, como el evento "shoot" del juego.
                GMPlugin.RunAfter(swing * 0.5f, () => Land(attacker, target));
                GMPlugin.RunAfter(swing * 0.95f, () =>
                {
                    if (view != null) view.TrySetParameter("IsAttacking", false);
                });

                var pair = ((long)attacker.UniqueId << 32) | (uint)target.UniqueId;
                if (Said.Add(pair))
                {
                    GMPlugin.Log?.LogInfo(
                        $"[brawl] {GameAccess.Name(attacker)} pega a {GameAccess.Name(target)} "
                        + $"(goal del juego: {attacker.GoapAgent?.CurrentGoalName ?? "-"})");
                }
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[brawl] could not strike: {e}");
            }
        }

        private static void Land(HumanoidInstance attacker, HumanoidInstance target)
        {
            try
            {
                if (attacker == null || attacker.HasDisposed || attacker.HasDiedOrFainted) return;
                if (target == null || target.HasDisposed || target.HasDied) return;
                if (Vector3.Distance(attacker.GetPosition(), target.GetPosition()) > MaxReach + 0.5f) return;

                // El juego puede haberle cambiado el objetivo entre el gesto y
                // el golpe; para este golpe manda el nuestro.
                if (!ReferenceEquals(attacker.GetTarget(), target)) attacker.SetTarget(target);

                CombatActions.AttackMelee(attacker);
                LastStrike[attacker.UniqueId] = Time.time;
                RisenGoalState.Set(attacker.CombatAi, CombatAiState.LastAttackedTarget, target);

                if (!announcedStrike)
                {
                    announcedStrike = true;
                    GMPlugin.Log?.LogInfo("[brawl] first strike landed through CombatActions.AttackMelee");
                }
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[brawl] strike failed: {e}");
            }
        }
    }
}
