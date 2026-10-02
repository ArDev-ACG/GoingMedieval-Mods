using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Lo que alzados y vampiros tienen en comun por estar muertos: no se
    /// desmayan, los cadaveres no les afectan y tienen su color de piel. Lo
    /// compilan Undead Horde y Vampire Court, cada uno con estas claves en su
    /// cfg; con los dos instalados lo aplica el que carga primero.
    /// </summary>
    public partial class GMPlugin
    {
        /// <summary>
        /// Whether the dead can be knocked unconscious. Off by default: the
        /// thirst and the sunburn both push Consciousness down hard enough to
        /// drop a vampire in its own yard, which reads as a broken settler.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> UndeadNeverFaint;
        /// <summary>
        /// Whether corpses still upset the dead - seeing one, butchering one,
        /// eating one. Off by default, for the obvious reason.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> UndeadIgnoreCorpses;
        internal static BepInEx.Configuration.ConfigEntry<bool> RisenPallorVariety;
        /// <summary>
        /// The skin the Risen get. Any HTML colour the game can parse works -
        /// skin colours in HumanAppearance.json are plain hex strings read
        /// through ColorUtility, so this never has to exist in a repository.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<string> RisenSkinColor;
        /// <summary>
        /// The hair a Risen gets, painted separately from the skin.
        ///
        /// A body has exactly two colour slots - _SkinColor and _HairColor -
        /// and washing both in the same grey is what made a horde read as a row
        /// of clay figures rather than corpses. Dead hair keeps almost no
        /// colour but stays far darker than dead skin, and that one contrast is
        /// most of what tells the eye it is looking at a body.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<string> RisenHairColor;
        /// <summary>
        /// The skin a Vampire or a Ghoul gets, which is not the Risen's.
        ///
        /// A Risen is rotting and reads green-grey; a vampire is simply out of
        /// blood, so it wants a colder, paler colour. Same mechanism either
        /// way - a hex string on its way to ColorUtility - and empty leaves
        /// them the colour they were born.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<string> VampireSkinColor;
        /// <summary>The hair that goes with it, painted separately.</summary>
        internal static BepInEx.Configuration.ConfigEntry<string> VampireHairColor;

        private void BindKin()
        {
            UndeadNeverFaint = Config.Bind("Undead", "NeverFaint", true,
                "The dead do not pass out. Off, a starving vampire ends up "
                + "face down in the yard, because the thirst drains "
                + "Consciousness and that is what the game faints on.");

            UndeadIgnoreCorpses = Config.Bind("Undead", "IgnoreCorpses", true,
                "Corpses stop upsetting the dead - seeing one, butchering one, "
                + "cooking one, eating one, wearing what came off one.");

            RisenPallorVariety = Config.Bind("Undead", "RisenPallorVariety", true,
                "Corre el gris de cada alzado un poco, siempre igual para el "
                + "mismo, en vez de pintarlos a los cuarenta con el mismo hex. "
                + "La piel podrida de verdad es una textura del cuerpo y esa no "
                + "se puede cambiar desde un mod; esto es lo que si se puede.");

            RisenSkinColor = Config.Bind("Undead", "RisenSkinColor", "#7B8770",
                "Skin colour for anyone carrying the Risen perk. Empty leaves "
                + "them the colour they were. Try #9AA3A8 for freshly dead, "
                + "#B9AEB0 for pallid, #6A7358 for well past ripe.");

            RisenHairColor = Config.Bind("Undead", "RisenHairColor", "#2B2620",
                "Hair colour for the Risen, painted separately from the skin. "
                + "Empty paints it the same as the skin, which is what made a "
                + "horde look like clay figures.");

            // A vampire is not rotting, it is exsanguinated: the Risen's
            // green-grey would be wrong on one. #C6C2BE is bloodless rather
            // than dead - try #D6CFC7 for porcelain, #AFB4BA for a colder one.
            VampireSkinColor = Config.Bind("Vampire", "VampireSkinColor", "#C6C2BE",
                "Skin colour for anyone carrying the Vampire or Ghoul perk. "
                + "Empty leaves them the colour they were. Try #D6CFC7 for "
                + "porcelain, #AFB4BA for colder.");

            VampireHairColor = Config.Bind("Vampire", "VampireHairColor", "#1E1A19",
                "Hair colour for a Vampire or a Ghoul, painted separately from "
                + "the skin. Empty paints it the same as the skin.");
        }

        private static void PatchKin(Harmony harmony)
        {
            if (Shared.Claim(nameof(RisenPallor))) PatchOne(harmony, typeof(RisenPallor));
            if (Shared.Claim(nameof(UndeadNeverFaints))) PatchOne(harmony, typeof(UndeadNeverFaints));
            if (Shared.Claim(nameof(UndeadNeverFaintsHumanoid))) PatchOne(harmony, typeof(UndeadNeverFaintsHumanoid));
            if (Shared.Claim(nameof(UndeadUnmoved))) PatchOne(harmony, typeof(UndeadUnmoved));
        }
    }
}
