using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ZombieDetectionHUD
{
    /// <summary>
    /// Executes the zombie detection logic previously driven by XML and broadcasts state changes to the HUD manager.
    /// </summary>
    public sealed class DetectionSystem : MonoBehaviour
    {
        private readonly List<EntityEnemy> nearbyZombies = new();
        private EntityPlayerLocal player;
        private float lastCheckTime;
        private bool isDetected;
        private HUDManager hudManager;

        /// <summary>
        /// Event raised whenever the detection status changes.
        /// </summary>
        public event Action<bool> DetectionStateChanged;

        /// <summary>
        /// Initializes the detection system with the HUD manager reference for synchronized updates.
        /// </summary>
        public void Initialize(HUDManager manager)
        {
            hudManager = manager;
        }

        /// <summary>
        /// Returns the current detection state for other systems.
        /// </summary>
        public bool IsPlayerDetected => isDetected;

        /// <summary>
        /// Returns a snapshot of nearby zombie entities detected during the last scan.
        /// </summary>
        public IReadOnlyList<EntityEnemy> NearbyZombies => nearbyZombies;

        private void Start()
        {
            lastCheckTime = Time.time;
        }

        private void Update()
        {
            try
            {
                if (!EnsurePlayerReference())
                {
                    return;
                }

                if (Time.time - lastCheckTime < ModConfig.CheckInterval)
                {
                    return;
                }

                lastCheckTime = Time.time;
                EvaluateDetection();
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error durante la actualización del sistema de detección.", ex);
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
                ModLogger.Error("No se pudo obtener el jugador local.", ex);
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
                // The vanilla API exposes a range lookup that expects integer coordinates; we mirror that contract here.
                var entities = world.GetEntitiesInRange((int)position.x, (int)position.y, (int)position.z, Mathf.CeilToInt(ModConfig.DetectionRange));
                foreach (var entity in entities)
                {
                    if (entity is not EntityEnemy zombie || !zombie.IsAlive())
                    {
                        continue;
                    }

                    // Cache the zombie for the HUD count before applying vision logic.
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
                ModLogger.Error("Error al evaluar la detección de zombies.", ex);
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
            if (distance > ModConfig.DetectionRange)
            {
                return false;
            }

            // Respect the vanilla senses by checking for a clear line of sight first.
            if (!HasLineOfSight(zombie, player))
            {
                return false;
            }

            // FOV restrictions mimic the XML configuration that previously controlled stealth difficulty.
            if (!IsInFieldOfView(zombie, player))
            {
                return false;
            }

            // Allow crouched players to sneak as long as they stay within the configured hearing radius.
            if (PlayerStateUtility.IsPlayerStealthed(player) && distance > ModConfig.HearingRange)
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
                    // Prefer the enemy's own senses implementation for maximum compatibility with overhauls.
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
            return angle <= ModConfig.DetectionAngle * 0.5f;
        }
    }
}
