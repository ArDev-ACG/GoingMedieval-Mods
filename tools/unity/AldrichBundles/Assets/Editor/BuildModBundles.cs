using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Aldrich
{
    /// <summary>
    /// Turns the meshes in ASSESTS/Modelos into an addressable bundle the game
    /// will load out of a mod folder.
    ///
    /// Run it without opening Unity:
    ///
    ///   "C:\Program Files\Unity 2022.3.46f1\Editor\Unity.exe" -batchmode -quit ^
    ///     -projectPath tools\unity\AldrichBundles ^
    ///     -executeMethod Aldrich.BuildModBundles.Run -logFile -
    ///
    /// <b>How the game finds any of this.</b> <c>ModInstance.LoadAddressable
    /// Assets</c> looks for a folder called <c>AddressableAssets</c> under the
    /// mod's <c>Data</c>, hands it to <c>AddressableModManager</c>, which takes
    /// the first <c>.json</c> in it as a content catalog and loads it. It then
    /// asks that catalog for five labels - Mesh, Texture, Sprite, SpriteAsset,
    /// Prefab - and pushes whatever comes back into the matching repository;
    /// meshes go through <c>TryAddMesh</c> into <c>MeshRepository</c>, which is
    /// where a building's <c>baseMesh</c> slot is resolved from. So the address
    /// of an entry <em>is</em> the string a building's JSON names, and the
    /// label is what decides which repository it lands in. Get either wrong and
    /// the load is silent.
    ///
    /// <b>The Addressables version is pinned on purpose.</b> The game ships
    /// Unity.Addressables 1.22.3, and a catalog is only as portable as the
    /// runtime that reads it. Packages/manifest.json asks for exactly 1.22.3
    /// for that reason, not because it happened to be current.
    /// </summary>
    public static class BuildModBundles
    {
        /// <summary>Where the FBX files come from, relative to the repo root.</summary>
        private const string Source = "ASSESTS/Modelos";

        /// <summary>Where a mod's catalog and bundles go, under the user's home.</summary>
        private const string Destination =
            "Documents/Foxy Voxel/Going Medieval/Mods/{0}/Data/AddressableAssets";

        private const string GroupName = "AldrichMeshes";
        private const string MeshLabel = "Mesh";

        /// <summary>
        /// Which mod each mesh belongs to.
        ///
        /// <b>One mod, one catalog, and no sharing.</b> A mesh lands in
        /// <c>MeshRepository</c>, which is global, so a mesh shipped by one mod
        /// does resolve for a building declared by another - right up until
        /// somebody installs only the second one, and then the building is
        /// invisible with nothing in the log to say why. The cat statue belongs
        /// to Carrion and Plague and the court's furniture to Vampire Court, so
        /// each mod gets its own build and its own catalog.
        ///
        /// A mesh not named here would end up in nobody's mod, so an unknown
        /// file is a failure rather than a default.
        /// </summary>
        private static readonly Dictionary<string, string> Mods =
            new Dictionary<string, string>
            {
                { "aldrich_count_throne", "VampireCourt" },
                { "aldrich_count_coffin", "VampireCourt" },
                { "aldrich_count_crypt", "VampireCourt" },
                { "aldrich_blood_altar", "VampireCourt" },
                { "aldrich_blood_circle", "VampireCourt" },
                { "aldrich_blood_brazier", "VampireCourt" },
                { "aldrich_blood_well", "VampireCourt" },
                { "aldrich_court_banner", "VampireCourt" },
                { "aldrich_court_banner_wall", "VampireCourt" },
                { "aldrich_court_reliquary", "VampireCourt" },
                { "aldrich_crimson_candle", "VampireCourt" },
                { "aldrich_veiled_mirror", "VampireCourt" },
                { "aldrich_vigil_table", "VampireCourt" },
                { "aldrich_impaled_stake", "VampireCourt" },
                { "aldrich_impaled_stake_02", "VampireCourt" },
                { "aldrich_impaled_stake_03", "VampireCourt" },
                { "aldrich_cat_statue", "CarrionAndPlague" },
                { "aldrich_plague_mask", "CarrionAndPlague" },
                { "aldrich_mass_pyre", "Gravedigger" },

                // Sin prefijo, y a proposito: la direccion de la malla de un arma
                // equipada tiene que ser el id del recurso, que es como el juego la
                // busca. `undead_claws` es nuestro, asi que no choca con vanilla.
                { "undead_claws", "UndeadHorde" },

                { "aldrich_xeno_runner", "XenomorphRunner" },
                { "aldrich_xeno_egg", "XenomorphRunner" },
                { "aldrich_facehugger", "XenomorphRunner" },
            };

        private const string PrefabLabel = "Prefab";

        /// <summary>
        /// Models that travel whole - hierarchy, skeleton and legacy clips -
        /// instead of as a bare mesh. They still go under the <c>Mesh</c> label
        /// (see <see cref="Register"/>): <c>MeshRepository</c> keeps the whole
        /// GameObject, and the plugin picks it up there by address.
        /// </summary>
        private static readonly Dictionary<string, string[]> AnimatedModels =
            new Dictionary<string, string[]>
            {
                // Loops first; roar plays once.
                { "aldrich_xeno_runner", new[] { "idle", "idle2", "running", "sprinting", "roar" } },
            };

        private static readonly HashSet<string> LoopingClips =
            new HashSet<string> { "idle", "idle2", "running", "sprinting" };

        public static void Run()
        {
            try
            {
                var repo = RepoRoot();
                Log("repo root " + repo);

                var from = Path.Combine(repo, Source);
                var found = Directory.Exists(from)
                    ? Directory.GetFiles(from, "*.fbx")
                    : new string[0];

                if (found.Length == 0)
                {
                    Fail("no .fbx found under " + Source);
                    return;
                }

                // Grouped by mod first, then one whole build per mod: the
                // Addressables build writes a single catalog for the whole
                // project, so two mods cannot come out of one pass.
                var byMod = new Dictionary<string, List<string>>();

                foreach (var file in found)
                {
                    var mesh = Path.GetFileNameWithoutExtension(file);

                    string mod;
                    if (!Mods.TryGetValue(mesh, out mod))
                    {
                        Fail($"'{mesh}.fbx' is not in the Mods table - say which mod owns it");
                        return;
                    }

                    if (!byMod.ContainsKey(mod)) byMod[mod] = new List<string>();
                    byMod[mod].Add(file);
                }

                foreach (var pair in byMod)
                {
                    if (!BuildFor(pair.Key, pair.Value)) return;
                }

                Done(0);
            }
            catch (Exception e)
            {
                Fail(e.ToString());
            }
        }

        /// <summary>
        /// One mod: import only its meshes, build, and publish into it.
        ///
        /// Assets/Models is emptied first by <see cref="Import"/>. Leaving the
        /// previous mod's FBX there would put it in this mod's catalog too,
        /// which is the sharing this whole split exists to stop.
        /// </summary>
        private static bool BuildFor(string mod, List<string> files)
        {
            Log($"--- {mod}: {files.Count} mesh(es) ---");

            var imported = Import(files);
            if (imported.Count == 0)
            {
                Fail("nothing imported for " + mod);
                return false;
            }

            var settings = Settings();

            // <b>The mesh bundle is not the only one with a shared name.</b>
            // Addressables writes a second bundle beside it for Unity's
            // built-in shaders, and its name is the project's - identical in
            // every mod this project builds. Unity refuses the second copy the
            // same way it refused the second mesh bundle, and the mesh bundle
            // fails with it, because the shaders are its dependency. Naming it
            // after the mod is what keeps a player who installs two of ours
            // from losing the meshes of whichever loaded last.
            settings.ShaderBundleNaming = ShaderBundleNaming.Custom;
            settings.ShaderBundleCustomNaming = "aldrich" + mod.ToLowerInvariant();

            Register(settings, Group(settings, mod), imported);

            var built = Build();
            if (built == null) return false;

            var into = Path.Combine(Home(), string.Format(Destination, mod));
            var copied = Publish(built, into);

            Log($"published {copied} file(s) to {into}");
            return true;
        }

        // ------------------------------------------------------------------
        // Getting the meshes into the project
        // ------------------------------------------------------------------

        /// <summary>
        /// Copies the named FBX files into Assets/Models and imports them.
        ///
        /// Copying rather than referencing in place because Unity will not
        /// import an asset that does not live under Assets, and a symlink is
        /// one more thing to go wrong on a machine that is not this one.
        ///
        /// <b>The folder is emptied first</b>, and that is the whole reason
        /// this takes a list rather than a folder: one build per mod means the
        /// previous mod's mesh must not still be sitting there when this one is
        /// registered, or both end up in both catalogs.
        /// </summary>
        private static List<string> Import(List<string> files)
        {
            const string into = "Assets/Models";
            Directory.CreateDirectory(into);

            foreach (var stale in Directory.GetFiles(into))
            {
                File.Delete(stale);
            }

            var paths = new List<string>();

            foreach (var fbx in files)
            {
                var name = Path.GetFileName(fbx);
                var target = Path.Combine(into, name);

                File.Copy(fbx, target, true);
                paths.Add(target.Replace('\\', '/'));

                Log("imported " + name);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            foreach (var path in paths) Configure(path);

            AssetDatabase.SaveAssets();

            // An animated model travels as a prefab of its own, not as the
            // FBX: the model asset a legacy import produces carries the clips
            // as sub-assets but no Animation component to play them, and the
            // first build shipped a skeleton that could not move.
            for (var i = 0; i < paths.Count; i++)
            {
                if (AnimatedModels.ContainsKey(Path.GetFileNameWithoutExtension(paths[i])))
                {
                    paths[i] = MakePrefab(paths[i]);
                }
            }

            AssetDatabase.SaveAssets();
            return paths;
        }

        /// <summary>
        /// The import settings these meshes need: one unit is one tile, no rig,
        /// no animation, and read/write left off because nothing reads these
        /// vertices back at runtime.
        /// </summary>
        private static void Configure(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            if (AnimatedModels.TryGetValue(Path.GetFileNameWithoutExtension(path), out var clips))
            {
                ConfigureAnimated(importer, clips);
                CheckOneSubmesh(path);
                return;
            }

            importer.globalScale = 1f;
            importer.useFileScale = false;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;

            // No materials, on purpose. The game does not use ours: a building
            // names a mesh in its baseMesh slot and then builds its own
            // material from the albedo and the shader parameters in the same
            // JSON. Importing materials only drags Unity's built-in shaders
            // into the build as a second bundle - one the game looks for under
            // its own RuntimePath rather than the mod's folder, so it would
            // never be found anyway. Leaving them out makes the bundle a mesh
            // and nothing else, which is all that has to travel.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            // The facehugger is the exception: it has no rig, and the plugin
            // moves its legs and tail by rewriting the vertices every frame.
            importer.isReadable = Path.GetFileNameWithoutExtension(path) == "aldrich_facehugger";
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;

            importer.SaveAndReimport();

            CheckOneSubmesh(path);
        }

        /// <summary>
        /// A skinned model with legacy clips: no Animator, no avatar, just an
        /// <c>Animation</c> component the plugin plays by name. The takes come
        /// out of Blender as "xeno|idle" and so on; each is renamed to the part
        /// after the bar so the plugin never has to know the rig's name.
        /// </summary>
        private static void ConfigureAnimated(ModelImporter importer, string[] wanted)
        {
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = false;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();

            var clips = new List<ModelImporterClipAnimation>();
            foreach (var take in importer.defaultClipAnimations)
            {
                var name = take.takeName.Split('|').Last();
                if (!wanted.Contains(name)) continue;

                take.name = name;
                take.loopTime = LoopingClips.Contains(name);
                take.wrapMode = take.loopTime ? WrapMode.Loop : WrapMode.Once;
                clips.Add(take);
            }

            var missing = wanted.Where(w => clips.All(c => c.name != w)).ToList();
            if (missing.Count > 0)
            {
                Fail($"'{importer.assetPath}' is missing clips: {string.Join(", ", missing)}");
                return;
            }

            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
            Log($"animated model {Path.GetFileName(importer.assetPath)}: {string.Join(", ", clips.Select(c => c.name))}");
        }

        /// <summary>
        /// The model, an <c>Animation</c> holding every clip of the FBX, idle
        /// as the default, saved beside it as <c>&lt;name&gt;.prefab</c>.
        /// </summary>
        private static string MakePrefab(string fbxPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToList();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = Path.GetFileNameWithoutExtension(fbxPath);

            var animation = instance.GetComponent<Animation>() ?? instance.AddComponent<Animation>();
            foreach (var clip in clips)
            {
                clip.legacy = true;
                animation.AddClip(clip, clip.name);
            }
            animation.clip = clips.FirstOrDefault(c => c.name == "idle") ?? clips.FirstOrDefault();
            animation.playAutomatically = true;
            animation.cullingType = AnimationCullingType.AlwaysAnimate;

            var prefabPath = Path.ChangeExtension(fbxPath, ".prefab").Replace('\\', '/');
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);

            Log($"prefab {Path.GetFileName(prefabPath)} with {clips.Count} clip(s): {string.Join(", ", clips.Select(c => c.name))}");
            return prefabPath;
        }

        /// <summary>
        /// Refuses a mesh that arrives in more than one piece.
        ///
        /// <b>This is "los colores del trono", caught where it happened.</b>
        /// The throne was modelled with three materials - bone, stone, gold -
        /// and Unity turns one material slot into one submesh, so the mesh in
        /// the bundle had three. The building renderers this game draws with
        /// carry a single material each (its own log says <c>materials=1</c>),
        /// and Unity draws only <c>min(subMeshCount, materials.Length)</c>
        /// submeshes: submesh 0 was drawn and the other two were dropped
        /// without a word. What reached the player was the bone group alone -
        /// the throne in one flat colour, which read for a fortnight as a
        /// texture that never arrived.
        ///
        /// Every mesh the game ships has exactly one submesh - quality_chair,
        /// wood_chair, cat_stuffed_trophy, foxy_statue, limestone_sarcophagus,
        /// all 1 - and the colour of ours rides on the palette UVs, not on
        /// material slots. So one is not a preference here, it is the only
        /// count that draws, and a build is the right place to say so.
        /// </summary>
        private static void CheckOneSubmesh(string path)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null || mesh.subMeshCount <= 1) return;

            Fail($"'{Path.GetFileName(path)}' has {mesh.subMeshCount} submeshes; "
                 + "the building renderers carry one material, so only the first "
                 + "would be drawn. Collapse the material slots in the Blender "
                 + "build (see one_submesh) and re-export.");
        }

        // ------------------------------------------------------------------
        // Addressables
        // ------------------------------------------------------------------

        private static AddressableAssetSettings Settings()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null) return settings;

            settings = AddressableAssetSettings.Create(
                AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                true, true);

            AddressableAssetSettingsDefaultObject.Settings = settings;
            Log("created Addressables settings");
            return settings;
        }

        /// <summary>
        /// The one group, renamed for whichever mod is being built.
        ///
        /// <b>The name is the bundle's name, and two bundles may not share
        /// one.</b> Every mod was built out of a group called AldrichMeshes,
        /// so every mod shipped a bundle whose <em>internal</em> name was
        /// AldrichMeshes as well - different file names and different hashes,
        /// but the same name inside. Unity loads the first and refuses the
        /// rest outright:
        ///
        ///   The AssetBundle 'Mods\VampireCourt\...ldrichmeshes_assets_all
        ///   _85a8....bundle' can't be loaded because another AssetBundle with
        ///   the same files is already loaded.
        ///
        /// and then the catalog entry that depends on it fails with it -
        /// `TryAddMesh() failed for key: aldrich_count_throne` - so whichever
        /// mod loaded second had no meshes at all. It is still one group
        /// reused, renamed per pass rather than one group per mod, because a
        /// second group would be built into the first mod's catalog too.
        /// </summary>
        private static AddressableAssetGroup Group(AddressableAssetSettings settings, string mod)
        {
            var name = GroupName + mod;

            var group = settings.FindGroup(name);
            if (group == null)
            {
                // Whatever the last pass was called. Renaming keeps one group
                // for the project instead of leaving one behind per mod.
                group = settings.groups.FirstOrDefault(
                    g => g != null && g.Name.StartsWith(GroupName, StringComparison.Ordinal));
            }

            if (group == null)
            {
                group = settings.CreateGroup(name, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                Log("created group " + name);
            }
            else if (group.Name != name)
            {
                group.Name = name;
                Log("renamed group to " + name);
            }

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null)
            {
                // One bundle for the lot. The game loads a mod's catalog whole,
                // so splitting it per asset buys nothing and multiplies the
                // files that have to be copied into the mod.
                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                schema.IncludeInBuild = true;

                // <b>Renaming the group fixed the file name, not the name
                // inside.</b> A bundle carries a second name - the serialized
                // file Unity registers it under, the CAB-... - and that one is
                // not the group's. BuildScriptPackedMode builds it out of
                // CalculateGroupHash, which under the default mode
                // (GroupGuidProjectIdHash) hashes the group's <em>guid</em> and
                // the project id - and this script deliberately reuses one
                // group for every mod. Same guid, same project, same hash: both
                // mesh bundles shipped as
                // CAB-b0f4cc8c9b26b124371ac3c6239ac413, so Unity refused the
                // second exactly as it refused the second AldrichMeshes, with
                // the same ending - `TryAddMesh() failed for key:
                // aldrich_count_throne`, `Loaded ...\VampireCourt\... with 0
                // assets`, and a throne that is not there.
                //
                // GroupGuidProjectIdEntriesHash folds the entries' guids into
                // that hash, and the entries are the one thing that is already
                // different per mod - Import empties Assets/Models and Register
                // clears the group on every pass, so the throne's build never
                // sees the cat statue. The mode is documented as unsuitable for
                // caching on device, which costs nothing here: the cache is off
                // two lines up, and Publish replaces a mod's bundles whole.
                schema.InternalBundleIdMode =
                    BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdEntriesHash;
                schema.UseAssetBundleCache = false;
                schema.UseAssetBundleCrc = false;
                schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
                // Not kLocalBuildPath. That one is the "dynamic lookup" form of
                // StreamingAssets, and Addressables refuses outright to pair it
                // with a load path that is not also dynamic:
                //
                //   BuildPath for group 'AldrichMeshes' is set to the
                //   dynamic-lookup version of StreamingAssets, but LoadPath is
                //   not.
                //
                // The bundles are being copied into a mod folder by hand a few
                // lines further down, so where the build drops them first is of
                // no consequence - only that it is somewhere plain.
                schema.BuildPath.SetVariableByName(settings, BuildFolder(settings));

                // <b>The load path is the whole reason the first throne was
                // invisible.</b> The default is
                // {UnityEngine.AddressableAssets.Addressables.RuntimePath},
                // which at runtime is the *game's* StreamingAssets/aa folder -
                // so the catalog loaded, the entry registered, and the bundle
                // was then looked for somewhere it could never be. The log said
                // it in as many words:
                //
                //   Added instance: aldrich_count_throne
                //     ...Going Medieval_Data/StreamingAssets/aa\StandaloneWindows64\...bundle
                //   [ERR] TryAddMesh() failed for key: aldrich_count_throne
                //   Loaded ...\VampireCourt\Data\AddressableAssets with 0 assets
                //
                // The game publishes its own token for exactly this, and every
                // working mod on the workshop uses it:
                // {NSMedieval.Modding.ModdingUtils.LoadPath}, which the loader
                // points at the mod's own AddressableAssets folder before it
                // reads the catalog. The bundles therefore sit flat in that
                // folder with no platform subdirectory - see Publish.
                schema.LoadPath.SetVariableByName(settings, ModLoadPath(settings));
            }

            // Every other group as well, and the built-in one most of all.
            //
            // Addressables always emits a second bundle for Unity's built-in
            // shaders, and it takes its paths from whatever group carries the
            // default schema - not from ours. Left alone it keeps the
            // RuntimePath token, which points into the game's own install: the
            // catalog then names a bundle that cannot exist, and whether the
            // loader survives that is not something worth discovering in a test
            // round. The workshop mods that work ship theirs with the same mod
            // token as everything else, so ours does too.
            foreach (var other in settings.groups)
            {
                if (other == null || other == group) continue;

                var theirs = other.GetSchema<BundledAssetGroupSchema>();
                if (theirs == null) continue;

                theirs.BuildPath.SetVariableByName(settings, BuildFolder(settings));
                theirs.LoadPath.SetVariableByName(settings, ModLoadPath(settings));

                Log("pointed group '" + other.Name + "' at the mod folder too");
            }

            return group;
        }

        /// <summary>
        /// The profile variable holding the game's own mod load token, created
        /// on first use.
        ///
        /// Addressables will only take a load path by the <em>name</em> of a
        /// profile variable, so the token cannot simply be assigned - it has to
        /// exist in the profile first. The value is the literal string the
        /// game's loader substitutes; see the note in <see cref="Group"/>.
        /// </summary>
        private static string ModLoadPath(AddressableAssetSettings settings)
        {
            return Variable(settings, "AldrichModLoadPath",
                "{NSMedieval.Modding.ModdingUtils.LoadPath}");
        }

        /// <summary>Where the build drops the bundles before they are copied.</summary>
        private static string BuildFolder(AddressableAssetSettings settings)
        {
            return Variable(settings, "AldrichBuildPath", BuildStaging);
        }

        private const string BuildStaging = "ServerData/[BuildTarget]";

        /// <summary>
        /// One profile variable, created on first use and rewritten every run.
        ///
        /// Addressables takes a path only by the <em>name</em> of a profile
        /// variable, so a literal cannot simply be assigned - it has to exist
        /// in the profile first. Rewriting it each run matters: a profile left
        /// behind by an older version of this script would still hold the old
        /// value, and the failure that causes is silent at build time and
        /// invisible in game.
        /// </summary>
        private static string Variable(AddressableAssetSettings settings, string name, string value)
        {
            var profiles = settings.profileSettings;

            if (profiles.GetProfileDataByName(name) == null)
            {
                profiles.CreateValue(name, value);
                Log("created profile variable " + name);
            }

            profiles.SetValue(settings.activeProfileId, name, value);
            return name;
        }

        /// <summary>
        /// One entry per mesh, addressed by its file name and labelled Mesh.
        ///
        /// The address is what a building's JSON says in its baseMesh slot, so
        /// it is the file's stem and nothing else - aldrich_count_throne, not
        /// Assets/Models/aldrich_count_throne.fbx.
        /// </summary>
        private static void Register(AddressableAssetSettings settings,
                                     AddressableAssetGroup group,
                                     IEnumerable<string> paths)
        {
            foreach (var label in new[] { MeshLabel, PrefabLabel })
            {
                if (!settings.GetLabels().Contains(label)) settings.AddLabel(label);
            }

            // Everything the last pass registered goes first. One build per mod
            // means the group is reused, and an entry left over from the other
            // mod is that mod's mesh in this mod's catalog - the exact leak the
            // per-mod split exists to close.
            foreach (var stale in group.entries.ToList())
            {
                settings.RemoveAssetEntry(stale.guid, false);
            }

            foreach (var path in paths)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    Log("no guid for " + path);
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = Path.GetFileNameWithoutExtension(path);
                // Always Mesh, animated models too. The game's mod loader asks
                // the catalog for "Prefab" but its LoadAssets has no case for it,
                // so a Prefab entry is dropped without a word ("with 0 assets").
                // Mesh loads the whole GameObject into MeshRepository, skeleton
                // and Animation included, and the plugin takes it from there.
                var label = MeshLabel;
                entry.SetLabel(MeshLabel, false, false, false);
                entry.SetLabel(PrefabLabel, false, false, false);
                entry.SetLabel(label, true, true, false);

                Log($"addressable '{entry.address}' [{label}]");
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Builds the content, from an empty staging folder.
        ///
        /// <b>The wipe is not tidiness.</b> A bundle's filename carries a hash
        /// of its contents, so every rebuild that changes the mesh writes a
        /// <em>new</em> file into ServerData and leaves the old one sitting
        /// beside it. Publish clears the mod folder before it copies, but what
        /// it copies is the whole staging folder, so every old build goes
        /// across with the new one. Three throne bundles shipped that way on
        /// the 9th - one lying on its back, one a hundred times too big, one
        /// right - and which mesh the game ends up with then depends on file
        /// enumeration order rather than on anything in this file.
        /// </summary>
        private static AddressablesPlayerBuildResult Build()
        {
            var staging = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                BuildStaging.Replace("[BuildTarget]", "StandaloneWindows64"));

            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
                Log("cleared the staging folder " + staging);
            }

            AddressableAssetSettings.BuildPlayerContent(out var result);

            if (!string.IsNullOrEmpty(result.Error))
            {
                Fail("Addressables build failed: " + result.Error);
                return null;
            }

            Log($"built in {result.Duration:0.0}s -> {result.OutputPath}");
            return result;
        }

        /// <summary>
        /// Copies the catalog and the bundles into the mod, in the shape the
        /// catalog expects to find them in, and clears out what was there
        /// before.
        ///
        /// <b>The folder layout is not ours to choose, and it is flat.</b>
        /// Every bundle in the catalog is addressed as
        ///
        ///     {NSMedieval.Modding.ModdingUtils.LoadPath}\&lt;name&gt;.bundle
        ///
        /// and that token resolves to the mod's own AddressableAssets folder,
        /// so the bundles sit directly beside the catalog with no platform
        /// subdirectory. This was written the other way round first - a
        /// <c>StandaloneWindows64</c> folder, copied from the shape of the
        /// game's own catalog, which uses <c>Addressables.RuntimePath</c> and
        /// therefore does need one - and the result was a catalog that loaded,
        /// reported no error, registered the entry, and then failed
        /// <c>TryAddMesh</c> with the building invisible in play.
        ///
        /// The four workshop mods that ship meshes and work - Construction
        /// Variants Personaliz, Elements Variants Personaliz, Ordo Magnae
        /// Operae and Thousand Shelf - all do exactly this: LoadPath in the
        /// catalog, bundles flat, no platform folder anywhere. That is the
        /// check to run when a mesh stops appearing.
        ///
        /// <b>Only one .json goes across.</b> <c>AddressableModManager</c> takes
        /// the <em>first</em> file ending in .json as the content catalog, and
        /// the build drops a <c>settings.json</c> next to <c>catalog.json</c>.
        /// Copying both is a coin flip on file enumeration order, decided in
        /// the game rather than here; the first run of this script copied both
        /// and would have been exactly that bug.
        ///
        /// The stale files have to go for the same family of reason: a leftover
        /// bundle from a previous build is a mesh that stays after it has been
        /// replaced.
        /// </summary>
        private static int Publish(AddressablesPlayerBuildResult built, string into)
        {
            var from = Path.GetDirectoryName(built.OutputPath);
            if (string.IsNullOrEmpty(from) || !Directory.Exists(from))
            {
                Fail("cannot find the build output beside " + built.OutputPath);
                return 0;
            }

            if (Directory.Exists(into)) Directory.Delete(into, true);
            Directory.CreateDirectory(into);

            var copied = 0;

            var catalog = Path.Combine(from, "catalog.json");
            if (!File.Exists(catalog))
            {
                Fail("the build produced no catalog.json in " + from);
                return 0;
            }

            File.Copy(catalog, Path.Combine(into, "catalog.json"), true);
            copied++;
            Log("copied catalog.json");

            // Flat, next to the catalog. The load path token resolves to this
            // folder itself, so a platform subdirectory here would be a folder
            // the runtime never looks in - which is the shape the first build
            // shipped, and why the throne loaded as nothing.
            //
            // The bundles come from the group's own build folder, which is no
            // longer beside the catalog: the catalog is written under Library,
            // the bundles under ServerData. See BuildFolder for why they had to
            // be separated.
            var platform = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                BuildStaging.Replace("[BuildTarget]", "StandaloneWindows64"));

            if (!Directory.Exists(platform))
            {
                Fail("no bundles at " + platform);
                return copied;
            }

            foreach (var file in Directory.GetFiles(platform, "*.bundle"))
            {
                var name = Path.GetFileName(file);
                File.Copy(file, Path.Combine(into, name), true);
                copied++;

                Log("copied " + name);
            }

            return copied;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// The repository this project sits inside. Application.dataPath is
        /// .../tools/unity/AldrichBundles/Assets, so the root is four parents
        /// up from there.
        /// </summary>
        private static string RepoRoot()
        {
            var project = Directory.GetParent(Application.dataPath);
            return project?.Parent?.Parent?.Parent?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static string Home()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        private static void Log(string what)
        {
            Debug.Log("[aldrich] " + what);
            Console.WriteLine("[aldrich] " + what);
        }

        private static void Fail(string why)
        {
            Debug.LogError("[aldrich] " + why);
            Console.Error.WriteLine("[aldrich] FAILED " + why);
            Done(1);
        }

        private static void Done(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
