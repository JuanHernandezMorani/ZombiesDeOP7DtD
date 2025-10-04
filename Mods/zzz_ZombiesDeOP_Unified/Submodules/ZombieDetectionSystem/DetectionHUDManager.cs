using System.Linq;
using UnityEngine;
using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.ZombieDetectionSystem
{
    public sealed class DetectionHUDManager : MonoBehaviour, IModule
    {
        private DetectionEngine detectionEngine;
        private GUIStyle detectionStyle;
        private GUIStyle infoStyle;
        private Texture2D detectedIcon;
        private Texture2D hiddenIcon;
        private Color currentColor = Color.green;
        private string currentStatus = "OCULTO";
        private float lastHudUpdate;
        private int zombieCount;

        public bool IsEnabled => DetectionConfig.EnableDetectionHUD;
        public string ModuleName => "HUD de Detección de Zombies";

        public void InitializeModule()
        {
            enabled = IsEnabled;
            if (detectionEngine != null)
            {
                detectionEngine.AttachHud(this);
                detectionEngine.DetectionStateChanged += OnDetectionStateChanged;
            }
        }

        public void InitializeModule(DetectionEngine engine)
        {
            detectionEngine = engine;
            InitializeModule();
        }

        public void Shutdown()
        {
            Detach();
            enabled = false;
            DestroyTextures();
        }

        public void Detach()
        {
            if (detectionEngine != null)
            {
                detectionEngine.DetectionStateChanged -= OnDetectionStateChanged;
                detectionEngine = null;
            }
        }

        public void UpdateZombieCount(int count)
        {
            zombieCount = count;
        }

        private void Start()
        {
            CreateStyles();
            CreateTextures();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void OnDetectionStateChanged(bool detected)
        {
            currentStatus = detected ? "DETECTADO" : "OCULTO";
            currentColor = detected ? new Color(0.85f, 0.1f, 0.1f) : new Color(0.1f, 0.7f, 0.2f);
        }

        private void OnGUI()
        {
            if (!IsEnabled || detectionEngine == null)
            {
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.IsPaused())
            {
                return;
            }

            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (Time.time - lastHudUpdate < DetectionConfig.HudUpdateInterval)
            {
                return;
            }

            lastHudUpdate = Time.time;
            DrawHud();
        }

        private void DrawHud()
        {
            if (detectionStyle == null || infoStyle == null)
            {
                CreateStyles();
            }

            var scale = Mathf.Max(0.75f, Screen.height / 1080f);
            detectionStyle.fontSize = Mathf.RoundToInt(DetectionConfig.FontSize * scale);
            detectionStyle.normal.textColor = currentColor;
            infoStyle.fontSize = Mathf.RoundToInt(12 * scale);

            var offset = DetectionConfig.HudOffset;
            float width = 210f * scale;
            float height = 68f * scale;
            float x = Screen.width - offset.x - width;
            float y = offset.y;

            var backgroundRect = new Rect(x, y, width, height);
            DrawBackground(backgroundRect);

            float iconSize = Mathf.Max(36f * scale, detectionStyle.fontSize * 1.25f);
            var iconRect = new Rect(x + 12f * scale, y + (height - iconSize) / 2f, iconSize, iconSize);
            var textRect = new Rect(iconRect.xMax + 12f * scale, y + 10f * scale, width - iconRect.width - 36f * scale, detectionStyle.fontSize + 8f * scale);
            var infoRect = new Rect(textRect.x, textRect.yMax - 4f * scale, textRect.width, infoStyle.fontSize + 4f * scale);

            var icon = currentStatus == "DETECTADO" ? detectedIcon : hiddenIcon;
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            }

            DrawLabel(textRect, currentStatus, detectionStyle, DetectionConfig.UseOutline);
            GUI.Label(infoRect, $"Zombies cercanos: {zombieCount}", infoStyle);
        }

        private void DrawBackground(Rect rect)
        {
            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void DrawLabel(Rect rect, string text, GUIStyle style, bool outline)
        {
            if (!outline)
            {
                GUI.Label(rect, text, style);
                return;
            }

            var originalColor = style.normal.textColor;
            var outlineColor = Color.black;
            const float offset = 1.5f;

            style.normal.textColor = outlineColor;
            GUI.Label(new Rect(rect.x - offset, rect.y, rect.width, rect.height), text, style);
            GUI.Label(new Rect(rect.x + offset, rect.y, rect.width, rect.height), text, style);
            GUI.Label(new Rect(rect.x, rect.y - offset, rect.width, rect.height), text, style);
            GUI.Label(new Rect(rect.x, rect.y + offset, rect.width, rect.height), text, style);

            style.normal.textColor = originalColor;
            GUI.Label(rect, text, style);
        }

        private void CreateStyles()
        {
            detectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = DetectionConfig.FontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
        }

        private void CreateTextures()
        {
            detectedIcon = CreateSolidTexture(new Color(0.85f, 0.1f, 0.1f));
            hiddenIcon = CreateSolidTexture(new Color(0.1f, 0.7f, 0.2f));
        }

        private void DestroyTextures()
        {
            if (detectedIcon != null)
            {
                Destroy(detectedIcon);
                detectedIcon = null;
            }

            if (hiddenIcon != null)
            {
                Destroy(hiddenIcon);
                hiddenIcon = null;
            }
        }

        private Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(32, 32);
            var pixels = Enumerable.Repeat(color, 32 * 32).ToArray();
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
