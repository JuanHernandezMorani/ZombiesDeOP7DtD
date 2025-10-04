using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;

namespace ExtendedStorageReach
{
    /// <summary>
    /// Punto de entrada del módulo: garantiza la carga de la configuración y mantiene vivo el watcher del archivo XML.
    /// </summary>
    [HarmonyPatch]
    public static class StorageReachPatch
    {
        private static bool s_bootstrapped;

        [HarmonyPatch(typeof(GameManager), "Awake")]
        [HarmonyPostfix]
        private static void OnGameManagerAwake()
        {
            if (s_bootstrapped)
            {
                return;
            }

            StorageReachLogger.Info("Inicializando ExtendedStorageReach");
            StorageReachConfig.EnsureLoaded();
            s_bootstrapped = true;
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        [HarmonyPostfix]
        private static void OnGameManagerUpdate()
        {
            if (!s_bootstrapped)
            {
                return;
            }

            StorageReachConfig.Update();
        }

        /// <summary>
        /// Amplía la comprobación de distancia para cualquier TileEntity de tipo cofre (incluidos los de War3zuk).
        /// </summary>
        /// <returns>
        ///     <c>false</c> cuando el acceso extendido ha sido concedido para evitar la ejecución del método original.
        ///     <c>true</c> en cualquier otro caso, respetando el flujo vanilla y de mods como QuickStack.
        /// </returns>
        [HarmonyPrefix]
        private static bool Prefix(object __instance, ref bool __result, [HarmonyArgument(0)] object interactingEntity)
        {
            if (__instance == null)
            {
                return true;
            }

            if (!s_bootstrapped)
            {
                StorageReachConfig.EnsureLoaded();
                s_bootstrapped = true;
            }

            if (!StorageReachManager.IsSupportedContainer(__instance))
            {
                return true;
            }

            if (StorageReachManager.ShouldBypassForQuickStack())
            {
                return true;
            }

            var entity = StorageReachManager.ResolveEntity(interactingEntity);
            if (entity == null)
            {
                return true;
            }

            var tileEntity = StorageReachManager.ResolveTileEntity(__instance);
            if (tileEntity == null)
            {
                return true;
            }

            if (StorageReachManager.HasExtendedAccess(entity, tileEntity))
            {
                __result = true;
                return false;
            }

            return true;
        }

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var typesToPatch = new List<Type>
            {
                typeof(TileEntityLootContainer)
            };

            var secureType = AccessTools.TypeByName("TileEntitySecureLootContainer");
            if (secureType != null)
            {
                typesToPatch.Add(secureType);
            }

            // Compatibilidad con War3zuk y otras variantes personalizadas.
            foreach (var candidateName in StorageReachManager.War3zukContainerTypeNames)
            {
                var candidateType = AccessTools.TypeByName(candidateName);
                if (candidateType != null)
                {
                    typesToPatch.Add(candidateType);
                }
            }

            foreach (var type in typesToPatch)
            {
                var method = AccessTools.Method(type, "IsPlayerWithin");
                if (method != null)
                {
                    yield return method;
                }
            }
        }
    }

    internal static class StorageReachManager
    {
        internal static readonly string[] War3zukContainerTypeNames =
        {
            "War3zuk.TileEntityLootContainerWar3zuk",
            "War3zuk.TileEntitySecureLootContainerWar3zuk",
            "TileEntityLootContainerWar3zuk",
            "TileEntitySecureLootContainerWar3zuk",
            "TileEntitySecureLootContainerUnlocked"
        };

        private static readonly Vector3 HalfBlock = new(0.5f, 0.5f, 0.5f);

        internal static bool IsSupportedContainer(object tileEntityObject)
        {
            if (tileEntityObject == null)
            {
                return false;
            }

            var type = tileEntityObject.GetType();
            if (typeof(TileEntityLootContainer).IsAssignableFrom(type))
            {
                return true;
            }

            var secureType = AccessTools.TypeByName("TileEntitySecureLootContainer");
            if (secureType != null && secureType.IsAssignableFrom(type))
            {
                return true;
            }

            foreach (var candidateName in War3zukContainerTypeNames)
            {
                var candidateType = AccessTools.TypeByName(candidateName);
                if (candidateType != null && candidateType.IsAssignableFrom(type))
                {
                    return true;
                }
            }

            return false;
        }

        internal static TileEntity ResolveTileEntity(object instance)
        {
            if (instance is TileEntity tileEntity)
            {
                return tileEntity;
            }

            return null;
        }

        internal static EntityAlive ResolveEntity(object interactingEntity)
        {
            switch (interactingEntity)
            {
                case EntityAlive alive:
                    return alive;
                case EntityPlayerLocal local:
                    return local;
                case EntityPlayer player:
                    return player;
                default:
                    return null;
            }
        }

        internal static bool ShouldBypassForQuickStack()
        {
            var quickStackType = AccessTools.TypeByName("QuickStack.QuickStackManager");
            if (quickStackType == null)
            {
                return false;
            }

            try
            {
                var property = AccessTools.Property(quickStackType, "IsRunning") ?? AccessTools.Property(quickStackType, "Enabled");
                if (property != null && property.PropertyType == typeof(bool))
                {
                    var value = property.GetValue(null, null);
                    if (value is bool boolValue)
                    {
                        return boolValue;
                    }
                }

                var field = AccessTools.Field(quickStackType, "IsRunning") ?? AccessTools.Field(quickStackType, "s_isRunning");
                if (field != null && field.FieldType == typeof(bool))
                {
                    var value = field.GetValue(null);
                    if (value is bool boolField)
                    {
                        return boolField;
                    }
                }
            }
            catch (Exception exception)
            {
                StorageReachLogger.Warn($"QuickStack detection failed: {exception.Message}");
            }

            return false;
        }

        internal static bool HasExtendedAccess(EntityAlive entity, TileEntity tileEntity)
        {
            if (entity == null || tileEntity == null)
            {
                return false;
            }

            float reachDistance = Mathf.Max(StorageReachConfig.ReachDistance, 0f);
            if (reachDistance <= 0f)
            {
                return false;
            }

            Vector3 containerCenter = GetContainerWorldCenter(tileEntity);
            Vector3 entityPosition = entity.position;
            float distance = Vector3.Distance(entityPosition, containerCenter);

            if (distance <= StorageReachConfig.MinimumVanillaDistance)
            {
                return true;
            }

            if (distance > reachDistance)
            {
                return false;
            }

            if (HasLineOfSight(entity, tileEntity, containerCenter))
            {
                return true;
            }

            return distance <= StorageReachConfig.ProximityTolerance;
        }

        private static Vector3 GetContainerWorldCenter(TileEntity tileEntity)
        {
            Vector3 center;
            try
            {
                var worldPos = tileEntity.ToWorldPos();
                center = new Vector3(worldPos.x, worldPos.y, worldPos.z) + HalfBlock;
            }
            catch
            {
                center = tileEntity.localChunkPos + HalfBlock;
            }

            return center;
        }

        private static bool HasLineOfSight(EntityAlive entity, TileEntity tileEntity, Vector3 containerCenter)
        {
            try
            {
                var world = entity.world ?? GameManager.Instance?.World;
                if (world == null)
                {
                    return false;
                }

                Vector3 start = GetEyePosition(entity);
                Vector3 direction = containerCenter - start;
                float distance = direction.magnitude;
                if (distance <= 0.1f)
                {
                    return true;
                }

                int steps = Mathf.Clamp(Mathf.CeilToInt(distance * 4f), 1, 256);
                Vector3 step = direction / steps;
                var targetBlock = tileEntity.ToWorldPos();

                for (int i = 1; i < steps; i++)
                {
                    Vector3 sample = start + step * i;
                    var blockPos = new Vector3i(Mathf.FloorToInt(sample.x), Mathf.FloorToInt(sample.y), Mathf.FloorToInt(sample.z));
                    if (blockPos.Equals(targetBlock))
                    {
                        continue;
                    }

                    var blockValue = world.GetBlock(blockPos);
                    if (!IsPassable(blockValue))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                StorageReachLogger.Warn($"Line of sight check failed: {exception.Message}");
                return true;
            }
        }

        private static Vector3 GetEyePosition(EntityAlive entity)
        {
            float eyeHeight = 1.6f;
            try
            {
                var method = AccessTools.Method(entity.GetType(), "GetEyeHeight");
                if (method != null && method.ReturnType == typeof(float) && method.GetParameters().Length == 0)
                {
                    eyeHeight = (float)method.Invoke(entity, null);
                }
                else
                {
                    var field = AccessTools.Field(entity.GetType(), "eyeHeight");
                    if (field != null && field.FieldType == typeof(float))
                    {
                        eyeHeight = (float)field.GetValue(entity);
                    }
                }
            }
            catch (Exception exception)
            {
                StorageReachLogger.Debug($"Failed to read eye height: {exception.Message}");
            }

            return entity.position + Vector3.up * Mathf.Max(1f, eyeHeight);
        }

        private static bool IsPassable(BlockValue blockValue)
        {
            try
            {
                if (blockValue.type == 0)
                {
                    return true;
                }

                var block = blockValue.Block;
                if (block == null)
                {
                    return true;
                }

                if (block.IsAir)
                {
                    return true;
                }

                var shape = block.shape;
                if (shape == null)
                {
                    return false;
                }

                var airFace = AccessTools.Method(shape.GetType(), "IsAirFace", new[] { typeof(int) });
                if (airFace != null)
                {
                    var result = airFace.Invoke(shape, new object[] { 0 });
                    if (result is bool boolResult)
                    {
                        return boolResult;
                    }
                }

                var isPassable = AccessTools.Method(shape.GetType(), "IsPassable", Type.EmptyTypes);
                if (isPassable != null)
                {
                    var result = isPassable.Invoke(shape, Array.Empty<object>());
                    if (result is bool boolResult)
                    {
                        return boolResult;
                    }
                }

                return false;
            }
            catch
            {
                return true;
            }
        }
    }

    internal static class StorageReachConfig
    {
        private const float DefaultReachDistance = 20f;
        private const string ConfigFileName = "StorageReach.xml";

        private static readonly object Sync = new();
        private static FileSystemWatcher s_watcher;
        private static bool s_loaded;
        private static bool s_pendingReload;
        private static DateTime s_reloadRequestedAt;

        internal static float ReachDistance { get; private set; } = DefaultReachDistance;
        internal static float MinimumVanillaDistance { get; private set; } = 4.0f;
        internal static float ProximityTolerance { get; private set; } = 5.5f;

        internal static void EnsureLoaded()
        {
            lock (Sync)
            {
                if (s_loaded)
                {
                    return;
                }

                Load();
                SetupWatcher();
                s_loaded = true;
            }
        }

        internal static void Update()
        {
            if (!s_pendingReload)
            {
                return;
            }

            if ((DateTime.UtcNow - s_reloadRequestedAt).TotalMilliseconds < 200)
            {
                return;
            }

            lock (Sync)
            {
                s_pendingReload = false;
                Load();
            }
        }

        private static void SetupWatcher()
        {
            try
            {
                string configPath = GetConfigPath();
                string directory = Path.GetDirectoryName(configPath);
                if (string.IsNullOrEmpty(directory))
                {
                    return;
                }

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                s_watcher = new FileSystemWatcher(directory, ConfigFileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                s_watcher.Changed += OnConfigChanged;
                s_watcher.Created += OnConfigChanged;
                s_watcher.Renamed += OnConfigChanged;
            }
            catch (Exception exception)
            {
                StorageReachLogger.Warn($"Failed to watch config: {exception.Message}");
            }
        }

        private static void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            lock (Sync)
            {
                s_pendingReload = true;
                s_reloadRequestedAt = DateTime.UtcNow;
            }
        }

        private static void Load()
        {
            string configPath = GetConfigPath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? string.Empty);

                if (!File.Exists(configPath))
                {
                    CreateDefaultConfig(configPath);
                    ReachDistance = DefaultReachDistance;
                    StorageReachLogger.Info($"Archivo de configuración creado en {configPath} con distancia {DefaultReachDistance}.");
                    return;
                }

                var document = XDocument.Load(configPath);
                var root = document.Root;
                if (root == null)
                {
                    throw new InvalidDataException("StorageReach.xml carece de nodo raíz.");
                }

                var reachElement = root.Element("ReachDistance");
                if (reachElement != null)
                {
                    var attribute = reachElement.Attribute("value");
                    if (attribute != null)
                    {
                        if (float.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                        {
                            ReachDistance = Mathf.Max(0f, value);
                        }
                    }
                }

                StorageReachLogger.Info($"Configuración cargada. ReachDistance = {ReachDistance:0.00}.");
            }
            catch (Exception exception)
            {
                ReachDistance = DefaultReachDistance;
                StorageReachLogger.Warn($"No se pudo leer StorageReach.xml ({exception.Message}). Se usará el valor por defecto {DefaultReachDistance}.");
            }
        }

        private static void CreateDefaultConfig(string path)
        {
            var document = new XDocument(
                new XElement("StorageReach",
                    new XElement("ReachDistance", new XAttribute("value", DefaultReachDistance.ToString(CultureInfo.InvariantCulture)))));

            document.Save(path);
        }

        private static string GetConfigPath()
        {
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var basePath = string.IsNullOrEmpty(assemblyPath)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(Path.Combine(assemblyPath, ".."));

            return Path.Combine(basePath, "Config", ConfigFileName);
        }
    }

    internal static class StorageReachLogger
    {
        private const string Prefix = "[ExtendedStorageReach] ";

        internal static void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        internal static void Warn(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        internal static void Debug(string message)
        {
#if DEBUG
            UnityEngine.Debug.Log(Prefix + "[DEBUG] " + message);
#else
            _ = message;
#endif
        }
    }
}
