using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.ZombieBehaviorModule
{
    public sealed class ZombieBehaviorManager : UnityEngine.MonoBehaviour, IModule
    {
        public bool IsEnabled => BehaviorConfig.EnableDepthAwareness;
        public string ModuleName => "Ajustes de Comportamiento Zombie";

        public void InitializeModule()
        {
            enabled = IsEnabled;
            if (IsEnabled)
            {
                ModLogger.Log("Módulo de comportamiento zombie activado (Depth Awareness)");
            }
        }

        public void Shutdown()
        {
            enabled = false;
        }
    }
}
