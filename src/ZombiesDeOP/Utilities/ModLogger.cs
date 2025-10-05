using System;
using UnityEngine;

namespace ZombiesDeOP.Utilities
{
    public static class ModLogger
    {
        public static void LogDebug(string message) => Log.Out($"[ZombiesDeOP:DEBUG] {message}");
        public static void Info(string message) => Log.Out($"[ZombiesDeOP] {message}");
        public static void Warn(string message) => Log.Warning($"[ZombiesDeOP:WARN] {message}");
        public static void Error(string message, Exception ex = null)
        {
            Log.Error($"[ZombiesDeOP:ERROR] {message}" + (ex != null ? $"\n{ex}" : ""));
        }
    }
}
