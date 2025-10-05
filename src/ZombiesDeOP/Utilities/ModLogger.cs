using UnityEngine;

namespace ZombiesDeOP.Utilities
{
    public static class ModLogger
    {
        private const string PREFIX = "[ZombiesDeOP] ";

        public static void Log(string message)
        {
            Debug.Log(PREFIX + message);
        }

        public static void Warning(string message)
        {
            Debug.LogWarning(PREFIX + message);
        }

        public static void Error(string message)
        {
            Debug.LogError(PREFIX + message);
        }

        public static void Debug(string message)
        {
            if (ModSettings.DebugMode)
            {
                UnityEngine.Debug.Log(PREFIX + "[DEBUG] " + message);
            }
        }
    }
}
