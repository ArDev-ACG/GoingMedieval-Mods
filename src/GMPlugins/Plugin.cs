using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// El nucleo que comparten las DLL de todos los mods. Cada mod compila
    /// esto, sus propios ficheros y un <c>Entry.cs</c> con su
    /// <c>[BepInPlugin]</c> y su <c>Load</c> (ver <c>src/Mods/</c>): asi cada
    /// mod funciona solo, con BepInEx y nada mas.
    ///
    /// Lo comun que parchea el juego - texturas y sprites de mods, nombres,
    /// fichas de muebles, guardas y la consola - lo aplica solo la primera DLL
    /// que carga (<see cref="Shared.Claim"/>): dos copias del mismo parche
    /// harian el trabajo dos veces.
    /// </summary>
    public partial class GMPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        /// <summary>
        /// Runs something every so often, for as long as the game is up.
        ///
        /// The alternative was a Harmony patch on whatever the game ticks, and
        /// there is no honest candidate: the per-creature ticks run on the
        /// pathfinding threads, and the per-quarter-hour ones stop with the
        /// game clock, which a horde standing still does not.
        ///
        /// <b>It used to be a coroutine on this component, and that is the bug
        /// this signature carries the scar of.</b> Three sweeps registered this
        /// way wrote nothing at all across a session that gave all three
        /// something to say. The work now goes to <see cref="Heartbeat"/>, an
        /// object of ours with an Update of its own, and every job announces
        /// its first tick so "found nothing" and "never ran" stop reading the
        /// same in a log.
        /// </summary>
        internal static void Every(string name, float seconds, Action action)
        {
            Heartbeat.Live()?.Repeat(name, seconds, action);
        }

        /// <summary>
        /// Runs something a moment from now, on the main thread.
        ///
        /// Needed because a goal that has just failed cannot give itself the
        /// same order again: it is still being torn down, and the agent is mid
        /// tick.
        /// </summary>
        internal static void RunAfter(float seconds, Action action)
        {
            if (action == null) return;

            var beat = Heartbeat.Live();
            if (beat == null)
            {
                action();
                return;
            }

            beat.After(seconds, action);
        }

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{PluginName} {PluginVersion} loading");

            ModDataSync.Run(System.IO.Path.GetDirectoryName(Info.Location), ModFolder);

            var harmony = new Harmony(PluginGuid);
            PatchShared(harmony);
            Load(harmony);

            Log.LogInfo($"{PluginName} patched {harmony.GetPatchedMethods().Count()} method(s)");
        }

        /// <summary>Lo de cada mod: su config, sus parches y sus barridos. Lo pone Entry.cs.</summary>
        partial void Load(Harmony harmony);

        private static void PatchShared(Harmony harmony)
        {
            if (Shared.Claim(nameof(ModTextures))) PatchOne(harmony, typeof(ModTextures));
            if (Shared.Claim(nameof(ModSpriteAssets))) PatchOne(harmony, typeof(ModSpriteAssets));
            if (Shared.Claim(nameof(ModNpcNames))) PatchOne(harmony, typeof(ModNpcNames));
            if (Shared.Claim(nameof(ModBuildingInfo))) PatchOne(harmony, typeof(ModBuildingInfo));
            if (Shared.Claim(nameof(DevCommands))) PatchOne(harmony, typeof(DevCommands));
            if (Shared.Claim(nameof(QualityBaseGuard))) PatchOne(harmony, typeof(QualityBaseGuard));
            if (Shared.Claim(nameof(StorageGroupNullGuard))) PatchOne(harmony, typeof(StorageGroupNullGuard));
            if (Shared.Claim(nameof(AgeEffectorsGuard))) PatchOne(harmony, typeof(AgeEffectorsGuard));
            if (Shared.Claim(nameof(FlammabilityGuard))) PatchOne(harmony, typeof(FlammabilityGuard));
        }

        internal static void PatchOne(Harmony harmony, Type patchClass)
        {
            try
            {
                harmony.CreateClassProcessor(patchClass).Patch();
                Log.LogInfo($"  patched {patchClass.Name}");
            }
            catch (Exception e)
            {
                Log.LogError($"  FAILED {patchClass.Name}: {e.Message}{(e.InnerException != null ? " <- " + e.InnerException : "")}");
            }
        }
    }
}
