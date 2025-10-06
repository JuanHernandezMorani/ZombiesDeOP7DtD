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
            "Update",
            "OnUpdate"
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
            if (__instance is not EntityEnemy enemy)
            {
                return;
            }

            DetectionSystemRuntime.NotifyHarmonyTick(enemy);
        }

        private static IEnumerable<MethodInfo> GetCandidateMethods(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            return type
                .GetMethods(flags)
                .Where(m => CandidateMethods.Contains(m.Name))
                .Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 0);
        }
    }
}
