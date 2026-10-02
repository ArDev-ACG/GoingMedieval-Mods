using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSMedieval.Goap;
using NSMedieval.Manager;
using NSMedieval.Scripts.Pooler;
using NSMedieval.State;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Lo que le sale al Corredor por las heridas no es sangre: es acido.
    ///
    /// Tres cosas, que son las tres que se pidieron - "que las heridas del xeno
    /// tiren sangre verde y queme como acido si alguien le salpica o entra en
    /// contacto":
    ///
    ///   1. <b>Verde.</b> El chorro que el juego pinta al golpear lo tine este
    ///      parche y lo devuelve a su color un segundo despues. Se tine el
    ///      objeto que sale del pozo de particulas, no el prefab, y por eso hay
    ///      que devolverlo: el siguiente que lo pida es cualquiera.
    ///   2. <b>Salpica a quien pega.</b> El que acaba de herirlo, si estaba
    ///      encima, se lleva la herida <c>xeno_acid_burn</c> con la probabilidad
    ///      de <c>[Xeno] AcidSplash</c>, y el arma con la que lo hizo se gasta
    ///      de mas: el acido tambien se come el filo.
    ///   3. <b>Y queda en el suelo.</b> Un Corredor muerto deja un charco doce
    ///      segundos; quien lo pise se quema, con menos probabilidad que el que
    ///      metio la mano.
    ///
    /// El aviso es el del ticket: el acido no distingue bandos. Un colono que
    /// remate a un Corredor a cuchillo va a acabar con quemaduras.
    /// </summary>
    internal static class XenoAcid
    {
        private const string BurnWound = "xeno_acid_burn";
        private const float SweepSeconds = 1.5f;
        private const float PoolSeconds = 12f;
        private const float PoolReach = 1.6f;
        private const float SplashReach = 2.5f;
        private const float BystanderReach = 1.6f;

        private static readonly Color Acid = new Color(0.44f, 0.95f, 0.22f, 1f);

        private static int greenFrame = -10;

        private sealed class Pool
        {
            internal Vector3 Where;
            internal float Until;
        }

        private static readonly List<Pool> Pools = new List<Pool>();
        private static readonly HashSet<int> Bled = new HashSet<int>();
        private static bool announced;

        internal static bool IsXeno(CreatureBase body)
        {
            var animal = body as AnimalInstance;
            if (animal == null) return false;
            return animal.Id == XenoRunnerModel.AnimalId
                   || (animal.Blueprint != null && animal.Blueprint.GetID() == XenoRunnerModel.AnimalId);
        }

        /// <summary>
        /// Cualquiera de la colmena: Corredor, huevo o abrazacaras. Entre ellos
        /// ni el acido ni la cola hacen dano - el 24 el acido de un Corredor
        /// quemo a un abrazacaras (#217) y los dos acabaron matandose.
        /// </summary>
        internal static bool IsHive(CreatureBase body)
        {
            if (IsXeno(body)) return true;

            var animal = body as AnimalInstance;
            if (animal == null) return false;

            var id = animal.Blueprint?.GetID() ?? animal.Id;
            return id == Facehugger.EggId || id == Facehugger.HuggerId
                   || animal.Id == Facehugger.EggId || animal.Id == Facehugger.HuggerId;
        }

        internal static void Start()
        {
            GMPlugin.Every("xeno acid", SweepSeconds, Sweep);
        }

        /// <summary>
        /// Durante este fotograma, lo que pinte el pozo de particulas es acido.
        /// </summary>
        private static void PaintNextGreen()
        {
            greenFrame = Time.frameCount;
        }

        internal static bool Painting()
        {
            return Time.frameCount - greenFrame <= 1;
        }

        internal static void Tint(GameObject particles)
        {
            if (particles == null) return;

            var systems = particles.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0) return;

            var before = new ParticleSystem.MinMaxGradient[systems.Length];
            var lifetimeWas = new bool[systems.Length];

            for (var i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                before[i] = main.startColor;
                main.startColor = new ParticleSystem.MinMaxGradient(Acid);

                // Un gradiente a lo largo de la vida pisa el color de salida,
                // y el de la sangre va de rojo a rojo oscuro.
                var over = systems[i].colorOverLifetime;
                lifetimeWas[i] = over.enabled;
                over.enabled = false;
            }

            if (!announced)
            {
                announced = true;
                GMPlugin.Log?.LogInfo(
                    $"[xeno] acid blood: {systems.Length} particle system(s) tinted green");
            }

            // Devuelto: el objeto vuelve al pozo y el siguiente que lo pida
            // puede ser la herida de cualquiera.
            GMPlugin.RunAfter(1.2f, () =>
            {
                for (var i = 0; i < systems.Length; i++)
                {
                    if (systems[i] == null) continue;
                    var main = systems[i].main;
                    main.startColor = before[i];
                    var over = systems[i].colorOverLifetime;
                    over.enabled = lifetimeWas[i];
                }
            });
        }

        /// <summary>
        /// El chorro de una herida del Corredor: verde, y quema a quien lo
        /// tenga encima.
        /// </summary>
        internal static void Wounded(IDamageDealAgent dealer, CreatureBase xeno)
        {
            try
            {
                if (!(GMPlugin.XenoAcidBlood?.Value ?? true)) return;
                if (xeno == null || xeno.HasDisposed) return;

                PaintNextGreen();

                var splash = GMPlugin.XenoAcidSplash?.Value ?? 0.5f;
                var here = xeno.GetPosition();

                var attacker = dealer as CreatureBase;
                if (attacker != null && !attacker.HasDisposed && !IsHive(attacker)
                    && Vector3.Distance(attacker.GetPosition(), here) <= SplashReach
                    && Random.value < splash)
                {
                    Burn(attacker, "le salpico al herirlo");

                    // El acido se come tambien el arma con la que le pego.
                    var weapon = CombatUtils.GetWeapon(dealer);
                    if (weapon != null) weapon.DealWeaponDurabilityDamage(false);
                }

                foreach (var body in Census.Everything())
                {
                    if (body == null || body.HasDisposed || body.HasDiedOrFainted) continue;
                    if (ReferenceEquals(body, xeno) || ReferenceEquals(body, attacker)) continue;
                    if (IsHive(body)) continue;
                    if (Vector3.Distance(body.GetPosition(), here) > BystanderReach) continue;
                    if (Random.value >= splash * 0.3f) continue;

                    Burn(body, "estaba al lado cuando salpico");
                }
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[xeno] acid splash failed: {e}");
            }
        }

        /// <summary>Los charcos que deja un Corredor muerto, y quien los pisa.</summary>
        private static void Sweep()
        {
            if (!(GMPlugin.XenoAcidBlood?.Value ?? true)) return;

            var now = Heartbeat.Now;

            foreach (var body in Census.Everything())
            {
                if (body == null || body.HasDisposed || !IsXeno(body)) continue;
                if (!body.HasDied) continue;
                if (!Bled.Add(body.UniqueId)) continue;

                Pools.Add(new Pool { Where = body.GetPosition(), Until = now + PoolSeconds });
                GMPlugin.Log?.LogInfo($"[xeno] {GameAccess.Name(body)} deja un charco de acido");
            }

            for (var i = Pools.Count - 1; i >= 0; i--)
            {
                if (now >= Pools[i].Until) Pools.RemoveAt(i);
            }

            if (Pools.Count == 0) return;

            var odds = (GMPlugin.XenoAcidSplash?.Value ?? 0.5f) * 0.35f;

            foreach (var body in Census.Everything())
            {
                if (body == null || body.HasDisposed || body.HasDiedOrFainted) continue;
                if (IsHive(body)) continue;

                var at = body.GetPosition();
                for (var i = 0; i < Pools.Count; i++)
                {
                    if (Vector3.Distance(at, Pools[i].Where) > PoolReach) continue;
                    if (Random.value < odds) Burn(body, "piso el charco");
                    break;
                }
            }
        }

        private static void Burn(CreatureBase victim, string why)
        {
            try
            {
                var stats = GameAccess.Stats(victim);
                if (stats == null) return;
                if (stats.StartEffector(BurnWound, 1f, false, -1, null))
                {
                    GMPlugin.Log?.LogInfo($"[xeno] {GameAccess.Name(victim)} se quema: {why}");
                }
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[xeno] acid burn failed: {e}");
            }
        }
    }

    /// <summary>
    /// El golpe que hiere a un Corredor avisa a <see cref="XenoAcid"/>. Va en
    /// el reparto de dano y no en el ataque cuerpo a cuerpo a proposito: por
    /// aqui pasan tambien la flecha y la trampa.
    /// </summary>
    [HarmonyPatch]
    internal static class XenoAcidOnHit
    {
        /// <remarks>
        /// <b>Por que no cargaba.</b> En el log del 22 esta la razon de que no
        /// se viera una gota de acido en toda la partida:
        /// <c>FAILED XenoAcidOnHit ... AmbiguousMatchException ... CombatHitManager:DealDamage</c>.
        /// <c>DealDamage</c> esta dos veces - la del combate
        /// <c>(IDamageDealAgent, IDamageTakingAgent, DamageType, Func&lt;,&gt;)</c>
        /// y la directa <c>(IDamageTakingAgent, float, IDamageDealAgent)</c> -
        /// y pedirla por nombre no elige ninguna: Harmony se planta y el
        /// parche entero se queda fuera. Se piden las dos por firma, cada una
        /// con su parche, que es lo unico que Harmony sabe casar.
        /// </remarks>
        private static MethodBase TargetMethod()
        {
            return Overload(4);
        }

        /// <summary>
        /// La sobrecarga de <c>DealDamage</c> con tantos parametros. Se elige
        /// por cuenta y no por firma porque uno de los tipos es un
        /// <c>Func&lt;,&gt;</c> del juego, y equivocar su argumento devuelve
        /// null sin decirlo.
        /// </summary>
        internal static MethodBase Overload(int howMany)
        {
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(CombatHitManager)))
            {
                if (method.Name == "DealDamage" && method.GetParameters().Length == howMany) return method;
            }

            GMPlugin.Log?.LogError($"[xeno] no DealDamage with {howMany} parameters; acid blood is off");
            return null;
        }

        private static void Postfix(IDamageDealAgent damageDealAgent, IDamageTakingAgent target)
        {
            var xeno = target as CreatureBase;
            if (xeno == null || !XenoAcid.IsXeno(xeno)) return;

            XenoAcid.Wounded(damageDealAgent, xeno);
        }
    }

    /// <summary>
    /// La otra puerta del dano: la que no viene de un ataque sino de un numero
    /// - una trampa, una caida, un efector -. La sangre del Corredor quema
    /// igual, y quien no tiene brazo (<c>dealer</c> nulo) no se quema.
    /// </summary>
    [HarmonyPatch]
    internal static class XenoAcidOnDirectHit
    {
        private static MethodBase TargetMethod()
        {
            return XenoAcidOnHit.Overload(3);
        }

        private static void Postfix(IDamageTakingAgent target, IDamageDealAgent dealer)
        {
            var xeno = target as CreatureBase;
            if (xeno == null || !XenoAcid.IsXeno(xeno)) return;

            XenoAcid.Wounded(dealer, xeno);
        }
    }

    /// <summary>
    /// Y el chorro que el juego pinta un instante despues sale verde. El pozo
    /// de particulas no sabe de quien es la herida, asi que lo dice el parche
    /// de arriba y esto solo mira si fue en este fotograma.
    /// </summary>
    [HarmonyPatch]
    internal static class XenoAcidParticles
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ParticleSystemPool), "PlayParticles",
                new[]
                {
                    typeof(string), typeof(Vector3), typeof(bool), typeof(bool),
                    typeof(System.Action<GameObject>), typeof(SkinnedMeshRenderer),
                });
        }

        private static void Postfix(string id, GameObject __result)
        {
            if (!XenoAcid.Painting()) return;
            if (id != "weapon_hit" && id != "block_hit") return;

            XenoAcid.Tint(__result);
        }
    }
}
