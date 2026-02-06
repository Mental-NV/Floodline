using UnityEngine;

namespace Floodline.Client
{
    /// <summary>
    /// Manages camera positioning, focus, and view modes.
    /// Implements Input_Feel_v0_2.md §7: Camera Rules (MVP).
    /// 
    /// Key principles:
    /// - Camera maintains stable horizon (does NOT rotate with world)
    /// - Grid/board rotates while camera stays fixed
    /// - Supports 4 isometric snap views (NE, NW, SE, SW)
    /// - Focuses on active piece + top of structure on spawn
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        /// <summary>
        /// Camera snap view presets
        /// </summary>
        public enum SnapView
        {
            NE,  // Northeast isometric
            NW,  // Northwest isometric
            SE,  // Southeast isometric
            SW   // Southwest isometric
        }

        [SerializeField]
        private Camera mainCamera;

        [SerializeField]
        private float cameraDistance = 20f;

        [SerializeField]
        private float cameraHeight = 15f;

        [SerializeField]
        private float focusLerpSpeed = 5f;

        [SerializeField]
        private float snapViewLerpSpeed = 3f;

        // Isometric camera angles for each snap view (relative to grid center)
        private static readonly Vector3[] SnapViewOffsets = new Vector3[4]
        {
            new Vector3(1, 1, 1).normalized * 20f,    // NE: positive X, positive Z (+ height)
            new Vector3(-1, 1, 1).normalized * 20f,   // NW: negative X, positive Z (+ height)
            new Vector3(1, 1, -1).normalized * 20f,   // SE: positive X, negative Z (+ height)
            new Vector3(-1, 1, -1).normalized * 20f   // SW: negative X, negative Z (+ height)
        };

        private Vector3 targetPosition;
        private Vector3 currentPosition;
        private bool isSnapping = false;

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            // Initialize at NE isometric view
            currentPosition = SnapViewOffsets[0];
            targetPosition = currentPosition;
            mainCamera.transform.position = currentPosition;
            mainCamera.transform.LookAt(Vector3.zero, Vector3.up);
        }

        private void LateUpdate()
        {
            // Update camera position via lerp
            currentPosition = Vector3.Lerp(currentPosition, targetPosition, Time.deltaTime * focusLerpSpeed);
            mainCamera.transform.position = currentPosition;
            mainCamera.transform.LookAt(Vector3.zero, Vector3.up);
        }

        /// <summary>
        /// Request camera snap to a named view
        /// </summary>
        public void SnapTo(SnapView view)
        {
            targetPosition = SnapViewOffsets[(int)view];
            isSnapping = true;
        }

        /// <summary>
        /// Set camera focus on a world position
        /// (Useful for focusing on active piece after spawn)
        /// </summary>
        public void FocusOn(Vector3 worldPosition)
        {
            // Calculate offset from focus point to camera (maintain current distance/angle)
            Vector3 currentOffset = currentPosition - Vector3.zero;  // Currently looking at origin
            Vector3 cameraDirection = currentOffset.normalized;
            
            // Position camera at same distance/angle away from the focus point
            targetPosition = worldPosition + (cameraDirection * currentOffset.magnitude);
        }

        /// <summary>
        /// Get the current camera look direction (normalized)
        /// </summary>
        public Vector3 GetLookDirection()
        {
            return (Vector3.zero - mainCamera.transform.position).normalized;
        }

        /// <summary>
        /// Convert viewport coordinates to a ray for picking
        /// </summary>
        public Ray GetPickRay(Vector2 viewportPoint)
        {
            return mainCamera.ViewportPointToRay(viewportPoint);
        }
    }
}
