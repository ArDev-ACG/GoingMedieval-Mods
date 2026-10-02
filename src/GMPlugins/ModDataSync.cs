using System;
using System.Collections.Generic;
using System.IO;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Pone los datos del mod donde el juego los busca.
    ///
    /// <b>Por que.</b> Un mod nuestro son dos cosas en dos sitios: la DLL, que
    /// BepInEx carga de <c>Going Medieval\BepInEx\plugins</c>, y los JSON,
    /// sprites y bundles, que el juego lee de
    /// <c>Documentos\Foxy Voxel\Going Medieval\Mods</c>. Vortex (su extension
    /// de Going Medieval) instala todo el archivo en <c>BepInEx\plugins</c> y no
    /// conoce la otra carpeta. Asi que el paquete lleva los datos al lado de la
    /// DLL, en <c>plugins\Aldrich&lt;Mod&gt;\&lt;Mod&gt;\</c>, y aqui se copian al
    /// arrancar, antes de que el juego mire sus mods.
    ///
    /// Es un espejo: lo que falta o cambio se copia, y lo que sobra en el
    /// destino se borra - los bundles llevan el hash en el nombre y una version
    /// vieja dejaria dos catalogos. Solo dentro de la carpeta de este mod. Sin
    /// carpeta de origen (una instalacion de desarrollo, con los datos ya en
    /// Documentos) no hace nada.
    /// </summary>
    internal static class ModDataSync
    {
        internal static void Run(string pluginDir, string modFolder)
        {
            try
            {
                if (string.IsNullOrEmpty(pluginDir) || string.IsNullOrEmpty(modFolder)) return;

                var source = Path.Combine(pluginDir, modFolder);
                if (!File.Exists(Path.Combine(source, "ModInfo.json"))) return;

                var target = Path.Combine(ModsRoot(), modFolder);
                int copied = 0, removed = 0;

                var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var from in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                {
                    var rel = from.Substring(source.Length).TrimStart('\\', '/');
                    wanted.Add(rel);

                    var to = Path.Combine(target, rel);
                    if (Same(from, to)) continue;

                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    File.Copy(from, to, true);
                    File.SetLastWriteTimeUtc(to, File.GetLastWriteTimeUtc(from));
                    copied++;
                }

                if (Directory.Exists(target))
                {
                    foreach (var old in Directory.GetFiles(target, "*", SearchOption.AllDirectories))
                    {
                        var rel = old.Substring(target.Length).TrimStart('\\', '/');
                        if (wanted.Contains(rel)) continue;
                        File.Delete(old);
                        removed++;
                    }
                }

                GMPlugin.Log?.LogInfo(copied + removed == 0
                    ? $"[data] {modFolder}: los datos del mod ya estaban al dia"
                    : $"[data] {modFolder}: {copied} fichero(s) copiado(s) y {removed} borrado(s) en la carpeta de mods");
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[data] {modFolder}: no se pudieron copiar los datos del mod: {e}");
            }
        }

        /// <summary>La misma cuenta que ModdingUtils.GetRootDirectoryPath.</summary>
        private static string ModsRoot()
        {
            var docs = NSMedieval.Modding.ModdingUtils.MyDocumentsPath;
            if (string.IsNullOrEmpty(docs)) docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "Foxy Voxel/Going Medieval/Mods");
        }

        private static bool Same(string a, string b)
        {
            if (!File.Exists(b)) return false;
            var x = new FileInfo(a);
            var y = new FileInfo(b);
            return x.Length == y.Length && x.LastWriteTimeUtc == y.LastWriteTimeUtc;
        }
    }
}
