using System;
using System.IO;
using UnityEngine;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP.Systems
{
    public class UIOverlayComponent : MonoBehaviour
    {
        private Texture2D hiddenTexture;
        private Texture2D seenTexture;
        private Texture2D currentTexture;
        private string currentState = "none";
        private bool isVisible;

        private const string HiddenImageName = "hidden.png";
        private const string SeenImageName = "seen.png";

        private void Awake()
        {
            ModLogger.Info("🧩 [ZombiesDeOP] UIOverlayComponent adjunto");
        }

        private void Start()
        {
            ModLogger.Info("🧩 [ZombiesDeOP] Inicializando UIOverlayComponent");
            LoadTextures();
            HideTexture();
        }

        private void LoadTextures()
        {
            hiddenTexture = LoadTexture(HiddenImageName);
            seenTexture = LoadTexture(SeenImageName);

            bool hiddenLoaded = hiddenTexture != null;
            bool seenLoaded = seenTexture != null;
            ModLogger.Info($"🧩 [ZombiesDeOP] Texturas cargadas -> Hidden: {hiddenLoaded}, Seen: {seenLoaded}");
        }

        private Texture2D LoadTexture(string imageName)
        {
            try
            {
                // Candidatos relativos al directorio del mod (resueltos por ModContext)
                string[] candidates =
                {
                    Path.Combine("Resources", imageName),
                    Path.Combine("imgs", imageName),
                    imageName
                };

                string texturePath = string.Empty;
                foreach (string candidate in candidates)
                {
                    string resolved = ModContext.ResolveConfigPath(candidate);
                    if (File.Exists(resolved))
                    {
                        texturePath = resolved;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(texturePath))
                {
                    ModLogger.Warn($"⚠️ [ZombiesDeOP] Textura no encontrada para {imageName}");
                    return null;
                }

                byte[] data = File.ReadAllBytes(texturePath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                // Prefijo explícito para asegurar resolución del símbolo con el módulo de ImageConversion
                if (!UnityEngine.ImageConversion.LoadImage(texture, data))
                {
                    ModLogger.Error($"❌ [ZombiesDeOP] Falló la carga de imagen: {texturePath}");
                    Destroy(texture);
                    return null;
                }

                ModLogger.LogDebug($"🧩 [ZombiesDeOP] Textura cargada: {texturePath}");
                return texture;
            }
            catch (Exception e)
            {
                ModLogger.Error($"❌ [ZombiesDeOP] Excepción cargando textura {imageName}: {e}");
                return null;
            }
        }

        public void SetState(string state)
        {
            if (string.IsNullOrEmpty(state))
            {
                state = "none";
            }

            if (currentState == state)
            {
                return;
            }

            currentState = state;
            ModLogger.Info($"🧩 [ZombiesDeOP] Cambiando estado de visibilidad a: {state}");

            switch (state)
            {
                case "hidden":
                    currentTexture = hiddenTexture;
                    isVisible = hiddenTexture != null;
                    if (!isVisible)
                    {
                        ModLogger.Warn("⚠️ [ZombiesDeOP] Textura HIDDEN no disponible");
                    }
                    else
                    {
                        ModLogger.Info("🧩 [ZombiesDeOP] Mostrando icono HIDDEN");
                    }
                    break;

                case "seen":
                    currentTexture = seenTexture;
                    isVisible = seenTexture != null;
                    if (!isVisible)
                    {
                        ModLogger.Warn("⚠️ [ZombiesDeOP] Textura SEEN no disponible");
                    }
                    else
                    {
                        ModLogger.Info("🧩 [ZombiesDeOP] Mostrando icono SEEN");
                    }
                    break;

                default:
                    HideTexture();
                    break;
            }
        }

        private void HideTexture()
        {
            isVisible = false;
            currentTexture = null;
            currentState = "none";
            ModLogger.Info("🧩 [ZombiesDeOP] Ocultando icono de visibilidad");
        }

        private void OnGUI()
        {
            if (!isVisible || currentTexture == null)
            {
                return;
            }

            const int size = 64;
            var rect = new Rect(50f, 50f, size, size);

            // Prefijos explícitos para garantizar símbolos del IMGUI module
            UnityEngine.GUI.DrawTexture(rect, currentTexture, UnityEngine.ScaleMode.ScaleToFit, true);
        }

        private void OnDestroy()
        {
            if (hiddenTexture != null)
            {
                Destroy(hiddenTexture);
                hiddenTexture = null;
            }

            if (seenTexture != null)
            {
                Destroy(seenTexture);
                seenTexture = null;
            }

            currentTexture = null;
            ModLogger.LogDebug("🧩 [ZombiesDeOP] UIOverlayComponent destruido");
        }
    }
}
