using UnityEngine;

namespace Floodline.Client
{
    /// <summary>
    /// Lighting configuration for Floodline MVP.
    /// Provides simple, non-distracting lighting that preserves voxel silhouette readability.
    /// 
    /// Design Goals:
    /// - Clear silhouettes for all voxel types
    /// - Minimal shadow acne or harsh shadows
    /// - Consistent brightness across the playfield
    /// - Non-distracting environment (no heavy post-processing)
    /// - Performance-friendly (2-3 lights maximum)
    /// 
    /// Light Setup:
    /// 1. Directional light (main): Simulates sunlight, provides primary illumination
    ///    - Angle: Adjusted for isometric camera view (roughly NE-facing)
    ///    - Intensity: 0.7-0.8 (not overly bright, preserves depth)
    ///    - Shadows: Moderate quality, reduced artifact risk
    /// 
    /// 2. Ambient light: Flat fill to eliminate harsh shadows
    ///    - Color: Neutral gray or slight blue tint
    ///    - Intensity: 0.3-0.4
    /// 
    /// 3. Secondary fill light (optional): Softened back light
    ///    - Intensity: 0.2-0.3 (very subtle)
    ///    - Helps define edges without creating competing shadows
    /// </summary>
    public class LightingSetup : MonoBehaviour
    {
        [Header("Directional Light (Main)")]
        [SerializeField]
        private Transform mainLightTransform;

        [SerializeField]
        private float mainLightIntensity = 0.75f;

        [SerializeField]
        private Color mainLightColor = Color.white;

        [Header("Ambient Light")]
        [SerializeField]
        private float ambientIntensity = 0.35f;

        [SerializeField]
        private Color ambientColor = new Color(0.9f, 0.9f, 1f);  // Slight cool tint

        [Header("Fill Light (Optional)")]
        [SerializeField]
        private Transform fillLightTransform;

        [SerializeField]
        private float fillLightIntensity = 0.25f;

        [SerializeField]
        private Color fillLightColor = Color.white;

        [Header("Shadow Settings")]
        [SerializeField]
        private bool useShadows = true;

        [SerializeField]
        private int shadowResolution = 2048;

        private Light _mainLight;
        private Light _fillLight;

        private void OnEnable()
        {
            ApplyLighting();
        }

        /// <summary>
        /// Apply lighting configuration to the scene.
        /// Creates lights if transform references are provided, or uses existing scene lights.
        /// </summary>
        private void ApplyLighting()
        {
            // Configure or create main directional light
            if (mainLightTransform != null)
            {
                _mainLight = mainLightTransform.GetComponent<Light>();
                if (_mainLight == null)
                    _mainLight = mainLightTransform.gameObject.AddComponent<Light>();
            }
            else
            {
                // Find existing main light or create one
                _mainLight = FindObjectOfType<Light>();
                if (_mainLight == null)
                {
                    var lightObj = new GameObject("MainDirectionalLight");
                    lightObj.transform.parent = transform;
                    _mainLight = lightObj.AddComponent<Light>();
                    mainLightTransform = lightObj.transform;
                }
            }

            // Configure main light
            _mainLight.type = LightType.Directional;
            _mainLight.intensity = mainLightIntensity;
            _mainLight.color = mainLightColor;
            _mainLight.shadows = useShadows ? LightShadows.Soft : LightShadows.None;

            // Position for isometric view: roughly NE (elevated, forward-facing)
            if (mainLightTransform != null)
            {
                mainLightTransform.rotation = Quaternion.Euler(45f, 45f, 0f);
            }

            // Configure ambient light
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor * ambientIntensity;

            // Configure fill light if provided
            if (fillLightTransform != null)
            {
                _fillLight = fillLightTransform.GetComponent<Light>();
                if (_fillLight == null)
                    _fillLight = fillLightTransform.gameObject.AddComponent<Light>();

                _fillLight.type = LightType.Directional;
                _fillLight.intensity = fillLightIntensity;
                _fillLight.color = fillLightColor;
                _fillLight.shadows = LightShadows.None;  // No shadows for fill light

                // Position opposite to main light (subtle back lighting)
                fillLightTransform.rotation = Quaternion.Euler(40f, 225f, 0f);
            }

            // Apply shadow resolution if using shadows
            if (useShadows && _mainLight != null)
            {
                QualitySettings.shadowResolution = (ShadowResolution)shadowResolution;
            }

            Debug.Log($"Lighting setup applied: main={mainLightIntensity}@{mainLightColor}, ambient={ambientIntensity}@{ambientColor}");
        }

        /// <summary>
        /// Editor-only method to visualize light directions
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (mainLightTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(mainLightTransform.position, mainLightTransform.forward * 5f);
            }

            if (fillLightTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(fillLightTransform.position, fillLightTransform.forward * 5f);
            }
        }
    }
}
