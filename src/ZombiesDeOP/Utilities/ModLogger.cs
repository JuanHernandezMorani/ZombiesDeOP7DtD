using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ZombiesDeOP.Utilities
{
    public static class ModLogger
    {
        private static readonly Action<string> _out;
        private static readonly Action<string> _warn;
        private static readonly Action<string> _error;

        // Resolver en runtime: Log.Out / Log.Warning / Log.Error si existen; si no, usar Debug.*
        static ModLogger()
        {
            try
            {
                var logType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .SelectMany(a => {
                        try { return a.GetTypes(); }
                        catch { return Type.EmptyTypes; }
                    })
                    .FirstOrDefault(t => t.Name == "Log"); // 7DTD suele exponer 'Log' en Assembly-CSharp

                if (logType != null)
                {
                    _out = CreateDelegateOrNull(logType, "Out") ?? Debug.Log;
                    _warn = CreateDelegateOrNull(logType, "Warning") ?? Debug.LogWarning;
                    _error = CreateDelegateOrNull(logType, "Error") ?? Debug.LogError;
                    return;
                }
            }
            catch { /* ignorar y caer a Debug */ }

            // Fallback si no hay tipo Log
            _out = Debug.Log;
            _warn = Debug.LogWarning;
            _error = Debug.LogError;
        }

        private static Action<string> CreateDelegateOrNull(Type type, string method)
        {
            try
            {
                var mi = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                return mi != null ? (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), mi) : null;
            }
            catch { return null; }
        }

        public static void LogDebug(string message) => _out?.Invoke($"[ZombiesDeOP:DEBUG] {message}");
        public static void Info(string message)     => _out?.Invoke($"[ZombiesDeOP] {message}");
        public static void Warn(string message)     => _warn?.Invoke($"[ZombiesDeOP:WARN] {message}");
        public static void Error(string message, Exception ex = null)
        {
            var msg = $"[ZombiesDeOP:ERROR] {message}" + (ex != null ? $"\n{ex}" : "");
            _error?.Invoke(msg);
        }
    }
}
