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
            ModLogger.Log("🧩 [ZombiesDeOP] UIOverlayComponent adjunto");
        }

        private void Start()
        {
            ModLogger.Log("🧩 [ZombiesDeOP] Inicializando UIOverlayComponent");
            LoadTextures();
            HideTexture();
        }

        private void LoadTextures()
        {
            hiddenTexture = LoadTexture(HiddenImageName);
            seenTexture = LoadTexture(SeenImageName);

            bool hiddenLoaded = hiddenTexture != null;
            bool seenLoaded = seenTexture != null;
            ModLogger.Log($"🧩 [ZombiesDeOP] Texturas cargadas -> Hidden: {hiddenLoaded}, Seen: {seenLoaded}");
        }

        private Texture2D LoadTexture(string imageName)
        {
            try
            {
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
                    ModLogger.Warning($"⚠️ [ZombiesDeOP] Textura no encontrada para {imageName}");
                    return null;
                }

                byte[] data = File.ReadAllBytes(texturePath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                if (!ImageConversion.LoadImage(texture, data))
                {
                    ModLogger.Error($"❌ [ZombiesDeOP] Falló la carga de imagen: {texturePath}");
                    Destroy(texture);
                    return null;
                }

                ModLogger.Debug($"🧩 [ZombiesDeOP] Textura cargada: {texturePath}");
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
            ModLogger.Log($"🧩 [ZombiesDeOP] Cambiando estado de visibilidad a: {state}");

            switch (state)
            {
                case "hidden":
                    currentTexture = hiddenTexture;
                    isVisible = hiddenTexture != null;
                    if (!isVisible)
                    {
                        ModLogger.Warning("⚠️ [ZombiesDeOP] Textura HIDDEN no disponible");
                    }
                    else
                    {
                        ModLogger.Log("🧩 [ZombiesDeOP] Mostrando icono HIDDEN");
                    }
                    break;
                case "seen":
                    currentTexture = seenTexture;
                    isVisible = seenTexture != null;
                    if (!isVisible)
                    {
                        ModLogger.Warning("⚠️ [ZombiesDeOP] Textura SEEN no disponible");
                    }
                    else
                    {
                        ModLogger.Log("🧩 [ZombiesDeOP] Mostrando icono SEEN");
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
            ModLogger.Log("🧩 [ZombiesDeOP] Ocultando icono de visibilidad");
        }

        private void OnGUI()
        {
            if (!isVisible || currentTexture == null)
            {
                return;
            }

            const int size = 64;
            var rect = new Rect(50f, 50f, size, size);
            GUI.DrawTexture(rect, currentTexture, ScaleMode.ScaleToFit, true);
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
            ModLogger.Debug("🧩 [ZombiesDeOP] UIOverlayComponent destruido");
        }
    }
}
