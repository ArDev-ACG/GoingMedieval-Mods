using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NSMedieval.Repository;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Makes a mod's sprites usable *inside text*, which is where role icons
    /// actually live.
    ///
    /// The log had been saying this for days and it read as harmless noise:
    ///
    ///     [WARN] [AssetUtils] There is not Sprite Asset at address: "aldrich_role_count"   x112
    ///
    /// It is not the same thing as a missing sprite. The game draws a role icon
    /// beside a settler's name by writing a TextMeshPro tag into the label -
    /// AssetUtils.GetSpriteAssetFormat builds one out of the icon's name - and
    /// TMP can only resolve that tag if a TMP_SpriteAsset is registered under
    /// that address in SpriteAssetRepository. ModInstance.LoadSprites() fills
    /// SpriteRepository, which covers every place the icon is drawn as an
    /// Image, and nothing at all fills the sprite-asset side. So the shield
    /// rendered everywhere except the one place the player looks most: next to
    /// the name of the settler holding the role.
    ///
    /// Each PNG therefore gets a one-glyph TMP_SpriteAsset built around it at
    /// runtime and registered under its own file name, alongside the Image
    /// sprite the game already made. The material is cloned from TMP's own
    /// default sprite asset rather than conjured with Shader.Find, because the
    /// shader that survived the build's stripping is the one TMP is already
    /// using.
    ///
    /// The metrics are deliberately the simple case: one glyph filling the
    /// whole texture, a baseline offset of 80% of its height so the icon sits
    /// on the line rather than under it, and an advance equal to its width.
    ///
    /// <b>It has to be lazy.</b> The first version hung off
    /// SpriteAssetRepository.OnStart, which runs while the repositories come
    /// up - a good half second <em>before</em> ModManager enables a single mod.
    /// GameAccess.AllMods() was empty, nothing was registered, and a "did this
    /// already" flag made sure it never tried again: the whole class did
    /// nothing at all, silently, and the 202 warnings stayed.
    ///
    /// So the hook is the question itself. AssetUtils.SpriteAssetExists is what
    /// the game asks before it writes a sprite tag, and every other overload
    /// funnels through it. A "no" for a name one of our mods ships is answered
    /// by building the asset there and then and turning it into a "yes" - which
    /// also means the work only happens for icons that are actually used.
    /// </summary>
    [HarmonyPatch]
    internal static class ModSpriteAssets
    {
        private const string SpriteFolder = "Sprites";

        /// <summary>
        /// The format TMP 3.x writes and reads. Anything else - an empty string
        /// included - is treated as "needs upgrading".
        /// </summary>
        private const string TmpSpriteAssetVersion = "1.1.0";

        /// <summary>PNG per sprite name, filled the first time a mod is loaded.</summary>
        private static Dictionary<string, string> pathByName;

        /// <summary>Names already answered for - ours registered, everyone else's not.</summary>
        private static readonly HashSet<string> Settled = new HashSet<string>();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(NSMedieval.UI.Utils.AssetUtils), "SpriteAssetExists");
        }

        private static void Postfix(string spriteId, ref bool __result)
        {
            if (__result || string.IsNullOrEmpty(spriteId)) return;
            if (Settled.Contains(spriteId)) return;

            var paths = SpritePaths();
            if (paths == null) return;   // no mods yet; ask again next time

            Settled.Add(spriteId);
            if (!paths.TryGetValue(spriteId, out var path)) return;

            if (Register(SpriteAssetRepository.Instance, SpriteAssetRepository.SpriteAssetNames, path))
            {
                __result = true;
                GMPlugin.Log?.LogInfo($"[sprite] {spriteId} is now usable inside text");
            }
        }

        /// <summary>
        /// Every PNG our mods ship, by name. Not cached until at least one mod
        /// has been seen, because an empty answer here is the exact mistake the
        /// first version made permanent.
        /// </summary>
        private static Dictionary<string, string> SpritePaths()
        {
            if (pathByName != null) return pathByName;

            var found = new Dictionary<string, string>();
            var anyMod = false;

            foreach (var mod in GameAccess.AllMods())
            {
                if (mod == null || string.IsNullOrEmpty(mod.DataPath)) continue;
                anyMod = true;

                var folder = Path.Combine(mod.DataPath, SpriteFolder);
                if (!Directory.Exists(folder)) continue;

                foreach (var file in Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
                {
                    found[Path.GetFileNameWithoutExtension(file)] = file;
                }
            }

            if (!anyMod) return null;

            pathByName = found;
            return pathByName;
        }

        private static bool Register(SpriteAssetRepository repository, HashSet<string> names, string path)
        {
            var key = Path.GetFileNameWithoutExtension(path);
            try
            {
                if (names != null && names.Contains(key)) return false;

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = key,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };

                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    GMPlugin.Log?.LogError($"[sprite] {key} is not a readable PNG");
                    return false;
                }

                var asset = Build(key, texture);
                if (asset == null) return false;

                if (repository == null)
                {
                    GMPlugin.Log?.LogError($"[sprite] no SpriteAssetRepository to register {key} in");
                    return false;
                }

                repository.AddNewObject(key, asset, true);
                names?.Add(key);
                return true;
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[sprite] failed to register {key}: {e}");
                return false;
            }
        }

        private static TMP_SpriteAsset Build(string key, Texture2D texture)
        {
            var material = SpriteMaterial(texture);
            if (material == null)
            {
                GMPlugin.Log?.LogError($"[sprite] no TMP sprite material to clone for {key}");
                return null;
            }

            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = key;

            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = key;
            asset.spriteSheet = texture;
            asset.material = material;

            // Has to happen before the first read of either table, and this is
            // what kept every icon showing the toolbox placeholder:
            //
            //     [sprite] failed to register aldrich_role_count:
            //       NullReferenceException at TMP_SpriteAsset.UpgradeSpriteAsset()
            //
            // spriteCharacterTable's getter calls UpdateLookupTables(), which
            // reads "material != null && string.IsNullOrEmpty(m_Version)" as
            // "this asset was serialised by an older TMP" and runs the
            // 1.0 -> 1.1 upgrade. That upgrade walks spriteInfoList - the
            // legacy list - which on an asset built with CreateInstance is
            // null, so it threw on the very line meant to fill the table. The
            // asset never reached the repository, SpriteAssetExists stayed
            // false, and AssetUtils.GetSpriteAsset fell back to "protoAsset",
            // which is the box of tools the player kept seeing.
            //
            // Stamping the current version says the asset is already 1.1, and
            // the empty list keeps an upgrade harmless if some other path ever
            // forces one anyway.
            StampCurrentVersion(asset, key);

            // The three tables are read-only properties over fields TMP itself
            // initialises, so they are never the null here. Left as a note so
            // nobody spends another evening on them: the first crash was
            // upstream, in TMP_Settings.defaultSpriteAsset, which throws
            // instead of returning null when there is no settings asset around.
            var glyph = new TMP_SpriteGlyph
            {
                index = 0,
                metrics = new GlyphMetrics(texture.width, texture.height, 0f,
                    texture.height * 0.8f, texture.width),
                glyphRect = new GlyphRect(0, 0, texture.width, texture.height),
                scale = 1f,
                sprite = sprite,
            };

            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0, glyph)
            {
                name = key,
                scale = 1f,
            });

            asset.UpdateLookupTables();
            return asset;
        }

        /// <summary>
        /// Tells TMP the freshly built asset is already in its current format.
        ///
        /// <c>m_Version</c> is private and serialized; the public
        /// <c>version</c> property is the same field but its setter is not
        /// guaranteed to be reachable from outside the assembly, so the field
        /// is written directly and the property is only the fallback. If
        /// neither works the asset is still returned - it will simply throw
        /// again in <c>UpdateLookupTables</c>, and the log will say why.
        /// </summary>
        private static void StampCurrentVersion(TMP_SpriteAsset asset, string key)
        {
            asset.spriteInfoList = new List<TMP_Sprite>();

            var field = AccessTools.Field(typeof(TMP_SpriteAsset), "m_Version");
            if (field != null)
            {
                field.SetValue(asset, TmpSpriteAssetVersion);
                return;
            }

            GMPlugin.Log?.LogWarning(
                $"[sprite] TMP_SpriteAsset.m_Version not found - {key} may fail to upgrade");
        }

        /// <summary>
        /// TMP's own sprite material, retextured.
        /// </summary>
        private static Material SpriteMaterial(Texture2D texture)
        {
            // TMP_Settings.defaultSpriteAsset does not return null when there is
            // no settings asset loaded - it dereferences a null singleton and
            // throws - so the whole lookup sits inside the guard rather than
            // being null-checked afterwards.
            try
            {
                var template = TMP_Settings.defaultSpriteAsset != null
                    ? TMP_Settings.defaultSpriteAsset.material
                    : null;

                if (template != null) return new Material(template) { mainTexture = texture };
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogWarning($"[sprite] TMP default sprite asset unusable: {e.Message}");
            }

            var shader = Shader.Find("TextMeshPro/Sprite") ?? Shader.Find("TextMeshPro/Distance Field");
            return shader == null ? null : new Material(shader) { mainTexture = texture };
        }

    }
}
