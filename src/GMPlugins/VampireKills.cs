using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSEipix.Base;
using NSMedieval.Goap;
using NSMedieval.Manager;
using NSMedieval.Roles;
using NSMedieval.State;
using NSMedieval.UI.Utils;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Lo que un vampiro ha matado, y lo que eso le da.
    ///
    /// <b>Pedido el 25.</b> Que el Conde, ademas de horas, habilidad y sala,
    /// necesite matar para subir, y cada vez mas; que su ultimo nivel sea
    /// Dracula; y que cualquier vampiro se haga mas fuerte con cada muerte.
    ///
    /// <b>Donde se cuenta.</b> <c>CombatController.OnAgentKilled(asesino,
    /// victima)</c>, que es por donde pasa toda muerte de combate - lo llama
    /// <c>CombatHitManager.DealDamage</c>, el mismo que usa el golpe de
    /// <see cref="RisenBrawl"/>. Las mordidas que desangran no cuentan: eso es
    /// comer, no pelear.
    ///
    /// <b>Donde se guarda.</b> En <c>StatsInstance.bannedEffectors</c>, que ya
    /// va en la partida guardada (es lo mismo que hace la inmunidad a la
    /// peste): una marca <c>AldrichKill:N</c> por muerte. Ningun efector se
    /// llama asi, asi que prohibirlo no prohibe nada.
    ///
    /// <b>Lo que da.</b> Un efector por muerte hasta <see cref="MaxTally"/>,
    /// <c>VampireBloodTally1..N</c> en VampireCourt/Effectors.json, con dano,
    /// vida y paso algo mayores cada uno. Son <c>dontSave</c>: el barrido los
    /// vuelve a poner despues de cargar, leyendo la cuenta.
    /// </summary>
    internal static class VampireKills
    {
        internal const string CountRole = "count";
        private const string Mark = "AldrichKill:";
        private const string TallyPrefix = "VampireBloodTally";
        internal const int MaxTally = 20;

        /// <summary>Muertes para llegar a cada nivel de Conde; el 1 no pide ninguna.</summary>
        private static readonly int[] KillsForLevel = { 0, 0, 5, 15, 30 };

        internal const int DraculaLevel = 4;

        internal static int Needed(int level)
        {
            return level >= 0 && level < KillsForLevel.Length ? KillsForLevel[level] : 0;
        }

        internal static int Kills(HumanoidInstance humanoid)
        {
            var banned = GameAccess.Stats(humanoid)?.GetBannedEffectors();
            if (banned == null) return 0;

            var n = 0;
            foreach (var name in banned)
            {
                if (name != null && name.StartsWith(Mark, StringComparison.Ordinal)) n++;
            }

            return n;
        }

        internal static bool IsVampire(HumanoidInstance humanoid)
        {
            return humanoid != null && GameAccess.HasPerk(humanoid, VampireBite.VampirePerk);
        }

        internal static void Start()
        {
            // Despues de cargar no hay ninguno puesto: dontSave.
            GMPlugin.Every("vampire blood tally", 10f, () =>
            {
                if (!MonoSingleton<WorkerManager>.IsInstantiated()) return;
                foreach (var settler in WorkerManager.WorkersHere)
                {
                    if (IsVampire(settler)) Sync(settler);
                }
            });
        }

        internal static void Tally(HumanoidInstance killer, object victim)
        {
            var stats = GameAccess.Stats(killer);
            if (stats == null) return;

            var kills = Kills(killer) + 1;
            stats.BanEffector(Mark + kills);
            Sync(killer);

            var victimName = victim is CreatureBase body ? GameAccess.Name(body) : "?";
            GMPlugin.Log?.LogInfo($"[blood] {GameAccess.Name(killer)} mata a {victimName}: {kills} muerte(s)");
        }

        /// <summary>Deja puesto el efector que toca por la cuenta, y ningun otro.</summary>
        private static void Sync(HumanoidInstance vampire)
        {
            var stats = GameAccess.Stats(vampire);
            if (stats == null) return;

            var tier = Math.Min(MaxTally, Kills(vampire));
            for (var i = 1; i <= MaxTally; i++)
            {
                var id = TallyPrefix + i;
                var active = stats.IsEffectorActive(id);
                if (i == tier && !active) stats.StartEffector(id, 1f, false, -1, null);
                else if (i != tier && active) GameAccess.ForceEndEffector(vampire, id);
            }
        }
    }

    /// <summary>Cuenta cada muerte de combate de un vampiro.</summary>
    [HarmonyPatch]
    internal static class VampireKillTally
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(NSMedieval.Controllers.CombatController), "OnAgentKilled",
                new[] { typeof(IDamageDealAgent), typeof(IDamageTakingAgent) });
        }

        private static void Postfix(IDamageDealAgent killer, IDamageTakingAgent target)
        {
            try
            {
                var vampire = killer as HumanoidInstance;
                if (!VampireKills.IsVampire(vampire) || ReferenceEquals(killer, target)) return;
                VampireKills.Tally(vampire, target);
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[blood] tally failed: {e}");
            }
        }
    }

    /// <summary>
    /// El Conde no sube de nivel sin sus muertes. Es el unico sitio que hace
    /// falta: la subida, el aviso de "puede subir" y el boton del panel de
    /// roles preguntan todos aqui.
    /// </summary>
    [HarmonyPatch]
    internal static class CountKillRequirement
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(HumanoidRoleOwner), "CanRoleBeLeveledUp", new[] { typeof(Role) });
        }

        private static void Postfix(HumanoidRoleOwner __instance, Role role, ref bool __result)
        {
            if (!__result || role?.GetID() != VampireKills.CountRole) return;

            var count = GameAccess.RoleOwnerHumanoid(__instance);
            if (count == null) return;

            var next = __instance.RoleLevel + 1;
            if (VampireKills.Kills(count) < VampireKills.Needed(next)) __result = false;
        }
    }

    /// <summary>
    /// La linea de las muertes en el tooltip del siguiente nivel, con el mismo
    /// formato que la de horas: "Muertes: 3 (5)", en rojo mientras falte.
    /// </summary>
    [HarmonyPatch]
    internal static class CountKillTooltip
    {
        private static readonly MethodInfo Format =
            AccessTools.Method(typeof(HumanoidRoleUtils), "FormatTooltipLine");

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(HumanoidRoleUtils), "FillNextLevelLines");
        }

        private static void Postfix(List<string> lines, HumanoidInstance humanoidInstance, Role role)
        {
            if (lines == null || role?.GetID() != VampireKills.CountRole || humanoidInstance == null) return;

            var owner = humanoidInstance.WorkerBehaviour?.HumanoidRoleOwner;
            var next = (owner != null && owner.HasRole(role) ? owner.RoleLevel : 0) + 1;
            var need = VampireKills.Needed(next);
            if (need <= 0) return;

            // El idioma, por el propio nombre del rol: el mod no tiene tabla de
            // textos y el juego ya lo tradujo.
            var spanish = (HumanoidRoleUtils.GetRoleName(role, humanoidInstance) ?? "").Contains("Conde");
            var have = VampireKills.Kills(humanoidInstance);
            var line = $"{(spanish ? "Muertes" : "Kills")}: {have} ({need})";
            lines.Add(Format != null ? (string)Format.Invoke(null, new object[] { line, have >= need }) : line);
        }
    }

    /// <summary>
    /// El ultimo nivel de Conde se llama Dracula: se cambia el nombre del rol
    /// alli donde el juego lo escribe con su nivel.
    /// </summary>
    [HarmonyPatch]
    internal static class CountDraculaName
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var withLevel = AccessTools.Method(typeof(HumanoidRoleUtils), "GetRoleNameWithIconAndLevel",
                new[] { typeof(Role), typeof(int), typeof(HumanoidInstance) });
            if (withLevel != null) yield return withLevel;

            var plain = AccessTools.Method(typeof(HumanoidRoleUtils), "GetRoleName",
                new[] { typeof(Role), typeof(HumanoidInstance) });
            if (plain != null) yield return plain;
        }

        private static void Postfix(object[] __args, ref string __result)
        {
            if (string.IsNullOrEmpty(__result) || __args == null || __args.Length == 0) return;
            var role = __args[0] as Role;
            if (role?.GetID() != VampireKills.CountRole) return;

            int level;
            if (__args.Length == 3 && __args[1] is int given) level = given;
            else
            {
                var owner = (__args[__args.Length - 1] as HumanoidInstance)?.WorkerBehaviour?.HumanoidRoleOwner;
                level = owner != null && owner.HasRole(role) ? owner.RoleLevel : 0;
            }

            if (level < VampireKills.DraculaLevel) return;

            __result = __result.Replace("Conde", "Drácula").Replace("Count", "Dracula");
        }
    }
}
