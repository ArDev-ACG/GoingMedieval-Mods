using System;
using NSEipix.Base;
using NSMedieval.Sound;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// El ruido del Corredor: el mordisco y el coletazo.
    ///
    /// <b>De donde salen los sonidos, y por que no de internet.</b> Se pidio
    /// buscar ficheros o fabricarlos. No hace falta ninguna de las dos cosas, y
    /// las dos son peores: este juego no carga audio de un mod - el sonido va
    /// en un banco de FMOD, <c>Master Bank.bank</c>, y un banco no se amplia
    /// desde fuera -, asi que un <c>.wav</c> nuestro en una carpeta no sonaria
    /// nunca. Y un sonido sacado de una pelicula es de alguien.
    ///
    /// Lo que si hay es un vocabulario. En <c>Master Bank.strings.bank</c>
    /// estan las claves que el juego usa, y entre ellas <c>BearAttack</c>,
    /// <c>WolfAttack</c>, <c>SoftBody</c> y
    /// <c>CriticalStrike</c> - las mismas que un JSON de vanilla nombra en
    /// claro, como <c>PawFootsteps</c> en su <c>AnimalBase.json</c> -. El ruido
    /// del xeno es **dos de esas a la vez**: el gruñido grave del oso con el
    /// impacto en carne encima, que juntos no se parecen a ninguno de los dos
    /// por separado. Las dos claves estan en la config, asi que cambiarle la
    /// voz al bicho no es recompilar.
    ///
    /// Si algun dia hace falta un sonido que no exista en el banco, la unica
    /// via honesta es un <c>AudioSource</c> propio del plugin con un clip
    /// sintetizado - no un banco de FMOD -, y eso es otro trabajo.
    /// </summary>
    internal static class XenoNoise
    {
        private static bool complained;

        /// <summary>El mordisco: grave y corto.</summary>
        internal static void Bite(Vector3 at)
        {
            Play(GMPlugin.XenoBiteSound?.Value ?? "BearAttack", at);
            Play("SoftBody", at);
        }

        /// <summary>El coletazo: el latigazo seco y el golpe en carne.</summary>
        internal static void Lash(Vector3 at)
        {
            Play(GMPlugin.XenoLashSound?.Value ?? "CriticalStrike", at);
            Play("SoftBody", at);
        }

        private static void Play(string key, Vector3 at)
        {
            if (string.IsNullOrEmpty(key)) return;

            try
            {
                // De instancia y no estatico, y fuera de partida no hay
                // ninguna: preguntar sin mirar es la excepcion de siempre.
                if (!MonoSingleton<AudioManager>.IsInstantiated()) return;

                MonoSingleton<AudioManager>.Instance.PlaySoundAtPosition(key, at);
            }
            catch (Exception e)
            {
                if (complained) return;

                complained = true;
                GMPlugin.Log?.LogWarning(
                    $"[xeno] el sonido '{key}' no sono y no se vuelve a decir: {e.Message}");
            }
        }
    }
}
