using System;
using UnityEngine;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP.Systems
{
    public static class VisibilityOverlaySystem
    {
        private static GameObject overlayObject;
        private static UIOverlayComponent overlayComponent;
        private static bool initialized;

        public static UIOverlayComponent OverlayComponent => overlayComponent;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            try
            {
                overlayObject = new GameObject("ZombiesDeOP_UI");
                overlayObject.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(overlayObject);

                overlayComponent = overlayObject.AddComponent<UIOverlayComponent>();

                initialized = true;
                ModLogger.Info("🖼️ [ZombiesDeOP] Overlay de visibilidad inicializado");
            }
            catch (Exception e)
            {
                ModLogger.Error($"❌ [ZombiesDeOP] Error al crear overlay de visibilidad: {e}");
                Shutdown();
            }
        }

        public static void Shutdown()
        {
            if (!initialized)
            {
                return;
            }

            if (overlayObject != null)
            {
                UnityEngine.Object.Destroy(overlayObject);
                overlayObject = null;
            }

            overlayComponent = null;
            initialized = false;
            ModLogger.Info("🧹 [ZombiesDeOP] Overlay de visibilidad desmontado");
        }
    }
}
