using System;

namespace ZombieDetectionHUD
{
    /// <summary>
    /// Centralized logging helpers that wrap the vanilla game logger with a consistent prefix.
    /// </summary>
    public static class ModLogger
    {
        private const string Prefix = "[ZombieDetectionHUD]";

        /// <summary>
        /// Writes an informational message to the console log.
        /// </summary>
        public static void Info(string message)
        {
            Log.Out($"{Prefix} {message}");
        }

        /// <summary>
        /// Writes a warning message to the console log.
        /// </summary>
        public static void Warning(string message)
        {
            Log.Warning($"{Prefix} {message}");
        }

        /// <summary>
        /// Writes an error message to the console log and optional exception details.
        /// </summary>
        public static void Error(string message, Exception exception = null)
        {
            if (exception == null)
            {
                Log.Error($"{Prefix} {message}");
                return;
            }

            Log.Error($"{Prefix} {message}\n{exception}");
        }

        /// <summary>
        /// Writes a verbose debug message when debug mode is enabled in the configuration.
        /// </summary>
        public static void Debug(string message)
        {
            if (!ModConfig.DebugMode)
            {
                return;
            }

            Log.Out($"{Prefix} [DEBUG] {message}");
        }
    }
}
