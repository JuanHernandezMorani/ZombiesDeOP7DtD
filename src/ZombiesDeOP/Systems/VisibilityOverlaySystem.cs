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
        private static bool overlayOwned;

        public static UIOverlayComponent OverlayComponent => overlayComponent;

        public static void Initialize(UIOverlayComponent existingComponent = null)
        {
            if (initialized)
            {
                return;
            }

            try
            {
                if (existingComponent != null)
                {
                    overlayComponent = existingComponent;
                    overlayObject = existingComponent.gameObject;
                    overlayOwned = false;
                }
                else
                {
                    overlayObject = new GameObject("ZombiesDeOP_UI");
                    overlayOwned = true;
                    overlayComponent = overlayObject.AddComponent<UIOverlayComponent>();
                }

                overlayObject.name = "ZombiesDeOP_UI";
                overlayObject.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(overlayObject);

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

            if (overlayOwned && overlayObject != null)
            {
                UnityEngine.Object.Destroy(overlayObject);
            }

            overlayObject = null;
            overlayComponent = null;
            overlayOwned = false;
            initialized = false;
            ModLogger.Info("🧹 [ZombiesDeOP] Overlay de visibilidad desmontado");
        }
    }
}
