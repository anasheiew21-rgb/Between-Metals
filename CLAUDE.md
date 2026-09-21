# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Between Metals is a psychological-horror / exploration game (first-person, a maze that changes behind the player, no objective system), built in Unity and meant to be heavily optimized for low-end PCs. The project is currently a prototype; the README and code comments are in Spanish.

- Unity **6000.5.9f1** (Unity 6), URP 17.5. Both `PC_RPAsset`/`PC_Renderer` and `Mobile_RPAsset`/`Mobile_Renderer` exist in `Assets/Settings/`.
- Branching: work happens on `develop`; PRs target `main`.

## Building / running / testing

There is no CLI build, lint, or test setup. Everything goes through the Unity Editor:

- Open the project in Unity 6000.5.9f1 and play `Assets/Scenes/Prototype.unity`.
- `com.unity.test-framework` is installed but there are no tests or `.asmdef` files yet — all scripts compile into the default `Assembly-CSharp`.
- `Assembly-CSharp*.csproj`, `*.slnx`, `Library/`, `Logs/`, `UserSettings/`, `.vs/` and `.vscode/` are generated/ignored; don't edit or commit them. Debugging is done via the VS Code "Attach to Unity" config.

## Architecture

The gameplay code is tiny (`Assets/Scripts/Player/`, `Assets/Scripts/Systems/`), but its correctness depends on the scene hierarchy in `Prototype.unity`, which the scripts assume:

```
Player (empty root)
└── PlayerController   ← PlayerController.cs + CharacterController (the "body")
    └── Main Camera    ← MouseLook.cs + PlayerInteraction.cs (serialized playerCamera ref)
        └── Flashlight ← FlashlightController.cs + Light
```

- `MouseLook` rotates the camera locally on X (pitch) and rotates `transform.parent` on Y (yaw), so the camera **must** be a direct child of the body object that carries `PlayerController`. It also locks/hides the cursor in `Start`.
- `PlayerController` moves via `CharacterController.Move` relative to its own transform, with hand-rolled gravity.
- `FlashlightController` and `PlayerInteraction` use `GetComponent<Light>()` / a serialized `Camera` reference respectively, so they must sit on the objects shown above.
- `PlayerInteraction` is currently a debug stub: on `E` it raycasts from the camera and only logs what it hit (no interactable interface yet).

### Input

All scripts use the **legacy** `UnityEngine.Input` API (`Input.GetAxis`, `Input.GetKey(KeyCode.…)`), hardcoded to WASD / Shift / E / F. `ProjectSettings` has `activeInputHandler: 2` (Both), which is why this works even though `com.unity.inputsystem` 1.20.0 and `Assets/InputSystem_Actions.inputactions` (the unused default template) are present. Keep using the same API in new code unless deliberately migrating to the Input System.

### Assets

- `Assets/Prefabs/{Player,Enemies,Environment}` and `Assets/Models/Environment` hold project-owned content (so far only `CaveRock_01`); `Assets/Animations`, `Audio`, and `UI` are empty placeholders.
- `Assets/EnvironmentPack` (sci-fi corridors) and `Assets/hedge_maze_pack` are third-party asset packs; `Assets/TutorialInfo` and `Assets/Readme.asset` are Unity template leftovers.

## Gotchas

- `ProjectSettings/EditorBuildSettings.asset` still lists `Assets/Scenes/SampleScene.unity`, which no longer exists — `Prototype.unity` is not in the build scenes list.
- Unity `.meta` files are tracked and must be committed together with their asset (and moved together when renaming/relocating assets), otherwise GUID references in scenes/prefabs break.
- Scene/prefab/ProjectSettings files are YAML that Git flags for LF→CRLF conversion on this Windows checkout; expect noisy line-ending warnings and avoid hand-editing these files when the Editor can do it.
- `com.unity.ai.assistant` is a pre-release package and its `ProjectSettings/Packages/com.unity.ai.assistant/` folder is untracked; that plus the `SENTIS_ANALYTICS_ENABLED` scripting define in `ProjectSettings.asset` are Editor-generated side effects, not intentional project changes.
