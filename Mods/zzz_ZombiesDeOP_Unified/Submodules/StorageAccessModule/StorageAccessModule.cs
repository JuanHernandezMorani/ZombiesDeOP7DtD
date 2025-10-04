using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.StorageAccessModule
{
    public sealed class StorageAccessModule : UnityEngine.MonoBehaviour, IModule
    {
        public bool IsEnabled => StorageAccessConfig.EnableExtendedReach;
        public string ModuleName => "Acceso Extendido a Almacenamiento";

        public void InitializeModule()
        {
            enabled = IsEnabled;
            if (IsEnabled)
            {
                ModLogger.Log("Acceso extendido a cofres habilitado");
            }
        }

        public void Shutdown()
        {
            enabled = false;
        }
    }
}
