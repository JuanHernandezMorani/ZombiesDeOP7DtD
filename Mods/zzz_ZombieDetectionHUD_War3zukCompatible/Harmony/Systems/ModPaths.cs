using System;
using System.IO;
using System.Reflection;

namespace ZombieDetectionHUD
{
    /// <summary>
    /// Helper responsible for discovering key directories inside the mod folder.
    /// </summary>
    public static class ModPaths
    {
        private static string modRoot;
        private static string configDirectory;

        /// <summary>
        /// Full path to the mod root directory on disk.
        /// </summary>
        public static string ModRoot
        {
            get
            {
                if (!string.IsNullOrEmpty(modRoot))
                {
                    return modRoot;
                }

                try
                {
                    var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                    if (!string.IsNullOrEmpty(assemblyLocation))
                    {
                        var directory = Path.GetDirectoryName(assemblyLocation);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            modRoot = Directory.GetParent(directory)?.FullName ?? directory;
                            return modRoot;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Error("No se pudo determinar la ruta del ensamblado.", ex);
                }

                var fallback = Path.Combine(Directory.GetCurrentDirectory(), "Mods", "zzz_ZombieDetectionHUD_War3zukCompatible");
                ModLogger.Warning($"Usando ruta alternativa del mod: {fallback}");
                modRoot = fallback;
                return modRoot;
            }
        }

        /// <summary>
        /// Directory containing configuration assets.
        /// </summary>
        public static string ConfigDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(configDirectory))
                {
                    return configDirectory;
                }

                configDirectory = Path.Combine(ModRoot, "Config");
                return configDirectory;
            }
        }
    }
}
