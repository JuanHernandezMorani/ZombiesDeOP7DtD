using System;
using System.Collections.Generic;
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
            "AIUpdate",
            "UpdateTasks",
            "Update",
            "UpdateTarget"
        };

        private static bool _targetLogged;

        public static IEnumerable<MethodBase> TargetMethods()
        {
            var enemyType = AccessTools.TypeByName("EntityEnemy");
            var aliveType = AccessTools.TypeByName("EntityAlive");
            int matches = 0;

            foreach (var type in new[] { enemyType, aliveType })
            {
                if (type == null)
                {
                    continue;
                }

                foreach (string methodName in CandidateMethods)
                {
                    var method = AccessTools.Method(type, methodName, Type.EmptyTypes);
                    if (method == null)
                    {
                        continue;
                    }

                    if (method.ReturnType != typeof(void) || method.GetParameters().Length != 0)
                    {
                        continue;
                    }

                    matches++;
                    ModLogger.Info($"🔧 [ZombiesDeOP] Harmony target detectado: {type.FullName}.{methodName}()");
                    yield return method;
                }
            }

            if (matches == 0 && !_targetLogged)
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
    }
}
