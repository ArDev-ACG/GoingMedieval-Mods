using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NSMedieval.Repository;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Lets a mod ship its own mesh textures as loose PNGs.
    ///
    /// ModInstance already carries a Textures dictionary, but nothing ever
    /// fills it: LoadSprites() only reads Data/Sprites and only produces UI
    /// sprites. Mesh textures come from somewhere else entirely - a building's
    /// "albedo" slot is an addressable name that SlotRendererPair.ApplyTexture
    /// resolves through TextureRepository.GetByAddress - so without this patch
    /// the only way to repaint a vanilla mesh is a Unity bundle.
    ///
    /// TextureRepository overrides AddNewObject(key, obj, overwrite), which is
    /// all that is needed: every PNG under &lt;Mod&gt;/Data/Textures is loaded once
    /// and registered under its own file name, and from then on a JSON slot can
    /// name it like any vanilla texture. Registration is deferred to the first
    /// mesh that asks for a texture, because the repository is a MonoBehaviour
    /// and does not exist yet while BepInEx is still patching.
    /// </summary>
    [HarmonyPatch]
    internal static class ModTextures
    {
        private const string TextureFolder = "Textures";

        private static bool loaded;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(NSMedieval.Views.Resources.MeshVariationHandler),
                "UpdateTextures");
        }

        private static void Prefix()
        {
            if (loaded) return;

            var repository = UnityEngine.Object.FindObjectOfType<TextureRepository>();
            if (repository == null) return;   // not up yet; try again on the next mesh

            loaded = true;
            foreach (var path in ModTexturePaths())
            {
                Register(repository, path);
            }
        }

        private static IEnumerable<string> ModTexturePaths()
        {
            foreach (var mod in GameAccess.AllMods())
            {
                if (mod == null || string.IsNullOrEmpty(mod.DataPath)) continue;

                var folder = Path.Combine(mod.DataPath, TextureFolder);
                if (!Directory.Exists(folder)) continue;

                foreach (var file in Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
                {
                    yield return file;
                }
            }
        }

        private static void Register(TextureRepository repository, string path)
        {
            var key = Path.GetFileNameWithoutExtension(path);
            try
            {
                // Mipmaps on: these sit on world meshes that are seen from far
                // enough away that a non-mipped texture shimmers.
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
                {
                    name = key,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };

                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    GMPlugin.Log?.LogError($"[tex] {key} is not a readable PNG");
                    return;
                }

                repository.AddNewObject(key, texture, true);
                GMPlugin.Log?.LogInfo($"[tex] registered {key} ({texture.width}x{texture.height})");
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[tex] failed to register {key}: {e.Message}");
            }
        }
    }
}
