# Map Collision and Arcade Look Implementation Plan

**Goal:** Add collision to the imported map and enable a vivid arcade grade in the Game scene.

**Architecture:** Add static non-convex mesh colliders to `Exo` descendants that have mesh data and no collider. Activate the existing scene-specific URP profile through a Global Volume and the Main Camera, then tune the profile for vivid color and controlled bloom.

**Tech Stack:** Unity 6.6, URP, Unity Editor scene components.

**Spec:** `docs/superpowers/specs/2026-10-27-map-collision-arcade-look-design.md`

## Global Constraints

- Keep the profile scene-specific and do not change global render-pipeline settings.
- Skip map nodes without a mesh and nodes with an existing collider.
- Keep the placeholder floor collider and all existing colliders.
- Do not change geometry, player movement, lighting design, or add dependencies.

## Review Focus

- Mesh-bearing map objects with an existing collider remain unchanged; verify collider count and representative components.
- The scene profile is actually active for the camera; verify the active Global Volume and camera post-processing setting.
- Bloom remains controlled and does not obscure bright sprites or HUD; inspect a Game View capture.
- Map collision excludes empty transform nodes; verify the added colliders reference mesh data.
- Collider additions do not change unrelated scene objects; compare Exo-only counts and inspect scene validation.

---

### Task 1: Add collision to imported map meshes

**Files:**
- Modify: `Assets/GameJamFoundation/Scenes/Game.unity`

**Interfaces:**
- Consumes: the existing `Exo` map hierarchy and its `MeshFilter.sharedMesh` references.
- Produces: non-convex `MeshCollider` components on eligible `Exo` descendants.

- [x] Find all `Exo` descendants with a non-null mesh and no collider.
- [x] Add non-convex MeshColliders to those objects, leaving existing colliders untouched.
- [x] Save the scene and verify the added collider count and mesh references in Unity.

### Task 2: Enable and tune vivid arcade post-processing

**Files:**
- Modify: `Assets/GameJamFoundation/Scenes/Game.unity`
- Modify: `Assets/GameJamFoundation/Scenes/Game/Global Volume Profile.asset`

**Interfaces:**
- Consumes: the existing scene-specific Volume Profile and Main Camera.
- Produces: an active scene Global Volume and vivid post-processing on the Game camera.

- [x] Add or configure a global Volume that references the existing profile; enable camera post-processing.
- [x] Tune saturation, contrast, Bloom, and subtle vignette for vivid colors without washing out gameplay effects.
- [x] Verify active volume/profile/camera settings and inspect a Game View capture; run scene validation.
