using System;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Cada mod lleva el mismo nucleo, y los parches comunes no deben aplicarse
    /// una vez por mod instalado. El primero que carga se queda cada uno; el
    /// AppDomain es lo unico que ven todas las DLL a la vez.
    /// </summary>
    internal static class Shared
    {
        // ponytail: gana la primera DLL en cargar aunque traiga una copia mas
        // vieja del parche; si eso llega a importar, comparar versiones aqui.
        internal static bool Claim(string what)
        {
            var key = "aldrich.shared." + what;
            var owner = AppDomain.CurrentDomain.GetData(key) as string;
            if (owner != null)
            {
                GMPlugin.Log?.LogInfo($"  {what}: ya lo aplica {owner}");
                return false;
            }

            AppDomain.CurrentDomain.SetData(key, GMPlugin.PluginGuid);
            return true;
        }
    }
}
