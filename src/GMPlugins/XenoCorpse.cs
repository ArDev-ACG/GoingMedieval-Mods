using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NSEipix.Repository;
using NSMedieval.Model;
using NSMedieval.Repository;
using NSMedieval.State;
using NSMedieval.Views.Resources;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Lo que queda de un xeno en el suelo es un xeno, no un lobo.
    ///
    /// <b>Por que volvia a ser un lobo.</b> Al morir un animal el juego no deja
    /// su cuerpo: lo quita y pone en su sitio una <b>pila de recurso</b>, la que
    /// dice <c>carcassResourceId</c> en su fase de vida. Los tres de la colmena
    /// decian <c>wolf_carcass</c>, y esa pila trae su malla de lobo muerto, su
    /// textura, su nombre y su "info" - que es el tooltip -. El log del 24 lo
    /// deja ver sin buscarlo: "Could not find free node to spawn wolf_carcass".
    ///
    /// <b>Lo que hay ahora.</b> Cada uno tiene su recurso en
    /// <c>XenomorphRunner/Data/Models/Resources.json</c> - nombre y descripcion
    /// en ingles y espanol, peso, pudrirse en huesos, y despiece en la mesa de
    /// carniceria por <c>Production.json</c> -, y este parche le pone encima de
    /// la pila el cuerpo de verdad: el Corredor de lado, el abrazacaras patas
    /// arriba y el huevo abierto y hundido. La malla de lobo de la pila se
    /// esconde con <c>forceRenderingOff</c>, como en los vivos.
    ///
    /// <b>Las vistas de pila se reciclan</b>, asi que cada Setup empieza
    /// quitando lo que este parche hubiera puesto antes: una pila de madera no
    /// hereda un xeno muerto.
    /// </summary>
    [HarmonyPatch]
    internal static class XenoCorpse
    {
        internal const string RunnerCarcass = "xeno_runner_carcass";
        internal const string HuggerCarcass = "aldrich_facehugger_carcass";
        internal const string EggRemains = "aldrich_xeno_egg_remains";

        private const string ChildName = "AldrichRemains";

        private sealed class Remains
        {
            internal string Mesh;
            internal string Texture;
            internal bool Skinned;

            /// <summary>Lo largo que queda en el suelo, en metros; 0 es "lo que mida el lobo".</summary>
            internal float Length;

            /// <summary>Como queda tumbado.</summary>
            internal Vector3 Lie;

            /// <summary>Aplastado en vertical: un huevo vacio no esta lleno.</summary>
            internal float Squash = 1f;
        }

        private static readonly Dictionary<string, Remains> Bodies = new Dictionary<string, Remains>
        {
            {
                RunnerCarcass, new Remains
                {
                    Mesh = "aldrich_xeno_runner", Texture = "aldrich_xeno_runner.png",
                    Skinned = true, Length = 0f, Lie = new Vector3(0f, 0f, 90f),
                }
            },
            {
                HuggerCarcass, new Remains
                {
                    Mesh = "aldrich_facehugger", Texture = "aldrich_facehugger.png",
                    Length = 0.6f, Lie = new Vector3(0f, 0f, 180f),
                }
            },
            {
                EggRemains, new Remains
                {
                    Mesh = "aldrich_xeno_egg", Texture = "aldrich_xeno_egg.png",
                    Length = 0.9f, Lie = new Vector3(8f, 0f, 0f), Squash = 0.7f,
                }
            },
        };

        private static readonly int Albedo = Shader.PropertyToID("_Albedo");
        private static readonly int Diffuse = Shader.PropertyToID("_Diffuse");
        private static readonly int Tint = Shader.PropertyToID("_BaseColorMultiplier");

        private static readonly AccessTools.FieldRef<ResourcePileView, Transform> MeshTransformRef =
            AccessTools.FieldRefAccess<ResourcePileView, Transform>("meshTransform");

        private static readonly HashSet<string> Warned = new HashSet<string>();
        private static readonly HashSet<string> Said = new HashSet<string>();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ResourcePileView), "Setup", new[] { typeof(ResourcePileInstance) });
        }

        private static void Postfix(ResourcePileView __instance, ResourcePileInstance resourcePileInstance)
        {
            try
            {
                if (__instance == null) return;

                var pile = MeshTransformRef(__instance);
                if (pile == null) return;

                Undress(__instance.transform, pile);

                var id = resourcePileInstance?.Blueprint?.GetID();
                Remains remains;
                if (id == null || !Bodies.TryGetValue(id, out remains)) return;
                if (!GMPlugin.XenoOwnModel.Value) return;

                Dress(__instance.transform, pile, remains, id);
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[corpse] could not dress a carcass: {e}");
            }
        }

        private static void Undress(Transform root, Transform pile)
        {
            var old = root.Find(ChildName);
            if (old == null) return;

            UnityEngine.Object.Destroy(old.gameObject);
            foreach (var r in pile.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = false;
        }

        private static void Dress(Transform root, Transform pile, Remains remains, string id)
        {
            var prefab = MonoRepository<MeshRepository, KeyGameObjectPair>.Instance?.GetByAddress(remains.Mesh);
            if (prefab == null)
            {
                if (Warned.Add(id))
                {
                    GMPlugin.Log?.LogWarning(
                        $"[corpse] '{remains.Mesh}' no esta en MeshRepository: '{id}' se queda con la malla del lobo");
                }
                return;
            }

            Renderer wolf = null;
            foreach (var r in pile.GetComponentsInChildren<Renderer>(true))
            {
                if (r.enabled) { wolf = r; break; }
            }
            if (wolf == null) return;

            var wolfBounds = wolf.bounds;
            var wolfLength = Mathf.Max(wolfBounds.size.x, wolfBounds.size.z);

            var model = UnityEngine.Object.Instantiate(prefab, root, false);
            model.name = ChildName;
            SetLayer(model.transform, wolf.gameObject.layer);

            var ours = remains.Skinned
                ? model.GetComponentInChildren<SkinnedMeshRenderer>(true)
                : model.GetComponentInChildren<Renderer>(true);
            if (ours == null)
            {
                UnityEngine.Object.Destroy(model);
                return;
            }

            if (remains.Skinned) Pose(model, (SkinnedMeshRenderer)ours);

            ours.sharedMaterial = wolf.sharedMaterial;
            ours.shadowCastingMode = wolf.shadowCastingMode;
            ours.receiveShadows = wolf.receiveShadows;

            // Tumbado primero y medido despues: lo largo que queda en el suelo
            // es lo que importa, no lo largo que era de pie.
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(remains.Lie);
            model.transform.localScale = Vector3.one;

            var size = ours.bounds.size;
            var length = Mathf.Max(0.0001f, Mathf.Max(size.x, size.z));
            var wanted = remains.Length > 0f ? remains.Length : wolfLength;
            if (remains.Skinned) wanted = Mathf.Min(wanted * 1.1f, GMPlugin.XenoMaxLength.Value);

            var scale = wanted / length;
            model.transform.localScale = new Vector3(scale, scale * remains.Squash, scale);

            // Apoyado en el suelo de la pila, ni enterrado ni flotando.
            var bottom = ours.bounds.min.y;
            model.transform.position += Vector3.up * (root.position.y - bottom);

            var block = new MaterialPropertyBlock();
            var texture = remains.Skinned ? XenoRunnerModel.Texture() : XenoHiveModel.Texture(remains.Texture);
            if (texture != null)
            {
                block.SetTexture(Albedo, texture);
                block.SetTexture(Diffuse, texture);
            }
            block.SetColor(Tint, Color.white);
            ours.SetPropertyBlock(block);

            foreach (var r in pile.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = true;

            if (Said.Add(id))
            {
                GMPlugin.Log?.LogInfo(
                    $"[corpse] '{id}' lleva ya '{remains.Mesh}' ({wanted:0.00} m en el suelo)");
            }
        }

        /// <summary>
        /// Una malla con esqueleto sin animar sale en la pose de montaje, con
        /// las patas abiertas. Se congela el primer fotograma de "idle" y se
        /// para la animacion: un muerto no respira.
        /// </summary>
        private static void Pose(GameObject model, SkinnedMeshRenderer skin)
        {
            skin.updateWhenOffscreen = true;

            var clips = model.GetComponentInChildren<Animation>(true);
            if (clips == null) return;

            var idle = clips["idle"];
            if (idle != null)
            {
                idle.enabled = true;
                idle.weight = 1f;
                idle.time = 0f;
                clips.Sample();
                idle.enabled = false;
            }

            clips.Stop();
            clips.enabled = false;
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (var i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }
    }
}
