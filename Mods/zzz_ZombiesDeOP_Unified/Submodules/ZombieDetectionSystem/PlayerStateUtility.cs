using System;
using HarmonyLib;
using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.ZombieDetectionSystem
{
    public static class PlayerStateUtility
    {
        public static bool IsPlayerStealthed(EntityPlayerLocal player)
        {
            if (player == null)
            {
                return false;
            }

            try
            {
                var type = player.GetType();
                var property = AccessTools.Property(type, "IsCrouching") ?? AccessTools.Property(type, "IsSneaking") ?? AccessTools.Property(type, "IsStealthed");
                if (property?.PropertyType == typeof(bool))
                {
                    return (bool)property.GetValue(player, null);
                }

                var field = AccessTools.Field(type, "isCrouching") ?? AccessTools.Field(type, "isSneaking");
                if (field?.FieldType == typeof(bool))
                {
                    return (bool)field.GetValue(player);
                }

                var stealthProperty = AccessTools.Property(type, "StealthState");
                if (stealthProperty?.GetValue(player, null) is Enum stealthEnum)
                {
                    return Convert.ToInt32(stealthEnum) != 0;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"No se pudo determinar el estado de sigilo del jugador: {ex.Message}");
            }

            return false;
        }
    }
}
