using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using ZombiesDeOP.Compatibility;
using ZombiesDeOP.Systems;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP
{
    public class ZombiesDeOPMain : IModApi
    {
        private const string HARMONY_ID = "com.juanhernandez.zombiesdeop.a21";
        private static HarmonyLib.Harmony harmonyInstance;
        private static bool overlayInitialized;
        private static GameObject runtimeHost;
        private static UIOverlayComponent overlayComponent;

        public void InitMod(Mod mod)
        {
            try
            {
                ModContext.Initialize(mod);

                ModLogger.Info("🎯 [ZombiesDeOP] Iniciando mod para Alpha 21...");

                ModSettings.Load();
                War3zukCompatibility.ApplyCompatibility();

                EnsureHarmony();
                EnsureRuntimeHost();

                DetectionSystem.Initialize();
                HUDManager.Initialize();
                BehaviorManager.Initialize();

                ModLogger.Info("✅ [ZombiesDeOP] Mod cargado exitosamente");
            }
            catch (Exception e)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error en inicialización", e);
            }
        }

        public void Shutdown()
        {
            try
            {
                harmonyInstance?.UnpatchSelf();
                ModLogger.Info("[ZombiesDeOP] Harmony patches desactivados correctamente.");
                harmonyInstance = null;
                HUDManager.Shutdown();
                DetectionSystem.Shutdown();
                ShutdownOverlay();
                BehaviorManager.Shutdown();
                ShutdownRuntimeHost();
                ModLogger.Info("✅ [ZombiesDeOP] Mod descargado correctamente");
            }
            catch (Exception e)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error en shutdown", e);
            }
        }

        private static void ShutdownOverlay()
        {
            if (!overlayInitialized)
            {
                return;
            }

            try
            {
                VisibilityOverlaySystem.Shutdown();
            }
            catch (Exception e)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error al detener overlay", e);
            }
            finally
            {
                overlayInitialized = false;
                overlayComponent = null;
            }
        }

        private static void EnsureHarmony()
        {
            if (harmonyInstance != null)
            {
                return;
            }

            harmonyInstance = new HarmonyLib.Harmony(HARMONY_ID);

            try
            {
                harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                ModLogger.Info("[ZombiesDeOP] PatchAll OK (v2.4)");
            }
            catch (Exception ex)
            {
                ModLogger.Error("⚠️ [ZombiesDeOP] Error aplicando parches Harmony", ex);
            }
        }

        private static void EnsureRuntimeHost()
        {
            if (runtimeHost != null)
            {
                return;
            }

            runtimeHost = new GameObject("ZombiesDeOP_Runtime");
            runtimeHost.hideFlags = HideFlags.HideAndDontSave;
            GameObject.DontDestroyOnLoad(runtimeHost);

            overlayComponent = runtimeHost.GetComponent<UIOverlayComponent>() ?? runtimeHost.AddComponent<UIOverlayComponent>();
            VisibilityOverlaySystem.Initialize(overlayComponent);
            overlayInitialized = true;

            if (runtimeHost.GetComponent<DetectionSystemRuntime>() == null)
            {
                runtimeHost.AddComponent<DetectionSystemRuntime>();
            }

            ModLogger.Info("🧠 [ZombiesDeOP] Runtime host inicializado para detección");
        }

        private static void ShutdownRuntimeHost()
        {
            if (runtimeHost == null)
            {
                return;
            }

            try
            {
                UnityEngine.Object.Destroy(runtimeHost);
            }
            catch (Exception ex)
            {
                ModLogger.Error("⚠️ [ZombiesDeOP] Error al destruir runtime host", ex);
            }
            finally
            {
                runtimeHost = null;
            }
        }
    }
}
