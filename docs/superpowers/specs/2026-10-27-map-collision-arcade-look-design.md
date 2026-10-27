# Map Collision and Arcade Look Design

## Intent

Make the imported `Exo` map physically solid and give the Game scene a vivid arcade presentation while preserving gameplay and effect readability.

## Scope

- Add non-convex `MeshCollider` components to `Exo` descendants that have a non-null `MeshFilter.sharedMesh` and no existing collider. Keep existing colliders and the placeholder floor collider.
- Make the scene's URP post-processing active and tune a vivid, saturated look with restrained bloom and subtle vignette. Keep the profile scene-specific so other scenes are unchanged.
- No changes to map geometry, player movement, lighting design, or global render-pipeline settings.

## Approaches

- **Per-mesh static collision (selected):** add colliders only to mesh-bearing map objects. This follows the imported map structure and avoids colliding empty transform nodes; it trades a larger collider count for accurate map coverage.
- **Single simplified boundary collider:** cheaper at runtime, but would require authoring a separate collision model and could miss interior obstacles.
- **Leave collision to the floor:** cheapest, but does not satisfy collision with the map.

For the look, use a vivid arcade grade with stronger color, moderate contrast, controlled bloom, and only a light vignette. This keeps colorful assets lively while maintaining clear silhouettes and HUD readability.

## Constraints and implementation calls

- The active scene contains an imported `Exo` root with 396 descendants. At inspection time, only the placeholder floor had a collider.
- Use Unity's existing URP Volume Profile and component types; do not add dependencies or a custom renderer feature.
- Configure colliders as static, non-convex mesh colliders; skip nodes without a mesh and nodes that already have a collider.
- Verify the active scene has a Global Volume, the camera renders post-processing, and collision coverage exists after the change.

## Applied settings

- Added non-convex MeshColliders to all 395 mesh-bearing `Exo` descendants; no existing colliders were found under `Exo`.
- Enabled the scene's Global Volume and Main Camera post-processing.
- Set Bloom threshold to 1.1 and intensity to 0.45, Color Adjustments contrast to 12 and saturation to 22, and Vignette intensity to 0.12 with smoothness 0.55.
