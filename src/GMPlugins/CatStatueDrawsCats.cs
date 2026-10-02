using System.Reflection;
using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Los gatos solo aparecen donde hay una estatua de gato.
    ///
    /// <b>Que es lo que decide esto.</b> El gato no llega por un evento - no
    /// hay ninguno que lo traiga - sino como fauna de ambiente: su entrada en
    /// <c>AnimalBase.json</c> lleva <c>maxCount: 10</c> y el repoblador del mapa
    /// va colocando hasta llegar a ese numero. Asi que la palanca no esta en
    /// ningun JSON que podamos escribir: esta en
    /// <c>AnimalSpawner.PlaceAnimal(string)</c>, que es la unica puerta por la
    /// que entra cada bicho, y por eso esto es un prefijo que devuelve false y
    /// no un peso ni una probabilidad.
    ///
    /// <b>Y por que la estatua.</b> La estatua era un monumento a los gatos que
    /// ya estaban; lo que se pidio es lo contrario, que sea la razon de que
    /// esten. Sin una construida el mapa no repone gatos - los que ya hubiera
    /// se quedan, porque esto no mata a nadie - y con una vuelven.
    ///
    /// Se cuenta la estatua, no el mod: quien instale Carrion and Plague y no
    /// construya nada no ha pedido gatos.
    ///
    /// La regla se apaga entera desde
    /// <c>BepInEx/config/aldrich.gmplugins.cfg</c>, porque es un cambio a la
    /// fauna del mapa y no todo el mundo quiere uno.
    /// </summary>
    [HarmonyPatch]
    internal static class CatStatueDrawsCats
    {
        internal const string CatAnimalId = "cat";
        internal const string StatueBuildingId = "cat_statue";

        /// <summary>Una linea cuando la regla cambia de lado, no una por gato.</summary>
        private static bool refusing;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method("NSMedieval.Map.AnimalSpawner:PlaceAnimal",
                                      new[] { typeof(string) });
        }

        private static bool Prefix(string animalId)
        {
            if (!(GMPlugin.CatsNeedStatue?.Value ?? true)) return true;
            if (animalId != CatAnimalId) return true;

            var statues = GameAccess.BuildingsCount(StatueBuildingId);
            if (statues > 0 && Room(statues))
            {
                if (refusing)
                {
                    GMPlugin.Log?.LogInfo(
                        "[cat] hay estatua de gato: los gatos vuelven a aparecer");
                    refusing = false;
                }

                return true;
            }

            if (!refusing)
            {
                GMPlugin.Log?.LogInfo(statues > 0
                    ? $"[cat] {statues} estatua(s) y ya hay los gatos que dan: no se reponen mas"
                    : "[cat] sin estatua de gato construida: no se repone fauna felina");
                refusing = true;
            }

            return false;
        }

        /// <summary>
        /// Graduado: cada estatua da <c>CatsPerStatue</c> gatos. Se cuentan los
        /// vivos del mapa, mascotas incluidas - un gato es un gato -.
        /// </summary>
        private static bool Room(int statues)
        {
            var each = GMPlugin.CatsPerStatue?.Value ?? 3;
            if (each <= 0) return true;

            var cats = 0;
            foreach (var body in Census.Everything())
            {
                var beast = body as NSMedieval.State.AnimalInstance;
                if (beast == null || beast.HasDisposed || beast.HasDied) continue;
                if (beast.Id == CatAnimalId || beast.Blueprint?.GetID() == CatAnimalId) cats++;
            }

            return cats < statues * each;
        }
    }
}
