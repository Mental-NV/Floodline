using Floodline.Core;
using UnityEngine;

namespace Floodline.Client
{
    /// <summary>
    /// Maps Floodline Core voxel types and material IDs to MaterialPalette keys.
    /// Acts as a translation layer between Core data structure and Unity rendering.
    /// 
    /// Rules:
    /// - OccupancyType.Solid: Maps to material based on MaterialId and voxel properties
    /// - OccupancyType.Bedrock: Always Bedrock material
    /// - OccupancyType.Porous: Always Porous material
    /// - OccupancyType.Water: Always Water material
    /// - OccupancyType.Ice: Always Ice material
    /// - OccupancyType.Drain: Always Drain material
    /// - OccupancyType.Empty: No rendering (filtered upstream)
    /// </summary>
    public static class VoxelMaterialMapper
    {
        /// <summary>
        /// Get the material palette key for a voxel.
        /// Maps occupancy type and material ID to visual presentation.
        /// </summary>
        public static MaterialPalette.MaterialKey GetMaterialKey(Voxel voxel)
        {
            return voxel.Type switch
            {
                OccupancyType.Bedrock => MaterialPalette.MaterialKey.Bedrock,
                OccupancyType.Porous => MaterialPalette.MaterialKey.Porous,
                OccupancyType.Water => MaterialPalette.MaterialKey.Water,
                OccupancyType.Ice => MaterialPalette.MaterialKey.Ice,
                OccupancyType.Drain => MaterialPalette.MaterialKey.Drain,
                OccupancyType.Solid => GetSolidMaterialKey(voxel.MaterialId),
                OccupancyType.Empty => MaterialPalette.MaterialKey.Default,
                _ => MaterialPalette.MaterialKey.Default
            };
        }

        /// <summary>
        /// Map Core MaterialId to visual material type.
        /// MaterialId values from Floodline.Core:
        /// - 0: STANDARD (default building block)
        /// - 1: HEAVY (weight-limiting blocks)
        /// - 2: REINFORCED (reinforced blocks)
        /// 
        /// If additional material IDs are added to Core, extend this mapping.
        /// </summary>
        private static MaterialPalette.MaterialKey GetSolidMaterialKey(uint materialId)
        {
            return materialId switch
            {
                0 => MaterialPalette.MaterialKey.Standard,
                1 => MaterialPalette.MaterialKey.Heavy,
                2 => MaterialPalette.MaterialKey.Reinforced,
                _ => MaterialPalette.MaterialKey.Standard  // Default fallback
            };
        }

        /// <summary>
        /// Get material from palette using voxel data.
        /// Returns null if palette not available (GridRenderer will use default).
        /// </summary>
        public static Material GetMaterial(Voxel voxel)
        {
            if (MaterialPalette.Instance == null)
                return null;

            var key = GetMaterialKey(voxel);
            return MaterialPalette.Instance.GetMaterial(key);
        }
    }
}
