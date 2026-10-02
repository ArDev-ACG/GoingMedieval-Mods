using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>Lo que el codigo de Vampire Court necesita del plugin: su config, sus parches y sus barridos.</summary>
    public partial class GMPlugin
    {
        internal static BepInEx.Configuration.ConfigEntry<bool> VampireColdComfort;
        internal static BepInEx.Configuration.ConfigEntry<float> VampireColdComfortBelow;
        /// <summary>
        /// How often a vampire's melee hit drops its victim. A chance per hit,
        /// not a counter: no fight remembers the last one.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> BiteStunChance;
        /// <summary>
        /// How often an ordered bite on a prisoner turns them into a Ghoul.
        /// Only prisoners are ever rolled for; nothing a vampire does in a
        /// fight turns anyone any more.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> GhoulTurnChance;
        internal static BepInEx.Configuration.ConfigEntry<float> ThrallBloodFloor;
        internal static BepInEx.Configuration.ConfigEntry<float> ThrallBleedPercent;
        /// <summary>
        /// The five numbers behind "the sun burns where the sun reaches". They
        /// exist because the old rule was the game clock, which cannot tell a
        /// field at noon from a cellar at noon; see
        /// <see cref="VampireDaylight"/>.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> SunlightByPlace;
        internal static BepInEx.Configuration.ConfigEntry<bool> SleepShieldsFromSun;
        internal static BepInEx.Configuration.ConfigEntry<float> SunShelterLight;
        internal static BepInEx.Configuration.ConfigEntry<float> SunBurnLight;
        internal static BepInEx.Configuration.ConfigEntry<float> SunBurnSeconds;
        /// <summary>
        /// How much of a vampire's maximum health one meal puts back. Blood is
        /// what it runs on, so drinking has to be worth the walk.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> BiteHealPercent;
        /// <summary>
        /// How much of the victim's maximum health one bite takes. What the
        /// vampire gains has never had a cost on the other side of the fangs.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> BiteDrainPercent;
        /// <summary>
        /// How much of the victim's blood one bite takes.
        ///
        /// Separate from the health slice because they are separate bars with
        /// separate consequences: health is the wound, Blood is the game's own
        /// 0-100 stat whose thresholds at 80, 50 and 10 fire light-headedness,
        /// then failing consciousness, then a critical warning. This is what
        /// makes a household kept as a larder actually run dry.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> BiteBloodPercent;
        /// <summary>
        /// How long a vampire may go without blood before the thirst is at its
        /// worst, in real minutes. The three stages sit at a third, two thirds
        /// and all of it.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> ThirstMinutes;
        /// <summary>
        /// The share of consciousness below which a ravenous vampire stops
        /// choosing its own victims. 0 turns the rebellion off and leaves the
        /// thirst as penalties only.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> ThirstRebelBelow;
        /// <summary>
        /// How far the Count's presence carries, in tiles.
        ///
        /// It has to be ours. The game measures every other proximity effect
        /// with a single static sphere of radius 6 built once in
        /// CreatureBase.TryInitProximitySpheres and shared by every creature
        /// alive, so there is no way to widen the Count without widening messy
        /// eaters and rotting wounds too.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<int> CountAuraRadius;
        /// <summary>
        /// Whether a thirsty vampire feeds itself without being told.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> ThirstFeedsItself;
        /// <summary>
        /// How far a vampire will reach for a bottle of blood draught in the
        /// settlement's stock, in tiles.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> ThirstBottleRange;
        /// <summary>
        /// How much of the vampire's hunger a mouthful of blood puts back, as a
        /// multiple of the blood actually taken.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> BloodFeedsHunger;
        /// <summary>
        /// How much faster the dead mend out of the sun, and again in a coffin.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> ShelterHeals;
        /// <summary>
        /// How close a vampire has to get before it can bite, in tiles. The
        /// distance the chase action stops at.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> BiteReachDistance;
        /// <summary>
        /// How many times a vampire walks back over after losing its victim
        /// before it gives up and says so. 0 restores the old behaviour: one
        /// attempt, and a silent failure the moment the victim moved.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<int> BiteRetries;
        /// <summary>
        /// How far the dread around an impaled body reaches, in tiles. Only
        /// what is hostile to the settlement feels it.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<int> StakeDreadRadius;
        /// <summary>
        /// How much of an enemy's morale one sweep in sight of a pike takes, as
        /// a fraction of their maximum, and how low it is allowed to go. The
        /// effector's own MoodModify is a thought, and nothing on the enemy side
        /// of the game totals thoughts - see StakeDread.Dishearten.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> StakeMoodDrain;
        internal static BepInEx.Configuration.ConfigEntry<float> StakeMoodFloor;

        private void BindVampire()
        {
            BiteStunChance = Config.Bind("Vampire", "BiteStunChance", 0.10f,
                "Chance, per landed melee hit, that a vampire's bite drops the "
                + "victim in a faint. 0 disables it.");

            GhoulTurnChance = Config.Bind("Vampire", "GhoulTurnChance", 0.20f,
                "Chance that an ordered bite on a prisoner turns them into a "
                + "Ghoul and into a member of the settlement.");

            ThrallBloodFloor = Config.Bind("Vampire", "ThrallBloodFloor", 60f,
                "Sangre (0-100) por debajo de la cual un thrall deja de perderla. "
                + "Por encima de 50 no se desmaya.");

            ThrallBleedPercent = Config.Bind("Vampire", "ThrallBleedPercent", 0.016f,
                "Sangre que pierde un thrall cada 20 s (fraccion del maximo).");

            SunlightByPlace = Config.Bind("Vampire", "SunlightByPlace", true,
                "Whether daylight is decided by where the creature is standing "
                + "instead of by the clock. False restores the old behaviour, "
                + "where noon in a cellar hurt exactly as much as noon in a "
                + "field.");

            SleepShieldsFromSun = Config.Bind("Vampire", "SleepShieldsFromSun", true,
                "A sleeping vampire or ghoul is in its coffin and takes nothing "
                + "from the sun, whatever the light where the bed stands.");

            SunShelterLight = Config.Bind("Vampire", "SunShelterLight", 0.15f,
                "Light level below which a creature counts as sheltered - under "
                + "a roof, underground, or at night. 0 to 1.");

            SunBurnLight = Config.Bind("Vampire", "SunBurnLight", 0.6f,
                "Light level at or above which the burn fires as well as the "
                + "mood entry. Between this and SunShelterLight the day is felt "
                + "but the skin does not blister. 0 to 1.");

            SunBurnSeconds = Config.Bind("Vampire", "SunBurnSeconds", 12f,
                "Real seconds between two burns while out under open sky. The "
                + "health and the pain each one costs are in Effectors.json, "
                + "under VampireSunburn and GhoulSunburn. 0 disables the burn.");

            BiteHealPercent = Config.Bind("Vampire", "BiteHealPercent", 0.15f,
                "Fraction of maximum health a vampire recovers each time it "
                + "feeds. 0 disables the healing.");

            BiteDrainPercent = Config.Bind("Vampire", "BiteDrainPercent", 0.12f,
                "Fraction of maximum health a bite takes out of the victim, on "
                + "top of the blood-loss effector. 0 makes the bite painless.");

            BiteBloodPercent = Config.Bind("Vampire", "BiteBloodPercent", 0.18f,
                "Fraction of the victim's blood one bite drinks. The game's own "
                + "thresholds do the rest: under 80 light-headed, under 50 "
                + "failing, under 10 critical. 0 makes the bite bloodless.");

            ThirstMinutes = Config.Bind("Vampire", "ThirstMinutes", 25f,
                "Real minutes a vampire may go without blood before the thirst "
                + "reaches its worst. The three stages sit at a third, two "
                + "thirds and all of it. 0 turns the need off entirely.");

            ThirstRebelBelow = Config.Bind("Vampire", "ThirstRebelBelow", 0.5f,
                "Share of consciousness below which a ravenous vampire goes "
                + "for the nearest living thing on its own - settler, animal "
                + "or visitor. 0 leaves the thirst as penalties only.");

            CountAuraRadius = Config.Bind("Vampire", "CountAuraRadius", 12,
                "How far the Count's presence reaches, in tiles. The game's own "
                + "proximity effects are fixed at 6 and cannot be changed "
                + "without changing them for everyone.");

            BiteReachDistance = Config.Bind("Vampire", "BiteReachDistance", 2.2f,
                "How close a vampire has to get before it bites, in tiles. 1.2 "
                + "is arm's length and only ever worked on a victim standing "
                + "still; around 2 lets the bite land while both are moving.");

            BiteRetries = Config.Bind("Vampire", "BiteRetries", 2,
                "How many times a vampire walks back over after losing its "
                + "victim. 0 gives up on the first failed path, which is what "
                + "made the order look like it did nothing.");

            ThirstFeedsItself = Config.Bind("Vampire", "ThirstFeedsItself", true,
                "A thirsty vampire looks after itself: a bottle of blood "
                + "draught first, then an animal, and at the last stage of the "
                + "thirst whoever is nearest.");

            ThirstBottleRange = Config.Bind("Vampire", "ThirstBottleRange", 30f,
                "How far a vampire will reach into the settlement's stock for "
                + "a blood draught, in tiles.");

            BloodFeedsHunger = Config.Bind("Vampire", "BloodFeedsHunger", 1f,
                "How much of the vampire's hunger a mouthful puts back, as a "
                + "multiple of the blood actually taken out of the victim. "
                + "0 goes back to blood being no food at all.");

            ShelterHeals = Config.Bind("Vampire", "ShelterHeals", true,
                "Out of the sun the dead mend faster than the living, and in a "
                + "coffin faster still. Off, a sunburn takes as long to heal "
                + "as an axe wound.");

            StakeDreadRadius = Config.Bind("Vampire", "StakeDreadRadius", 10,
                "How far the dread around an impaled body reaches, in tiles. "
                + "Only creatures hostile to the settlement feel it.");

            StakeMoodDrain = Config.Bind("Vampire", "StakeMoodDrain", 0.06f,
                "How much morale one sweep in sight of a pike takes off an "
                + "enemy, as a fraction of their maximum. The effector's "
                + "MoodModify cannot do this: it files a thought, and nothing "
                + "on the enemy side of the game totals thoughts. 0 turns the "
                + "morale half of the dread off and leaves the attributes.");

            StakeMoodFloor = Config.Bind("Vampire", "StakeMoodFloor", 0.25f,
                "How low that drain is allowed to push an enemy's morale, as a "
                + "fraction of their maximum. The point is a raider who fights "
                + "worse, not one paralysed by the sight of a corpse.");

            VampireColdComfort = Config.Bind("Vampire", "ColdComfort", true,
                "A vampire or ghoul standing somewhere cold is where it wants "
                + "to be, and its mood says so.");

            VampireColdComfortBelow = Config.Bind("Vampire", "ColdComfortBelow", 2f,
                "Degrees on the tile the vampire is standing on, at or below "
                + "which the comfort applies.");
        }

        private static void PatchVampire(Harmony harmony)
        {
            PatchOne(harmony, typeof(VampireSunlight));
            PatchOne(harmony, typeof(VampireBite));
            PatchOne(harmony, typeof(CountAura));
            PatchOne(harmony, typeof(VampireKillTally));
            PatchOne(harmony, typeof(CountKillRequirement));
            PatchOne(harmony, typeof(CountKillTooltip));
            PatchOne(harmony, typeof(CountDraculaName));
            PatchOne(harmony, typeof(BiteGoalRegistration));
            PatchOne(harmony, typeof(BiteGoalPool));
            PatchOne(harmony, typeof(BiteMenuRegistration));
            PatchOne(harmony, typeof(BiteMenuInjection));
            PatchOne(harmony, typeof(BiteRetryCancel));
            PatchOne(harmony, typeof(BloodDraughtDrunk));
            PatchOne(harmony, typeof(BloodDraughtEaten));
            PatchOne(harmony, typeof(BloodDraughtMenuRegistration));
            PatchOne(harmony, typeof(BloodDraughtMenuInjection));
        }

        private static void StartVampire()
        {
            VampireCold.Start();
            StakeDread.Start();
            Thrall.Start();
            VampireDaylight.Start();
            VampireThirst.Start();
            VampireKills.Start();
        }
    }
}
