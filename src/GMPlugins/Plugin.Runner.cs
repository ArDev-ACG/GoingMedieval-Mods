using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>Lo que el codigo de The Runner necesita del plugin: su config, sus parches y sus barridos.</summary>
    public partial class GMPlugin
    {
        /// <summary>
        /// The Runner's own body over the invisible wolf that carries its
        /// logic. See <see cref="XenoRunnerModel"/>.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> XenoOwnModel;
        internal static BepInEx.Configuration.ConfigEntry<float> XenoScale;
        internal static BepInEx.Configuration.ConfigEntry<float> XenoMaxLength;
        internal static BepInEx.Configuration.ConfigEntry<float> XenoYaw;
        internal static BepInEx.Configuration.ConfigEntry<bool> XenoAcidBlood;
        internal static BepInEx.Configuration.ConfigEntry<bool> XenoTailEnabled;
        internal static BepInEx.Configuration.ConfigEntry<float> XenoTailDamage;
        internal static BepInEx.Configuration.ConfigEntry<string> XenoBiteSound;
        internal static BepInEx.Configuration.ConfigEntry<string> XenoLashSound;
        internal static BepInEx.Configuration.ConfigEntry<bool> FacehuggerEnabled;
        internal static BepInEx.Configuration.ConfigEntry<float> FacehuggerHours;
        internal static BepInEx.Configuration.ConfigEntry<float> XenoAcidSplash;
        internal static BepInEx.Configuration.ConfigEntry<int> XenoDreadRadius;
        internal static BepInEx.Configuration.ConfigEntry<int> XenoPanicRadius;
        internal static BepInEx.Configuration.ConfigEntry<bool> XenoPanicFlee;
        internal static BepInEx.Configuration.ConfigEntry<bool> XenoClimbEnabled;
        internal static BepInEx.Configuration.ConfigEntry<int> XenoClimbLevels;

        private void BindRunner()
        {
            XenoOwnModel = Config.Bind("Runner", "OwnModel", true,
                "The Runner is drawn with its own model and animations instead "
                + "of the wolf it is built on. Off shows the tinted wolf.");

            XenoScale = Config.Bind("Runner", "Scale", 1.15f,
                "Height of the Runner against the wolf's height.");

            XenoMaxLength = Config.Bind("Runner", "MaxLength", 3.4f,
                "Longest the Runner may be, head to tail tip, in tiles. Its "
                + "tail is long, and this keeps it from eating the scale.");

            XenoYaw = Config.Bind("Runner", "Yaw", 180f,
                "Turn of the model around its vertical axis, in degrees. "
                + "Change it if the Runner walks backwards or sideways.");

            XenoTailEnabled = Config.Bind("Runner", "Tail", true,
                "The Runner lashes with its tail at a second body standing "
                + "beside it - not the one it is biting. See XenoTail.");

            XenoTailDamage = Config.Bind("Runner", "TailDamage", 6f,
                "What that lash takes off. It goes through the game's own "
                + "damage call, so the acid blood answers it like any other hit.");

            XenoBiteSound = Config.Bind("Runner", "BiteSound", "BearAttack",
                "The sound key a Runner's bite plays, layered over SoftBody. "
                + "Keys are the game's own - BearAttack, WolfAttack, BoarAttack, "
                + "CriticalStrike - because a mod cannot add to an "
                + "FMOD bank. Empty is silence. See XenoNoise.");

            XenoLashSound = Config.Bind("Runner", "LashSound", "CriticalStrike",
                "The same, for the tail.");

            // BigSlash no existe en el banco: el log del 24 lo dice 111 veces
            // ("Sound BigSlash not found in Global.prefab"). Quien lo tenga
            // guardado en su cfg de antes pasa al que el juego si usa.
            if (XenoLashSound.Value == "BigSlash") XenoLashSound.Value = "CriticalStrike";
            if (XenoBiteSound.Value == "BigSlash") XenoBiteSound.Value = "BearAttack";

            FacehuggerEnabled = Config.Bind("Runner", "SpiderAlien", true,
                "Eggs hatch, huggers jump at a face, and what they hold does "
                + "not starve, dry out, freeze or fall asleep - it dies when "
                + "the xeno comes out. See Facehugger.cs.");

            FacehuggerHours = Config.Bind("Runner", "SpiderAlienHours", 48f,
                "Game hours from the jump to the xeno. The clock is the game's "
                + "own date, so speed and saving do not touch it.");

            XenoAcidBlood = Config.Bind("Runner", "AcidBlood", true,
                "A wounded Runner sprays acid, not blood: the hit particles go "
                + "green, whoever wounded it up close can be burned, and a dead "
                + "one leaves a pool that burns whoever walks into it.");

            XenoAcidSplash = Config.Bind("Runner", "AcidSplash", 0.5f,
                "Chance that the one who just wounded a Runner in melee catches "
                + "an acid burn. Bystanders next to it get a third of this, and "
                + "a corpse's pool about a third again.");

            XenoDreadRadius = Config.Bind("Runner", "DreadRadius", 14,
                "How close a live Runner has to be, in tiles, for a settler to "
                + "carry the XenoDread thought. 0 turns the fear off.");

            XenoPanicRadius = Config.Bind("Runner", "PanicRadius", 6,
                "Inside this many tiles the thought becomes XenoPanic, which is "
                + "worse and also shakes the hands.");

            XenoPanicFlee = Config.Bind("Runner", "PanicFlee", true,
                "Inside PanicRadius an undrafted settler drops what it is doing "
                + "and runs, with the game's own flee. Once every 20 s at most.");

            XenoClimbEnabled = Config.Bind("Runner", "Climb", true,
                "A Runner that cannot get closer to a settler goes up the wall in "
                + "its way and over it, or stays on top if it can stand there.");

            XenoClimbLevels = Config.Bind("Runner", "ClimbLevels", 12,
                "Tallest wall a Runner climbs, in levels. A tower is just a taller wall.");
        }

        private static void PatchRunner(Harmony harmony)
        {
            PatchOne(harmony, typeof(XenoRunnerModel));
            PatchOne(harmony, typeof(XenoHiveModel));
            PatchOne(harmony, typeof(XenoCorpse));
            PatchOne(harmony, typeof(XenoAcidOnHit));
            PatchOne(harmony, typeof(XenoAcidParticles));
        }

        private static void StartRunner()
        {
            Facehugger.Start();
            XenoTail.Start();
            XenoClimb.Start();
            XenoAcid.Start();
            XenoDread.Start();
        }
    }
}
