using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using NSMedieval.Construction;
using NSMedieval.Views.Resources;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Says, once per mesh of ours, what the game actually ended up drawing:
    /// the mesh, how many submeshes it has, how many materials the renderer
    /// carries, and every texture on each of them.
    ///
    /// <b>Why it moved.</b> The first version of this hung off
    /// <c>BaseBuildingViewComponent.InitModelSettings</c>, which is the wrong
    /// end of the story: that method sets up colliders, and the mesh and the
    /// textures are written later, by
    /// <c>MeshVariationHandler.UpdateMeshVariation</c>. So the probe read the
    /// pooled prefab before anything of ours had been put on it and reported
    /// the throne as <c>mesh=wood_chair</c> with <c>_Albedo=&lt;null&gt;</c> -
    /// true, useless, and easy to read as proof that our texture never
    /// arrived. It had; nobody had looked after the variation was applied.
    ///
    /// <b>What it prints now, and why those three numbers.</b> The one-colour
    /// throne was a mesh with <em>three</em> submeshes - one per material slot
    /// left on it by the Blender build - on a renderer that carries a single
    /// material. Unity draws <c>min(subMeshCount, materials.Length)</c>
    /// submeshes and silently drops the rest, so the stone and the gold were
    /// never on screen and what was left was the bone group: one flat colour,
    /// the first swatch of its own palette. Every vanilla building mesh has
    /// exactly one submesh. <c>submeshes=</c> against <c>materials=</c> is that
    /// bug in two numbers.
    ///
    /// <b>Do not read anything into <c>uv=0</c>.</b> The FBX is imported with
    /// Read/Write off, as the vanilla meshes are, so <c>Mesh.uv</c> is empty to
    /// script while the GPU has the UVs perfectly well. The channel layout is
    /// checked outside the game instead, against the shipped bundle.
    ///
    /// This still fixes nothing. It is the line in the log that says whether a
    /// piece of ours is being drawn the way it was built.
    /// </summary>
    [HarmonyPatch]
    internal static class ModMeshMaterialProbe
    {
        /// <summary>One report per mesh, not one per placed building.</summary>
        private static readonly HashSet<string> Told = new HashSet<string>();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(MeshVariationHandler), "UpdateMeshVariation",
                                      new[] { typeof(MeshVariation), typeof(string) });
        }

        private static void Postfix(MeshVariationHandler __instance, MeshVariation meshVariation)
        {
            if (__instance == null || meshVariation == null) return;
            if (!(GMPlugin.MeshMaterialProbe?.Value ?? true)) return;

            // Read off the variation rather than off the blueprint: this is the
            // name the mesh repository was just asked for, so a new piece of
            // the court needs no edit here.
            var mesh = MeshName(meshVariation);
            if (string.IsNullOrEmpty(mesh) || !mesh.StartsWith("aldrich_")) return;
            if (!Told.Add(mesh)) return;

            foreach (var renderer in __instance.GetComponentsInChildren<Renderer>(true))
            {
                Report(mesh, renderer);
            }
        }

        /// <summary>
        /// The <c>baseMesh</c> slot of this variation, through Harmony rather
        /// than a direct call: <c>GetMeshName</c> is the game's own member and
        /// nothing guarantees it stays reachable from outside the assembly.
        /// </summary>
        private static string MeshName(MeshVariation variation)
        {
            return Traverse.Create(variation).Method("GetMeshName", new object[] { "baseMesh" })
                           .GetValue<string>();
        }

        private static void Report(string wanted, Renderer renderer)
        {
            if (renderer == null) return;

            var filter = renderer.GetComponent<MeshFilter>();
            var mesh = filter == null ? null : filter.sharedMesh;

            var line = new StringBuilder();
            line.Append("[mat] ").Append(wanted).Append(" / ").Append(renderer.name);
            line.Append(" mesh=").Append(mesh == null ? "<none>" : mesh.name);

            var materials = renderer.sharedMaterials;

            if (mesh != null)
            {
                // The pair of numbers the one-colour throne came down to: any
                // submesh past the last material is not drawn at all.
                line.Append(" submeshes=").Append(mesh.subMeshCount);
                line.Append(" verts=").Append(mesh.vertexCount);
            }

            line.Append(" materials=").Append(materials == null ? 0 : materials.Length);
            GMPlugin.Log?.LogInfo(line.ToString());

            if (materials == null) return;

            foreach (var material in materials)
            {
                if (material == null) continue;

                var detail = new StringBuilder();
                detail.Append("[mat]   ").Append(material.name);
                detail.Append(" shader=")
                      .Append(material.shader == null ? "<none>" : material.shader.name);

                var shader = material.shader;
                if (shader != null)
                {
                    for (var i = 0; i < shader.GetPropertyCount(); i++)
                    {
                        if (shader.GetPropertyType(i)
                            != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;

                        var name = shader.GetPropertyName(i);
                        var texture = material.GetTexture(name);

                        detail.Append(' ').Append(name).Append('=');
                        detail.Append(texture == null ? "<null>" : texture.name);

                        if (texture != null)
                        {
                            detail.Append('(').Append(texture.width).Append('x')
                                  .Append(texture.height).Append(')');
                        }

                        // A scale other than 1 pulls 0.25 and 0.75 into the
                        // same square of a 2x2 palette, which looks exactly
                        // like the bug this file was written for.
                        var scale = material.GetTextureScale(name);
                        var offset = material.GetTextureOffset(name);
                        if (scale != Vector2.one || offset != Vector2.zero)
                        {
                            detail.Append("[st ").Append(scale).Append(' ')
                                  .Append(offset).Append(']');
                        }
                    }
                }

                GMPlugin.Log?.LogInfo(detail.ToString());
            }
        }
    }
}
