using System.Collections.Generic;
using UnityEngine;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP.Systems
{
    public static class HUDManager
    {
        private sealed class HudRuntime : MonoBehaviour
        {
            private void Update() => OnGameUpdate();
            private void OnGUI() => OnGameGUI();
        }

        private static readonly Queue<string> Messages = new();
        private static bool initialized;
        private static float displayTimer;
        private static GameObject runtimeObject;
        private const float MESSAGE_DURATION = 3.5f;

        // Estilo IMGUI cacheado para evitar GC y variaciones por frame
        private static GUIStyle _labelStyle;
        private static Rect _labelRect = new Rect(50f, 120f, 600f, 28f);

        public static void Initialize()
        {
            if (initialized) return;

            if (!ModSettings.EnableHUD)
            {
                ModLogger.Info("ℹ️ [ZombiesDeOP] HUD deshabilitado por configuración");
                return;
            }

            runtimeObject = new GameObject("ZombiesDeOP_HUDRuntime");
            Object.DontDestroyOnLoad(runtimeObject);
            runtimeObject.hideFlags = HideFlags.HideAndDontSave;
            runtimeObject.AddComponent<HudRuntime>();

            // Estilo por defecto (blanco, semi-negrita, tamaño legible)
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = UnityEngine.FontStyle.Bold,
                alignment = UnityEngine.TextAnchor.UpperLeft,
                wordWrap = false
            };
            _labelStyle.normal.textColor = Color.white;

            initialized = true;
            displayTimer = 0f;
            ModLogger.Info("🖥️ [ZombiesDeOP] HUD inicializado");
        }

        public static void Shutdown()
        {
            if (!initialized) return;

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
            if (!initialized || enemy == null) return;

            string status = detected ? "DETECTADO" : "OCULTO";
            string message = $"[{status}] {enemy.EntityName} - {distance:F1}m";
            Messages.Enqueue(message);
            ModLogger.LogDebug($"HUD -> {message}");
        }

        private static void OnGameUpdate()
        {
            if (!initialized) return;
            if (Messages.Count == 0) return;

            displayTimer += Time.deltaTime;
            if (displayTimer >= MESSAGE_DURATION)
            {
                Messages.Dequeue();
                displayTimer = 0f;
            }
        }

        private static void OnGameGUI()
        {
            if (!initialized) return;
            if (Messages.Count == 0) return;

            string message = Messages.Peek();

            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(_labelRect.x - 6f, _labelRect.y - 4f, _labelRect.width + 12f, _labelRect.height + 8f), Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUI.Label(_labelRect, message, _labelStyle);
        }
    }
}
