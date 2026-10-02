using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>Lo que el codigo de Carrion and Plague necesita del plugin: su config, sus parches y sus barridos.</summary>
    public partial class GMPlugin
    {
        /// <summary>
        /// How much bigger than its mesh the cat statue is drawn. The mesh is an
        /// inventory trophy, so at 1 it is ornament-sized; the right number is a
        /// matter of taste, so it lives in the config file where it can be tuned
        /// between two loads instead of between two builds.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> CatStatueScale;
        /// <summary>
        /// Whether the scaled statue is nudged back over the centre of its own
        /// tile. Off leaves it wherever its pivot lands, which is what the
        /// scaling alone used to do.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> CatStatueRecenter;
        internal static BepInEx.Configuration.ConfigEntry<bool> CatsNeedStatue;
        internal static BepInEx.Configuration.ConfigEntry<int> CatsPerStatue;
        internal static BepInEx.Configuration.ConfigEntry<float> PlagueSpreadChance;
        internal static BepInEx.Configuration.ConfigEntry<int> PlagueSpreadReach;
        /// <summary>
        /// How much one unburied body adds to the weight of the rat plague.
        /// The event system reads weights against every other group's, so this
        /// is a share of a share.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> RatsPerCorpse;
        /// <summary>
        /// How many unburied bodies it takes before the animal raid that fires
        /// is certainly the rats rather than one of the other three in its
        /// group. 0 leaves the choice to chance.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<int> RatsCorpseCertainty;
        /// <summary>
        /// The colour a plague rat is painted. Any HTML colour the game can
        /// parse; empty leaves the vanilla rat alone.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<string> PlagueRatColor;
        /// <summary>
        /// How often a rat's bite gives a settler the fever. A chance per hit,
        /// because the game's own hit-effector threshold cannot express one:
        /// it is a damage floor, and a rat's damage is 1.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> PlagueBiteChance;
        /// <summary>What each face covering leaves of that chance.</summary>
        internal static BepInEx.Configuration.ConfigEntry<float> PlagueClothWard;
        internal static BepInEx.Configuration.ConfigEntry<float> PlagueMaskWard;
        internal static BepInEx.Configuration.ConfigEntry<float> PlagueHoodWard;
        /// <summary>
        /// How much harder a rat bites than vanilla. See
        /// <see cref="PlagueRatTeeth"/>.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> PlagueRatDamage;

        private void BindCarrion()
        {
            // 2, not 4, and the number comes off the mesh rather than off
            // taste. cat_stuffed_trophy measures 0.264 x 0.505 x 0.604 and a
            // tile is one unit across, so four times over is a cat 1.06 wide
            // and 2.42 deep standing on a one-tile footprint - hanging most of
            // a tile out of its own square in both directions, which is what
            // every "la caja esta mal" and "no esta centrada" has really been
            // looking at. Two is the largest that still fits the envelope
            // vanilla gives its own one-tile statues (foxy_statue is 0.965 x
            // 1.577 x 1.208), and 0.53 x 1.01 x 1.21 sits inside that.
            //
            // It is still a cat and not a monument, and no multiplier will fix
            // that: the mesh is an inventory trophy in a trophy's pose. The
            // sitting cat on a pedestal is modelling work, and it is first in
            // the Unity queue.
            CatStatueScale = Config.Bind("CatStatue", "Scale", 2.0f,
                "Size multiplier for the cat statue's mesh. 1 leaves it at the "
                + "size of the trophy it borrows; 2 fills the same envelope as "
                + "vanilla's own one-tile statues. Much above that and the cat "
                + "hangs out of its own tile.");

            CatStatueRecenter = Config.Bind("CatStatue", "Recenter", true,
                "Keep the scaled statue centred on its tile and standing on the "
                + "floor instead of drifting with its own pivot.");

            CatsNeedStatue = Config.Bind("Animals", "CatsNeedStatue", true,
                "Los gatos solo aparecen en un mapa donde haya una estatua de "
                + "gato construida. Los que ya estuvieran se quedan; lo que se "
                + "para es la reposicion de fauna.");

            CatsPerStatue = Config.Bind("Animals", "CatsPerStatue", 3,
                "Cuantos gatos repone el mapa por cada estatua de gato en pie. "
                + "0 = sin tope (una estatua basta para el maximo del juego).");

            PlagueSpreadChance = Config.Bind("Carrion", "PlagueSpreadChance", 0.04f,
                "Probabilidad de que un enfermo contagie a un colono sano que "
                + "tenga cerca, por barrido (cada 30 s) y por pareja. La careta "
                + "la multiplica igual que en un mordisco. 0 apaga el contagio "
                + "entre colonos y deja solo el de las ratas.");

            PlagueSpreadReach = Config.Bind("Carrion", "PlagueSpreadReach", 3,
                "A cuantas baldosas contagia un enfermo. Un piso de diferencia "
                + "no cuenta como cerca.");

            RatsPerCorpse = Config.Bind("Carrion", "RatsPerCorpse", 0.35f,
                "How much each unburied body adds to the weight of the animal "
                + "raid the rats belong to. 0 restores vanilla's dice.");

            RatsCorpseCertainty = Config.Bind("Carrion", "RatsCorpseCertainty", 8,
                "Unburied bodies needed before the animal raid that fires is "
                + "certainly the rats and not one of the other three. 0 leaves "
                + "it to chance.");

            PlagueRatColor = Config.Bind("Carrion", "PlagueRatColor", "#6E7A55",
                "Colour of a rat carrying the plague. Empty leaves rats the "
                + "colour the game paints them.");

            PlagueRatDamage = Config.Bind("Carrion", "PlagueRatDamage", 4f,
                "Multiplier on a rat's bite. Vanilla deals 1 damage, which "
                + "on a settler with a hundred hit points is invisible - the "
                + "bite looked like a miss because nothing happened. "
                + "1 restores vanilla.");

            PlagueBiteChance = Config.Bind("Carrion", "PlagueBiteChance", 0.35f,
                "Chance, per rat bite that lands, of catching the fever. The "
                + "hit-effector threshold in the JSON cannot do this: it is a "
                + "damage floor, and a rat only ever deals 1.");

            PlagueClothWard = Config.Bind("Carrion", "PlagueClothWard", 0.70f,
                "What a plain mouthpiece or head scarf leaves of the bite "
                + "chance. 1 is no protection at all, 0 is complete.");

            PlagueMaskWard = Config.Bind("Carrion", "PlagueMaskWard", 0.35f,
                "The same, for the beaked plague mask.");

            PlagueHoodWard = Config.Bind("Carrion", "PlagueHoodWard", 0.15f,
                "The same, for the full plague robe.");
        }

        private static void PatchCarrion(Harmony harmony)
        {
            PatchOne(harmony, typeof(PlagueImmunity));
            PatchOne(harmony, typeof(CatStatueScale));
            PatchOne(harmony, typeof(CatStatueDrawsCats));
            PatchOne(harmony, typeof(PlagueRatMark));
            PatchOne(harmony, typeof(PlagueRatBite));
            PatchOne(harmony, typeof(PlagueRatTeeth));
            PatchOne(harmony, typeof(CarrionDrawsRats));
            PatchOne(harmony, typeof(CarrionPicksRats));
        }

        private static void StartCarrion()
        {
            PlagueSpread.Start();
        }
    }
}
