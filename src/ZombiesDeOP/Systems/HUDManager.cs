using System.Collections.Generic;
using UnityEngine;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP.Systems
{
    public static class HUDManager
    {
        private sealed class HudRuntime : MonoBehaviour
        {
            private void Update()
            {
                OnGameUpdate();
            }
        }

        private static readonly Queue<string> Messages = new();
        private static bool initialized;
        private static float displayTimer;
        private static GameObject runtimeObject;
        private const float MESSAGE_DURATION = 3.5f;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            if (!ModSettings.EnableHUD)
            {
                ModLogger.Log("ℹ️ [ZombiesDeOP] HUD deshabilitado por configuración");
                return;
            }

            runtimeObject = new GameObject("ZombiesDeOP_HUDRuntime");
            Object.DontDestroyOnLoad(runtimeObject);
            runtimeObject.hideFlags = HideFlags.HideAndDontSave;
            runtimeObject.AddComponent<HudRuntime>();

            initialized = true;
            displayTimer = 0f;
            ModLogger.Log("🖥️ [ZombiesDeOP] HUD inicializado");
        }

        public static void Shutdown()
        {
            if (!initialized)
            {
                return;
            }

            Messages.Clear();
            if (runtimeObject != null)
            {
                Object.Destroy(runtimeObject);
                runtimeObject = null;
            }

            initialized = false;
        }

        public static void ReportDetection(EntityEnemy enemy, bool detected, float distance)
        {
            if (!initialized || enemy == null)
            {
                return;
            }

            string status = detected ? "DETECTADO" : "OCULTO";
            string message = $"[{status}] {enemy.EntityName} - {distance:F1}m";
            Messages.Enqueue(message);
            ModLogger.Debug($"HUD -> {message}");
        }

        private static void OnGameUpdate()
        {
            if (!initialized)
            {
                return;
            }

            if (Messages.Count == 0)
            {
                return;
            }

            displayTimer += Time.deltaTime;
            if (displayTimer >= MESSAGE_DURATION)
            {
                Messages.Dequeue();
                displayTimer = 0f;
                if (Messages.Count == 0)
                {
                    return;
                }
            }

            string message = Messages.Peek();
            GameManager.Instance?.ChatMessageServer(null, EChatType.Whisper, -1, message, null, EChatSenderType.System);
        }
    }
}
