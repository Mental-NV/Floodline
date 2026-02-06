using UnityEngine;
using System.Collections.Generic;

namespace Floodline.Client
{
    /// <summary>
    /// Centralizes material definitions for the voxel rendering system.
    /// Maps Core cell/material types to Unity materials for consistent visual presentation.
    /// 
    /// Material Palette:
    /// - STANDARD: Neutral gray/white for regular blocks (~RGB 220, 220, 220)
    /// - HEAVY: Dark with hazard pattern (diagonal stripes) for weighted blocks (~RGB 64, 64, 64)
    /// - REINFORCED: Metal frame appearance for reinforced blocks (~RGB 180, 180, 180 with grid pattern)
    /// - BEDROCK: Dark bedrock texture for foundation level (~RGB 50, 50, 50)
    /// - WATER: Translucent cyan/blue with strong horizontal surface line
    /// - ICE: Bright crystalline appearance (~RGB 200, 240, 255) with frost effect
    /// - POROUS: Lighter stone appearance for porous blocks (~RGB 200, 200, 180)
    /// - DRAIN: Small marker with distinctive color (orange ~RGB 255, 165, 0)
    /// - WALL: Subtle boundary marker (~RGB 128, 128, 128)
    /// </summary>
    public class MaterialPalette : MonoBehaviour
    {
        /// <summary>
        /// Material key enum for clean palette lookup
        /// </summary>
        public enum MaterialKey
        {
            Standard,    // Regular building blocks
            Heavy,       // Weight-limiting blocks
            Reinforced,  // Reinforced blocks
            Bedrock,     // Immovable foundation
            Porous,      // Porous/permeable blocks (water-absorbing)
            Water,       // Water occupancy
            Ice,         // Frozen water
            Drain,       // Drain hole marker
            Wall,        // Level boundary marker
            Default      // Fallback for unmapped types
        }

        [SerializeField]
        private Material _standardMaterial;

        [SerializeField]
        private Material _heavyMaterial;

        [SerializeField]
        private Material _reinforcedMaterial;

        [SerializeField]
        private Material _bedrockMaterial;

        [SerializeField]
        private Material _porousMaterial;

        [SerializeField]
        private Material _waterMaterial;

        [SerializeField]
        private Material _iceMaterial;

        [SerializeField]
        private Material _drainMaterial;

        [SerializeField]
        private Material _wallMaterial;

        [SerializeField]
        private Material _defaultMaterial;

        /// <summary>
        /// Static instance for global access (set by GameLoader)
        /// </summary>
        public static MaterialPalette Instance { get; private set; }

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Get material by key
        /// </summary>
        public Material GetMaterial(MaterialKey key)
        {
            return key switch
            {
                MaterialKey.Standard => _standardMaterial ?? _defaultMaterial,
                MaterialKey.Heavy => _heavyMaterial ?? _defaultMaterial,
                MaterialKey.Reinforced => _reinforcedMaterial ?? _defaultMaterial,
                MaterialKey.Bedrock => _bedrockMaterial ?? _defaultMaterial,
                MaterialKey.Porous => _porousMaterial ?? _defaultMaterial,
                MaterialKey.Water => _waterMaterial ?? _defaultMaterial,
                MaterialKey.Ice => _iceMaterial ?? _defaultMaterial,
                MaterialKey.Drain => _drainMaterial ?? _defaultMaterial,
                MaterialKey.Wall => _wallMaterial ?? _defaultMaterial,
                _ => _defaultMaterial ?? new Material(Shader.Find("Standard"))
            };
        }

        /// <summary>
        /// Get all materials as a dictionary
        /// </summary>
        public Dictionary<MaterialKey, Material> GetAllMaterials()
        {
            return new Dictionary<MaterialKey, Material>
            {
                { MaterialKey.Standard, _standardMaterial ?? _defaultMaterial },
                { MaterialKey.Heavy, _heavyMaterial ?? _defaultMaterial },
                { MaterialKey.Reinforced, _reinforcedMaterial ?? _defaultMaterial },
                { MaterialKey.Bedrock, _bedrockMaterial ?? _defaultMaterial },
                { MaterialKey.Porous, _porousMaterial ?? _defaultMaterial },
                { MaterialKey.Water, _waterMaterial ?? _defaultMaterial },
                { MaterialKey.Ice, _iceMaterial ?? _defaultMaterial },
                { MaterialKey.Drain, _drainMaterial ?? _defaultMaterial },
                { MaterialKey.Wall, _wallMaterial ?? _defaultMaterial },
            };
        }
    }
}
