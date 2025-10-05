using System.IO;

namespace ZombiesDeOP.Utilities
{
    public static class ModContext
    {
        public static string ModFolderPath { get; private set; } = string.Empty;

        public static void Initialize(Mod mod)
        {
            ModFolderPath = mod?.Path ?? string.Empty;
        }

        public static string ResolveConfigPath(string relative)
        {
            if (string.IsNullOrEmpty(ModFolderPath))
            {
                return relative;
            }

            return Path.Combine(ModFolderPath, relative);
        }
    }
}
