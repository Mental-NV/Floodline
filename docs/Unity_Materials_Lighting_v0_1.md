# Floodline Materials & Lighting Guide

## Overview

The Floodline visualizer uses a purposeful, minimal material palette and straightforward lighting setup to maintain voxel readability while supporting the game's aesthetic direction.

## Material Palette

### Material Types

1. **STANDARD** (Default blocks)
   - RGB: ~220, 220, 220 (light gray)
   - Regular building blocks placed by the player
   - Standard Unity Material with diffuse shading
   - No special properties

2. **HEAVY** (Weight-limiting blocks)
   - RGB: ~64, 64, 64 (dark gray)
   - Visual hazard pattern (diagonal stripes) to indicate weight-limiting
   - Shader: Standard with dark colors and optional stripe texture
   - Conveys mechanical weight/danger through darkness

3. **REINFORCED** (Reinforced blocks)
   - RGB: ~180, 180, 180 (medium gray)
   - Metal frame appearance with grid pattern
   - Shader: Standard with metallic properties or grid texture
   - Suggests structural strength and rigidity

4. **BEDROCK** (Foundation)
   - RGB: ~50, 50, 50 (very dark)
   - Immovable, permanent blocks
   - Textured appearance (stone/rock)
   - Conveys permanence and immutability

5. **POROUS** (Water-absorbing blocks)
   - RGB: ~200, 200, 180 (light stone/tan)
   - Permeable to water flow
   - Texture: rough stone surface
   - Distinguishes from solid blocks

6. **WATER** (Water occupancy)
   - Color: Translucent cyan/blue (~RGB 100, 200, 255 with 0.4-0.6 alpha)
   - Renders as a transparent overlay
   - Strong, visible horizontal surface line to show water level
   - Shader: Custom transparent shader or Standard with blend mode

7. **ICE** (Frozen water)
   - RGB: ~200, 240, 255 (bright crystalline cyan)
   - Crystalline appearance with frost effects
   - High specular reflection to suggest ice
   - Distinctly different from liquid water

8. **DRAIN** (Drain holes)
   - RGB: ~255, 165, 0 (orange)
   - Small marker cubes (~30% of voxel size)
   - High contrast to stand out against surrounding blocks
   - Visual indicator of water exit points

9. **WALL** (Level boundaries)
   - RGB: ~128, 128, 128 (neutral gray)
   - Subtle markers to show level extents
   - Can be slightly transparent or use different alpha
   - Non-distracting but clear

### Material Implementation

Materials are defined in the Unity Inspector as prefab assets stored in:
```
Assets/Materials/Prefabs/
  - StandardMaterial.mat
  - HeavyMaterial.mat
  - ReinforcedMaterial.mat
  - BedrockMaterial.mat
  - PorousMaterial.mat
  - WaterMaterial.mat
  - IceMaterial.mat
  - DrainMaterial.mat
  - WallMaterial.mat
  - DefaultMaterial.mat
```

### Material Assignment

The `MaterialPalette` component on the GameLoader (or a dedicated MaterialPalette GameObject) holds references to all materials.

The `VoxelMaterialMapper` static class maps:
- Core.OccupancyType → MaterialPalette.MaterialKey
- Core.MaterialId → MaterialPalette.MaterialKey

The `GridRenderer` uses `VoxelMaterialMapper.GetMaterial()` to look up the correct material for each voxel during rendering.

## Lighting Setup

### Goals

- **Clarity**: Each voxel type remains clearly distinct
- **Readability**: Silhouettes are preserved; no harsh shadows obscure detail
- **Non-distracting**: Lighting supports gameplay without drawing attention
- **Performance**: Minimal light count, no expensive post-processing

### Light Configuration

#### Main Directional Light
- **Type**: Directional (simulates sunlight)
- **Angle**: 45° elevation, 45° azimuth (NE-facing)
- **Intensity**: 0.7–0.8
- **Color**: White
- **Shadows**: Soft shadows at 2048×2048 resolution
- **Purpose**: Primary illumination

#### Ambient Light
- **Type**: Flat ambient (RenderSettings.ambientLight)
- **Intensity**: 0.35
- **Color**: Slightly cool white (RGB 0.9, 0.9, 1.0) for subtle depth cue
- **Purpose**: Fill light to eliminate complete black shadows

#### Fill Light (Optional, Secondary)
- **Type**: Directional
- **Angle**: 40° elevation, 225° azimuth (SW-facing, opposite to main)
- **Intensity**: 0.25
- **Color**: White
- **Shadows**: Off
- **Purpose**: Subtle back-lighting to add definition to edges

### Lighting Code

The `LightingSetup` component encapsulates lighting configuration:

```csharp
public class LightingSetup : MonoBehaviour
{
    // Exposes main light intensity, color, ambient intensity, fill light settings
    // Applies configuration on OnEnable()
    // Can be attached to a empty GameObject in the scene
}
```

To use:
1. Create an empty GameObject named "LightingSetup"
2. Attach the `LightingSetup` component
3. Optionally assign transforms for mainLight and fillLight
4. Adjust settings in Inspector if needed
5. Game will auto-configure lights on load

### Shadow Optimization

- Use soft shadows (hardware PCF) to reduce artifacts
- Resolution: 2048×2048 for balanced quality/performance
- Only the main directional light casts shadows (fill light does not)
- Consider disabling shadows on mobile/low-end hardware

## Scene Setup

### Recommended Scene Structure

```
Scene (MainGame)
├── Camera (CameraManager manages this)
├── GameLoader (Bootstrap, GameLoader script)
│   ├── HUD (Canvas for UI)
│   ├── AudioManager (Audio system)
│   ├── GridRenderer (Renders voxels into this transform)
│   ├── SFXTrigger
│   ├── WindGustFeedback
│   └── MusicController
├── LightingSetup (Empty GameObject with LightingSetup script)
└── [Optionally: Skybox/Background plane - keep non-distracting]
```

### Camera Positioning

- Default isometric view ~45° rotation around grid
- Positioned to show NE-facing wall of structure
- Field of view: ~60° (standard)
- Far clip: >= 100 (accommodate tall structures)

### Background/Skybox

- Keep minimal and non-distracting
- Options:
  - Neutral gray skybox
  - Simple gradient (sky blue to horizon gray)
  - Solid color (dark gray ~RGB 64, 64, 80)
- Avoid busy textures or bright colors that compete with voxels

### Performance Considerations

- GridRenderer creates ~N cube meshes per frame (N = voxel count)
  - This is acceptable for MVP but will need optimization in later milestones
  - Consider mesh instancing or compute shaders for large grids
- Lighting: 2–3 lights is performant on all hardware
- Material count: 9 materials (minimal draw calls)

## Testing Checklist

- [ ] All material palette references assigned in Inspector
- [ ] GridRenderer successfully renders voxels with correct materials
- [ ] Lighting setup applies on scene load
- [ ] Shadows are soft and artifact-free
- [ ] No harsh black shadows (ambient light provides fill)
- [ ] Voxel types are visually distinct
- [ ] Scene runs at stable framerate on target hardware
- [ ] No unintended visual side effects from material properties

## Future Improvements

1. **Custom Shaders**: Replace Standard shaders with custom ones optimized for voxel look
2. **Mesh Optimization**: Combine voxels into single mesh or use GPU instancing
3. **Advanced Textures**: Add striping/patterns to HEAVY, grid to REINFORCED, etc.
4. **Particle Effects**: Add subtle effects for water flow, drain suction, freeze formation
5. **Post-Processing**: Optional subtle effects (bloom for ice, caustics for water) if using URP
6. **LOD**: Reduce detail at distance (though less relevant for small grids)
