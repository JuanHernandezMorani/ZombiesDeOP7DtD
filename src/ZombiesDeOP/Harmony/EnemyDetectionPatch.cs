using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ZombiesDeOP.Systems;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP.Harmony
{
    [HarmonyPatch]
    public static class EnemyDetectionPatch
    {
        private static readonly string[] CandidateMethods =
        {
            "OnUpdateLive",
            "OnUpdate",
            "Update",
            "UpdateAI",
            "AIUpdate",
            "UpdateAITasks",
            "UpdateTasks",
            "UpdateTarget",
            "Tick",
            "TickAI"
        };

        private static bool _targetLogged;

        public static IEnumerable<MethodBase> TargetMethods()
        {
            var enemyType = AccessTools.TypeByName("EntityEnemy") ?? typeof(EntityEnemy);
            var aliveType = AccessTools.TypeByName("EntityAlive") ?? typeof(EntityAlive);
            var discovered = new HashSet<MethodBase>();

            foreach (var type in new[] { enemyType, aliveType })
            {
                if (type == null)
                {
                    continue;
                }

                foreach (var method in GetCandidateMethods(type))
                {
                    if (discovered.Add(method))
                    {
                        ModLogger.Info($"🔧 [ZombiesDeOP] Harmony target detectado: {method.DeclaringType?.FullName}.{method.Name}()");
                        yield return method;
                    }
                }
            }

            if (discovered.Count == 0 && !_targetLogged)
            {
                _targetLogged = true;
                ModLogger.Warn("⚠️ [ZombiesDeOP] EnemyDetectionPatch: no suitable targets found; el sistema usará modo polling.");
            }
        }

        public static void Postfix(object __instance)
        {
            if (__instance is not EntityAlive entity)
            {
                return;
            }

            if (!BehaviorManager.IsHostileEntity(entity))
            {
                return;
            }

            if (ModSettings.DebugMode)
            {
                try
                {
                    ModLogger.LogDebug($"EnemyDetectionPatch.Postfix -> {entity.EntityName} ({entity.entityId})");
                }
                catch
                {
                    // Ignorar cualquier excepción de propiedades durante depuración
                }
            }

            DetectionSystemRuntime.NotifyHarmonyTick(entity);
        }

        private static IEnumerable<MethodInfo> GetCandidateMethods(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            return type
                .GetMethods(flags)
                .Where(IsSupportedMethod);
        }

        private static bool IsSupportedMethod(MethodInfo method)
        {
            if (method == null)
            {
                return false;
            }

            if (method.ReturnType != typeof(void) || method.GetParameters().Length != 0)
            {
                return false;
            }

            var name = method.Name;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            foreach (var candidate in CandidateMethods)
            {
                if (string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith(candidate, StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(candidate, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
