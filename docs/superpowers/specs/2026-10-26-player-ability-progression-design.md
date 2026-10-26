# Player Ability Progression and Level-Up Choices

Date: 2026-10-26
Status: Implemented

## Intent

Turn the Dash and Suck prototypes into upgradeable run abilities and present a reusable uGUI choice panel whenever the player gains a level. This makes level-ups a visible gameplay decision and provides an inspector workflow for tuning each level independently.

## User requirements

- Both abilities start at level 0 and cannot be used until upgraded to level 1.
- J activates Dash and K activates Suck after unlock.
- Each ability starts with a 10 second cooldown at level 1; upgrades can reduce the cooldown.
- Dash travels forward over time at an inspector-configurable speed, rather than teleporting.
- Ability stats must be manually configurable for every level.
- Each player level-up opens a centered selection panel and pauses gameplay.
- The panel offers the next upgrade for each eligible ability, with support for up to three options as more abilities are added.
- Choosing an option applies that level and resumes gameplay.

## Design

- Keep ability state and input in the player runtime. Represent each ability's per-level values as serialized level entries (including cooldown, Dash distance and speed, and Suck range and existing cone/pull values). Level 1 is the first entry and unlocks the ability; the entries themselves are editable in the Inspector.
- Treat level 0 as locked. An ability is offered only if it has another configured level. Cooldown starts ready on unlock, then each activation starts that current level's configured cooldown. Gameplay time pauses during selection, so cooldowns pause too.
- Implement Dash as a timed forward movement using the configured speed and distance. Preserve normal player motion while adding the dash movement, and prevent overlapping dashes.
- Add a reusable uGUI level-up selector with a centered panel and up to three generated option buttons. Subscribe it to the existing `PlayerExperience.LeveledUp` event. Process multiple levels gained by one XP reward one choice at a time, keeping gameplay paused between choices.
- Save and restore the prior `Time.timeScale` around the modal so choosing an upgrade does not force a pre-paused game to resume.
- When the Foundation input controller exists, temporarily suppress its pause action for the duration of the choice modal and restore its prior enabled state. Keep `GameFlowController.State` unchanged so its pause snapshot cannot capture the modal's zero time scale.
- Remove the sandbox's direct ability-upgrade buttons; level-up choices become the ability upgrade path. Keep other sandbox cheats intact.

## Approaches considered

- Sandbox IMGUI overlay: fastest to prototype, but the choice flow would be tied to debug tooling and would need replacement for gameplay.
- Reusable uGUI modal: follows existing UI technology in the project and can serve both sandbox and gameplay. Selected by the user.

## Scope boundaries

- Only Dash and Suck are available as options now; the selector supports at most three choices.
- Do not add rerolls, stat upgrades, persistence, or a full run progression screen.
- Keep the existing level-up full heal behavior.
- Do not add or run automated tests unless requested.

## Builder decisions

- If the player gains multiple levels from one XP grant, queue one choice per level and keep the game paused until all are chosen.
- If every ability is capped or has no configured next entry, close the choice panel without blocking the game.
- Use per-level inspector entries instead of a generic stat-upgrade interface; it keeps the current two abilities explicit while still allowing a third option to be added later.
- Leave intentionally empty per-level lists empty during sandbox rebuilds; defaults come from the newly added component's initializer.
