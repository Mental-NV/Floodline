using Floodline.Core;
using Floodline.Core.Movement;
using UnityEngine;

namespace Floodline.Client
{
    /// <summary>
    /// Renders the Floodline grid state as a 3D visualization.
    /// - Reads voxel occupancy from the Core Simulation.State.Grid
    /// - Renders solids with their material types
    /// - Renders water as transparent overlay
    /// - Renders drain holes and freeze markers
    /// 
    /// For MVP, uses simple cube primitives with materials from FL-0508 constraints pack.
    /// </summary>
    public class GridRenderer : MonoBehaviour
    {
        [SerializeField]
        private float voxelSize = 1f;

        [SerializeField]
        private Material defaultMaterial;

        [SerializeField]
        private Material waterMaterial;

        [SerializeField]
        private Material drainMaterial;

        [SerializeField]
        private Material freezeMaterial;

        /// <summary>
        /// Called each frame to update grid visualization based on simulation state
        /// </summary>
        public void UpdateGridVisualization(Simulation sim)
        {
            if (sim == null)
                return;

            // Clear previous frames' visuals
            ClearGrid();

            // Iterate through grid and render voxels based on OccupancyType
            var grid = sim.State.Grid;
            var gridSize = grid.Size;

            for (int x = 0; x < gridSize.X; x++)
            {
                for (int y = 0; y < gridSize.Y; y++)
                {
                    for (int z = 0; z < gridSize.Z; z++)
                    {
                        if (!grid.TryGetVoxel(new Int3(x, y, z), out Voxel voxel))
                            continue;

                        // Skip empty cells
                        if (voxel.Type == OccupancyType.Empty)
                            continue;

                        // Render based on occupancy type
                        switch (voxel.Type)
                        {
                            case OccupancyType.Water:
                                RenderWater(x, y, z);
                                break;
                            case OccupancyType.Drain:
                                RenderDrain(x, y, z);
                                break;
                            case OccupancyType.Ice:
                                RenderFreeze(x, y, z);
                                break;
                            case OccupancyType.Solid:
                            case OccupancyType.Bedrock:
                            case OccupancyType.Porous:
                                RenderSolid(x, y, z, voxel);
                                break;
                        }
                    }
                }
            }

            // Render active piece (if any)
            if (sim.State.ActivePiece != null)
            {
                RenderActivePiece(sim.State.ActivePiece);
            }
        }

        /// <summary>
        /// Render a solid voxel with its material type
        /// </summary>
        private void RenderSolid(int x, int y, int z, Voxel voxel)
        {
            Vector3 worldPos = GridToWorldPosition(x, y, z);
            Material mat = GetMaterialForVoxel(voxel);

            CreateCube(worldPos, voxelSize, mat, $"Solid_{x}_{y}_{z}_{voxel.MaterialId}");
        }

        /// <summary>
        /// Render water voxel (transparent overlay)
        /// </summary>
        private void RenderWater(int x, int y, int z)
        {
            Vector3 worldPos = GridToWorldPosition(x, y, z);
            Material mat = waterMaterial ?? defaultMaterial;

            CreateCube(worldPos, voxelSize, mat, $"Water_{x}_{y}_{z}");
        }

        /// <summary>
        /// Render drain marker
        /// </summary>
        private void RenderDrain(int x, int y, int z)
        {
            Vector3 worldPos = GridToWorldPosition(x, y, z);
            Material mat = drainMaterial ?? defaultMaterial;

            // Render as slightly smaller cube at voxel center
            CreateCube(worldPos, voxelSize * 0.3f, mat, $"Drain_{x}_{y}_{z}");
        }

        /// <summary>
        /// Render freeze marker (visual ice indicator)
        /// </summary>
        private void RenderFreeze(int x, int y, int z)
        {
            Vector3 worldPos = GridToWorldPosition(x, y, z);
            Material mat = freezeMaterial ?? defaultMaterial;

            // Render as thin overlay on top of the voxel
            var pos = worldPos + Vector3.up * (voxelSize * 0.4f);
            CreateCube(pos, voxelSize * 0.2f, mat, $"Freeze_{x}_{y}_{z}");
        }

        /// <summary>
        /// Render the active piece (falling block)
        /// </summary>
        private void RenderActivePiece(ActivePiece piece)
        {
            if (piece == null)
                return;

            var worldPositions = piece.GetWorldPositions();
            foreach (var pos in worldPositions)
            {
                Vector3 worldPos = GridToWorldPosition(pos.X, pos.Y, pos.Z);
                
                // Create a Voxel record for material lookup
                var voxel = new Voxel(OccupancyType.Solid, piece.MaterialId);
                Material mat = GetMaterialForVoxel(voxel);

                CreateCube(worldPos, voxelSize * 0.95f, mat, $"ActivePiece_{pos.X}_{pos.Y}_{pos.Z}");
            }
        }

        /// <summary>
        /// Create a cube at world position with material
        /// </summary>
        private GameObject CreateCube(Vector3 position, float size, Material mat, string name)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.parent = transform;
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one * size;
            cube.name = name;

            // Remove collider for rendering-only cube
            var collider = cube.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            // Apply material
            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null && mat != null)
                renderer.material = mat;

            return cube;
        }

        /// <summary>
        /// Get material for a voxel based on its type and material ID
        /// Maps to placeholder materials from FL-0508 constraints pack
        /// </summary>
        private Material GetMaterialForVoxel(Voxel voxel)
        {
            // TODO: Map voxel.MaterialId to specific materials
            // For now, use default
            return defaultMaterial ?? new Material(Shader.Find("Standard"));
        }

        /// <summary>
        /// Convert grid coordinates (integers) to world space (floats)
        /// Grid uses integer coordinates; world space uses continuous floats
        /// </summary>
        private Vector3 GridToWorldPosition(int x, int y, int z)
        {
            return new Vector3(x * voxelSize, y * voxelSize, z * voxelSize);
        }

        /// <summary>
        /// Clear all rendered voxels
        /// </summary>
        private void ClearGrid()
        {
            // Destroy all child cubes
            foreach (Transform child in transform)
            {
                Object.Destroy(child.gameObject);
            }
        }
    }
}
