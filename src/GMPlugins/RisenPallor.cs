using System.Reflection;
using HarmonyLib;
using NSMedieval;
using NSMedieval.State;
using NSMedieval.View;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// The Risen go grey.
    ///
    /// PENDIENTES had this filed as impossible without new art, because
    /// HumanAppearance.json only has `default` and `default_enemy` and both are
    /// shared by everyone. The way through turned out to be a layer lower:
    /// skin colours are not repository entries at all, they are plain HTML
    /// strings that HumanoidBodyPreview.GetColor hands to
    /// ColorUtility.TryParseHtmlString. So any hex works, it never has to exist
    /// in a list, and painting one creature repaints nobody else.
    ///
    /// The paint has to go on before the body is drawn, not after:
    /// SetSkinColor only writes the value into the look's colour dictionary,
    /// and ShowBody is what pushes that dictionary into the material property
    /// block. Hence a prefix - it runs, ShowBody applies what it wrote.
    ///
    /// Two kinds of undead answer to this: a settler who got back up with the
    /// Risen perk, and anything spawned into the horde's own faction. The
    /// second test is what covers the walkers, since nothing in NPCs.json can
    /// hand them a perk.
    ///
    /// This is a stand-in for real rotten-skin textures, which need Unity. It
    /// buys the one thing that actually matters in play: telling the Risen
    /// apart from the living at a glance.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenPallor
    {
        internal const string RisenPerk = Census.RisenPerk;
        internal const string UndeadFactionId = Census.UndeadFactionId;
        internal const string UndeadBlueprintPrefix = Census.UndeadBlueprintPrefix;

        private static readonly System.Collections.Generic.HashSet<string> Painted =
            new System.Collections.Generic.HashSet<string>();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(HumanoidBodyPreview), "ShowBody");
        }

        private static void Prefix(HumanoidBodyPreview __instance)
        {
            if (__instance == null) return;

            string skin, hair;
            if (!Palette(__instance.HumanoidInstance, out skin, out hair)) return;

            try
            {
                Repaint(__instance, skin, hair);

                // Una linea por criatura, no una por redibujado: ShowBody corre
                // cada vez que el cuerpo se rearma y esto llenaria el log. La
                // clave es el nombre y el uniqueId, no el id del blueprint: ese
                // es "undead_horde_walker_easy" para toda la horda, asi que una
                // sola linea tapaba a los treinta.
                var who = GameAccess.Name(__instance.HumanoidInstance);
                if (Painted.Add(who))
                {
                    GMPlugin.Log?.LogInfo($"[pallor] {who} painted {skin} / hair {hair}");
                }
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[pallor] could not repaint: {e.Message}");
            }
        }

        /// <summary>
        /// A settler who got back up, or anything the horde spawned.
        ///
        /// The blueprint id is the test that actually works. `factionId` holds
        /// the id of the faction <em>instance</em> the raid was built from, not
        /// the "undead_horde" of FactionRepository.json, so that comparison was
        /// always false; and the Risen perk was being deleted from the
        /// repository outright by `hideInGame: true`, so that half was false
        /// too. Between them the walkers came out the colour of the living and
        /// the log never said a word, because there was nothing to say.
        ///
        /// The blueprint id cannot lie: it is the string the game itself prints
        /// when it spawns one - `undead_horde_walker_easy`.
        /// </summary>

        /// <summary>
        /// The one slot that is not skin. `WorkerPhysicalLook.Initialize`
        /// creates exactly two - "_SkinColor" and this - so naming this one is
        /// enough to tell them apart.
        /// </summary>
        private const string HairSlot = "_HairColor";

        /// <summary>
        /// Paints every colour slot the body has - but not all of them the same.
        ///
        /// `SetSkinColor` writes one entry - `ShaderParameters[0]` - into the
        /// look's colour dictionary, and `ApplyBodyColors` then pushes *every*
        /// entry of that dictionary into the material property block. So a
        /// walker painted with SetSkinColor alone kept its living hair, which on
        /// screen is a horde "de todos los colores" with slightly grey arms.
        ///
        /// Writing the whole dictionary fixed that and introduced the opposite
        /// problem: `WorkerPhysicalLook.Initialize` only ever creates two slots,
        /// _SkinColor and _HairColor, so one colour for both is a figure moulded
        /// from a single lump of clay. Real bodies do not go monochrome - hair
        /// keeps almost none of its colour but stays far darker than the skin -
        /// and that one contrast is most of what makes a silhouette read as a
        /// corpse rather than a statue.
        ///
        /// Anything that is not one of the two known slots gets the skin colour,
        /// so a game version that adds a third slot degrades into the old
        /// behaviour instead of leaving it bright pink.
        ///
        /// The keys are copied out before writing: the dictionary is the one
        /// being modified.
        /// </summary>
        private static void Repaint(HumanoidBodyPreview preview, string skin, string hair)
        {
            var colors = preview.GetInfo()?.PhysicalLook?.BodyColors;
            if (colors == null || colors.Count == 0)
            {
                preview.SetSkinColor(skin);
                return;
            }

            var slots = new string[colors.Count];
            colors.Keys.CopyTo(slots, 0);

            foreach (var slot in slots)
            {
                colors[slot] = slot == HairSlot ? hair : skin;
            }
        }

        /// <summary>
        /// Which of the two palettes this body is painted from, if any.
        ///
        /// The Risen are a corpse that got up: green-grey, and it is meant to
        /// read as rot. A vampire is not rotting - it is bloodless - so it gets
        /// its own pair of colours, paler and cooler, and a ghoul sits with the
        /// vampires because it was made the same way.
        ///
        /// Both are plain HTML strings on their way to
        /// <c>ColorUtility.TryParseHtmlString</c>, so neither has to exist in
        /// any repository and painting one creature repaints nobody else. An
        /// empty colour in the config means "leave this kind alone", which is
        /// how a player turns off half the rule without turning off the other.
        /// </summary>
        internal static bool Palette(HumanoidInstance humanoid, out string skin, out string hair)
        {
            skin = null;
            hair = null;
            if (humanoid == null) return false;

            if (Census.IsUndead(humanoid))
            {
                skin = Mottled(humanoid, GMPlugin.RisenSkinColor?.Value);
                hair = GMPlugin.RisenHairColor?.Value;
            }
            else if (GameAccess.HasPerk(humanoid, Undead.VampirePerk)
                     || GameAccess.HasPerk(humanoid, Undead.GhoulPerk))
            {
                skin = GMPlugin.VampireSkinColor?.Value;
                hair = GMPlugin.VampireHairColor?.Value;
            }

            if (string.IsNullOrEmpty(skin)) return false;

            // No hair colour configured means "paint it like the skin", which
            // is what this used to do for every slot.
            if (string.IsNullOrEmpty(hair)) hair = skin;
            return true;
        }

        /// <summary>
        /// Corre el gris configurado un poco, y siempre igual para la misma
        /// criatura.
        ///
        /// <b>Por que no es una textura.</b> El ticket pedia piel putrefacta en
        /// vez de un hex plano, y la piel del cuerpo es una textura del material
        /// de vanilla: no hay ranura en ningun JSON donde nombrar otra - las
        /// unicas dos palancas que el juego expone son <c>_SkinColor</c> y
        /// <c>_HairColor</c>, y las dos son un color, no una imagen. Lo que si
        /// se puede quitar es lo que delataba el truco: cuarenta alzados con
        /// exactamente el mismo gris se leen como copias del mismo modelo.
        ///
        /// El desvio sale del identificador de la criatura y no de un
        /// generador, porque <c>ShowBody</c> corre en cada redibujado: un valor
        /// al azar daria un alzado que cambia de color al salir y entrar en
        /// pantalla. Y va en verde y amarillo mas que en azul, que es como se
        /// apaga la carne.
        ///
        /// La cara demacrada, que era la otra mitad del ticket, no esta aqui:
        /// es <c>aldrich_risen</c> en el <c>HumanType.json</c> y el
        /// <c>HumanAppearance.json</c> del mod, que le dejan a los alzados solo
        /// las cabezas largas y cuadradas y les quitan barba y bigote.
        /// </summary>
        private static string Mottled(HumanoidInstance humanoid, string hex)
        {
            if (string.IsNullOrEmpty(hex) || !(GMPlugin.RisenPallorVariety?.Value ?? true))
                return hex;

            UnityEngine.Color colour;
            if (!UnityEngine.ColorUtility.TryParseHtmlString(hex, out colour)) return hex;

            var id = GameAccess.Id(humanoid) + "#" + humanoid.GetHashCode();
            var seed = 0;
            foreach (var c in id) seed = unchecked(seed * 31 + c);

            // +-6% en rojo y verde, la mitad en azul: un rango mas ancho saca
            // alzados verdes de dibujo animado.
            var r = Shift(colour.r, seed, 0.06f);
            var g = Shift(colour.g, seed >> 7, 0.06f);
            var b = Shift(colour.b, seed >> 14, 0.03f);

            return "#" + UnityEngine.ColorUtility.ToHtmlStringRGB(
                new UnityEngine.Color(r, g, b, 1f));
        }

        private static float Shift(float value, int seed, float range)
        {
            var step = ((seed & 0xFF) / 255f - 0.5f) * 2f * range;
            return UnityEngine.Mathf.Clamp01(value + step);
        }

    }

    /// <summary>
    /// A settler who turns mid-game already has a body on screen, and nothing
    /// redraws it on its own. Granting the perk is the moment to ask for one.
    /// </summary>
    [HarmonyPatch]
    internal static class RisenPallorRefresh
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(HumanoidInstance), "TryAddNewPerk");
        }

        private static void Postfix(HumanoidInstance __instance, string perkId)
        {
            if (__instance == null) return;

            // "si se convierte no lo convierte en gris": this listened for the
            // Risen perk and nothing else, so the one case where a body is
            // already on screen and needs repainting - a settler turned into a
            // vampire or a ghoul in front of the player - was the one case it
            // ignored. All three now ask for the redraw.
            if (perkId != RisenPallor.RisenPerk
                && perkId != Undead.VampirePerk
                && perkId != Undead.GhoulPerk) return;

            try
            {
                var view = __instance.GetAgentView<HumanoidView>();
                var body = view?.BodyPreview;
                if (body == null) return;

                body.ShowBody();
                GMPlugin.Log?.LogInfo($"[pallor] {GameAccess.Name(__instance)} went grey on {perkId}");
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[pallor] could not refresh body: {e.Message}");
            }
        }
    }
}
