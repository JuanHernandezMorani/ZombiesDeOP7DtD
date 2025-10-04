using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.ZombieDetectionSystem
{
    public sealed class DetectionEngine : MonoBehaviour, IModule
    {
        private readonly List<EntityEnemy> nearbyZombies = new();
        private EntityPlayerLocal player;
        private float lastCheckTime;
        private bool isDetected;
        private DetectionHUDManager hudManager;

        public bool IsEnabled => DetectionConfig.EnableDetectionSystem;
        public string ModuleName => "Sistema de Detección de Zombies";

        public event Action<bool> DetectionStateChanged;

        public IReadOnlyList<EntityEnemy> NearbyZombies => nearbyZombies;
        public bool IsPlayerDetected => isDetected;

        public void InitializeModule()
        {
            ModConfig.ConfigReloaded += OnConfigReloaded;
            lastCheckTime = Time.time;
            enabled = IsEnabled;
        }

        public void Shutdown()
        {
            ModConfig.ConfigReloaded -= OnConfigReloaded;
            if (hudManager != null)
            {
                hudManager.Detach();
                hudManager = null;
            }

            nearbyZombies.Clear();
            enabled = false;
        }

        internal void AttachHud(DetectionHUDManager manager)
        {
            hudManager = manager;
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void OnConfigReloaded()
        {
            enabled = IsEnabled;
        }

        private void Update()
        {
            if (!IsEnabled)
            {
                return;
            }

            try
            {
                if (!EnsurePlayerReference())
                {
                    return;
                }

                if (Time.time - lastCheckTime < DetectionConfig.CheckInterval)
                {
                    return;
                }

                lastCheckTime = Time.time;
                EvaluateDetection();
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error durante la actualización del sistema de detección: {ex}");
            }
        }

        private bool EnsurePlayerReference()
        {
            if (player != null)
            {
                return true;
            }

            try
            {
                player = GameManager.Instance?.World?.GetPrimaryPlayer();
                if (player == null)
                {
                    return false;
                }

                ModLogger.Debug("Referencia al jugador local establecida correctamente.");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Error($"No se pudo obtener el jugador local: {ex}");
                return false;
            }
        }

        private void EvaluateDetection()
        {
            if (player == null)
            {
                return;
            }

            nearbyZombies.Clear();
            bool detected = false;

            try
            {
                var world = GameManager.Instance?.World;
                if (world == null)
                {
                    return;
                }

                var position = player.position;
                var detectionRange = Mathf.CeilToInt(DetectionConfig.BaseDetectionRange);
                var entities = world.GetEntitiesInRange((int)position.x, (int)position.y, (int)position.z, detectionRange);
                foreach (var entity in entities)
                {
                    if (entity is not EntityEnemy zombie || !zombie.IsAlive())
                    {
                        continue;
                    }

                    nearbyZombies.Add(zombie);

                    if (CanZombieDetectPlayer(zombie, position))
                    {
                        detected = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error al evaluar la detección de zombies: {ex}");
            }

            if (detected != isDetected)
            {
                isDetected = detected;
                ModLogger.Debug($"Estado de detección actualizado: {(isDetected ? "DETECTADO" : "OCULTO")}");
                DetectionStateChanged?.Invoke(isDetected);
            }

            hudManager?.UpdateZombieCount(nearbyZombies.Count);
        }

        private bool CanZombieDetectPlayer(EntityEnemy zombie, Vector3 playerPosition)
        {
            var zombiePosition = zombie.position;
            float distance = Vector3.Distance(playerPosition, zombiePosition);
            if (distance > DetectionConfig.BaseDetectionRange)
            {
                return false;
            }

            if (!HasLineOfSight(zombie, player))
            {
                return false;
            }

            if (!IsInFieldOfView(zombie, player))
            {
                return false;
            }

            if (PlayerStateUtility.IsPlayerStealthed(player) && distance > DetectionConfig.HearingRange)
            {
                return false;
            }

            return true;
        }

        private bool HasLineOfSight(EntityEnemy zombie, EntityPlayerLocal target)
        {
            try
            {
                var senses = zombie.Senses;
                if (senses != null)
                {
                    var canSee = AccessTools.Method(senses.GetType(), "CanSee", new[] { typeof(Entity) });
                    if (canSee != null && canSee.Invoke(senses, new object[] { target }) is bool result)
                    {
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Fallo al consultar Senses.CanSee: {ex.Message}");
            }

            var from = zombie.getHeadPosition();
            var to = target.getHeadPosition();
            return !Physics.Linecast(from, to, out var hit) || hit.collider == null || hit.collider.gameObject == target.gameObject;
        }

        private bool IsInFieldOfView(EntityEnemy zombie, EntityPlayerLocal target)
        {
            var direction = (target.position - zombie.position).normalized;
            var forward = zombie.GetForwardVector();
            var angle = Vector3.Angle(forward, direction);
            return angle <= DetectionConfig.DetectionAngle * 0.5f;
        }
    }
}
