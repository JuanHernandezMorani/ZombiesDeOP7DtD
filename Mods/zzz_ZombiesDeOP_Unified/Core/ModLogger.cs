using UnityEngine;

namespace ZombiesDeOPUnified.Core
{
    public static class ModLogger
    {
        private const string Prefix = "[ZombiesDeOP Unified] ";

        public static void Log(string message)
        {
            Debug.Log(Prefix + message);
        }

        public static void Warning(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        public static void Error(string message)
        {
            Debug.LogError(Prefix + message);
        }

        public static void Debug(string message)
        {
            if (!ModConfig.Current.Detection.DebugMode)
            {
                return;
            }

            UnityEngine.Debug.Log(Prefix + "[DEBUG] " + message);
        }
    }
}
