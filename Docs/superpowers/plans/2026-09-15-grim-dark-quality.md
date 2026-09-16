# Grim Dark Quality Pass Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the prototype look and feel with Mixamo characters and animation, weighty movement and hits, clip-driven knockdowns, grim dark effects, a gothic environment and a styled HUD.

**Architecture:** Licensed art imports through a code-owned `AssetPostprocessor`; a character art table (`CharacterArtSet`) chooses rig, clips and scale per fighter, falling back to the CC0 rigs when the licensed folder is absent. The generated Animator controller is built from that table. Feel work layers on the existing bridge (`CharacterAnimationBridge`), motors and time service without moving gameplay truth out of the hitbox timeline.

**Tech Stack:** Unity 6000.3.20f1, URP 17, C# 9, NUnit EditMode/PlayMode, headless batch verification (`scratchpad/verify.sh`), Windows `-perfShot` captures and Python/Pillow for image evidence.

**Spec:** `Docs/superpowers/specs/2026-09-15-grim-dark-quality-design.md`

**Detail note.** Stage 1 is specified step by step. Stages 2–5 are specified at task level and are expanded into steps at the start of each stage, because their clip names, frame numbers and hooks depend on the inventory and contact sheet Stage 1 produces. Writing those steps now would mean guessing them.

## Global Constraints

- The boss never cheats: `AdaptiveBossArena.AI` and `.Learning` reference neither `.Player` nor `Unity.InputSystem`.
- Everything is generated from code; no hand-authored scenes, prefabs or controllers.
- No `UnityEngine.Random`; no `Time.timeScale` writes outside `TimeService`.
- The hitbox timeline decides when a blow lands; animation only presents it.
- Licensed Mixamo files live under `Assets/_Project/Art/Licensed/Mixamo/` and are git-ignored; without them setup, build and tests still pass on the CC0 rigs.
- Budgets: Windows p95 ≤ 8.3 ms, ≤ 600 draw calls; WebGL ≤ 250 draw calls.
- Every visual change is judged from a Windows build screenshot (and a GIF for motion), not by tests alone.
- XML docs say why; each change is its own commit ending `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.

## Verification commands

- Full chain: `bash scratchpad/verify.sh <tag> setup` → compile, RunFullSetup, EditMode, PlayMode.
- Screenshot: build Windows (`BuildScript.BuildWindows`), run `AdaptiveBossArena.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -perfCapture 8 -perfShot <raw> -perfQuit`, then `python scratchpad/raw2png.py <raw> <png>`.
- Motion: `-perfShots <folder> -perfShotEvery 0.1` (added in Task 3), then `python scratchpad/frames2gif.py <folder> <gif>`.

---

## Stage 1 — Characters and animation

### Task 1: Import the Mixamo art and inventory it

**Files:**
- Create: `Assets/_Project/Scripts/Editor/Art/LicensedArtPostprocessor.cs`
- Create: `Assets/_Project/Scripts/Editor/Art/LicensedArtInventory.cs`
- Test: `Assets/Tests/EditMode/LicensedArtImportTests.cs`

**Interfaces:**
- Produces: `LicensedArtPostprocessor.MixamoFolder` (`"Assets/_Project/Art/Licensed/Mixamo/"`), `LicensedArtInventory.Available` (bool), `LicensedArtInventory.Write(string path)` writing a text report of every clip (file, take name, length, loop, avatar validity) and every character (meshes, materials, embedded textures, humanoid bones found).

- [ ] Write EditMode tests: both characters import as valid Humanoid avatars; every animation file has exactly one clip, Humanoid, with its avatar copied from its character (knight and shared files from Paladin, boss files from Warrok); clips are in place; all skipped with `Assume.That(LicensedArtInventory.Available)` when the folder is absent.
- [ ] Run them; expect failures (Generic import, no avatar source).
- [ ] Implement the postprocessor: characters `Human` + `CreateFromThisModel`, materials via material description, embedded textures extracted beside them; animation files `Human` + `CopyFromOther` with the matching character's avatar, no materials; `OnPreprocessAnimation` bakes root motion into the pose and loops idle, walk, run, strafe and block idle clips; `GetVersion()` = 1.
- [ ] Run tests; expect pass. Write the inventory report; read it.
- [ ] Commit.

### Task 2: A contact sheet to choose clips by eye

**Files:**
- Create: `Assets/_Project/Scripts/Editor/Art/ClipContactSheet.cs` (GPU batch only)
- Create: `scratchpad/contact_sheet.py` (not committed)

**Interfaces:**
- Produces: `ClipContactSheet.Render()`: for every clip, poses its character at 0, 25, 50, 75 and 100 % with `AnimationClip.SampleAnimation`, renders each with a temporary camera into 256×256 raw RGBA files named `<clip>_<n>.raw` in the scratchpad.

- [ ] Implement; exit code 2 when `SystemInfo.graphicsDeviceType == Null`.
- [ ] Run from a GPU batch editor; stitch into one labelled PNG per folder; view them.
- [ ] Choose the clip for every `CharacterAction` of both fighters; record each choice and its reason as comments in Task 4's table.
- [ ] Commit the tool.

### Task 3: Motion capture from the running build

**Files:**
- Modify: `Assets/_Project/Scripts/Utilities/Statistics/PerfCaptureRequest.cs` (`ShotFolder`, `ShotEverySeconds` from `-perfShots`, `-perfShotEvery`)
- Modify: `Assets/_Project/Scripts/Managers/PerfProbe.cs` (numbered raw frames during the capture)
- Test: `Assets/Tests/EditMode/PerfCaptureRequestTests.cs`

- [ ] Test the two flags parse and default to absent; run, fail; implement; pass.
- [ ] Add `scratchpad/frames2gif.py` (Pillow, 10 fps, 640 px wide).
- [ ] Commit.

### Task 4: Character art table and fallback

**Files:**
- Create: `Assets/_Project/Scripts/Combat/Feel/CharacterArtSet.cs` (ScriptableObject; file name matches class)
- Modify: `Assets/_Project/Scripts/Editor/DefaultAssetGenerator.cs` (generate `KnightArt` and `BruteArt`; Mixamo when available, otherwise Quaternius)
- Modify: `Assets/_Project/Scripts/Editor/Art/AnimatorControllerBuilder.cs` (one controller per art set, built from its table)
- Modify: `Assets/_Project/Scripts/Editor/AssetIntegrityValidator.cs` (rule for `CharacterArtSet`)
- Test: `Assets/Tests/EditMode/CharacterArtSetTests.cs`; `RigFacingTests` keeps passing

**Interfaces:**
- Produces: `CharacterArtSet` with `RigPrefab`, `RigScale`, `AnimatorController`, `ClipFor(CharacterAction)`; `enum CharacterAction { Idle, Walk, Run, TurnLeft, TurnRight, Light1, Light2, Light3, Heavy, Special, Block, BlockIdle, HitFront, HitBack, Stun, FallBack, FallForward, GetUp, Death, Roll }`.

- [ ] Tests: every action resolves to a clip for both sets; with the licensed folder absent both point at the Quaternius rig; the knight's clips come from the Knight folder.
- [ ] Implement with the Task 2 choices.
- [ ] Setup; screenshot: both fighters are the new characters, facing each other.
- [ ] Commit.

### Task 5: Materials, weapons and scale

**Files:**
- Modify: `Assets/_Project/Scripts/Editor/SilhouetteBuilder.cs` (URP Lit materials from the extracted diffuse, normal and specular maps; no body override for licensed rigs)
- Modify: `Assets/_Project/Scripts/Editor/PlayerPrefabBuilder.cs`, `BossPrefabBuilder.cs` (no generated blade where the rig carries its own weapon; the boss holds a great cleaver sized to its rig)
- Test: `Assets/Tests/PlayMode/RiggedCharacterTests.cs` (renderers have textured materials; weapon socket under the right hand)

- [ ] Tests; implement; screenshot; commit.

### Task 6: Re-time attacks to the new clips

**Files:**
- Modify: `Assets/_Project/Scripts/Combat/Feel/CharacterAnimationConfig.cs` (per-binding contact fraction)
- Modify: `Assets/_Project/Scripts/Editor/DefaultAssetGenerator.cs` (contact fractions read from the contact sheets)
- Test: `Assets/Tests/EditMode/AttackClipTimeWarpTests.cs` (contact inside Active for every binding)

- [ ] Tests; implement; GIF of each attack; commit.

## Stage 2 — Weight and movement

Status, 2026-09-16: 7 and 8 done, 9 done differently, 10 deliberately skipped.

- **Task 9 as built:** the packs are in-place clips with no start, stop or turn coverage worth wiring, so instead
  the locomotion blend became directional (strafes and back-steps), each clip placed by travel measured from its
  planted foot. That also caught the brute running on a backwards clip. Foot IK is not in.
- **Task 10 skipped:** the boss's slow turn rate is the design's main lever on how easily it is circled, and
  slowing the clips further would fight the attack time-warp. Left alone on purpose.

- **Task 7: Directional hit reactions on an additive layer.** Upper-body additive layer with HitFront and HitBack; `CharacterAnimationBridge.PlayHit(Vector3 worldDirection, float weight)` called from both controllers' damage paths. PlayMode: a landed blow raises the layer weight above 0.5 within 3 frames without changing the base state.
- **Task 8: Heavier impact.** Pure `ImpactWeight.HitStopSeconds(damage, poiseDamage, cap)`; `CameraShaker.PunchDirection(Vector3, float)`; 0.35× slow motion for 0.4 s on a poise break through `ITimeService`. EditMode on the pure rule.
- **Task 9: Grounded locomotion.** Start and stop clips, 180° turns when the desired heading differs by more than 120°, `Combat/Feel/FootPlanting.cs` foot IK while speed is under 0.1. PlayMode: idle for 1 s moves a foot bone under 2 cm.
- **Task 10: Boss cadence.** Heavier turn rate and braking on `BossConfig`, boss clip speed 0.85. Evidence: GIF of approach and swing.

## Stage 3 — Knockdowns and ragdoll

Status, 2026-09-16: 12 done; 11 not possible with the art in hand.

- **Task 11 blocked:** neither pack has a forward fall, so a blow from behind cannot put the fighter on its face.
  It needs another Mixamo download, which is the user's to approve.

- **Task 11: Clip-driven falls and get-ups.** FallBack or FallForward chosen by the blow's direction relative to facing; Getting Up for the rise. PlayMode: a blow from behind plays FallForward.
- **Task 12: Ragdoll from pose and velocity.** `RagdollActivator` gives each body its animated velocity from the last two poses before enabling physics. PlayMode: hips velocity on activation within 30 % of the animated velocity.

## Stage 4 — Effects

Status, 2026-09-16: 13, 14 and 15 done; 16 partly — the brute's core is a sunk ember, heat haze is not in.
Beyond the plan: floor blood that fades, footfall dust, and post-processing made to render at all.

- **Task 13:** Impact effects — dark blood and sparks, stone dust, embers.
- **Task 14:** Textured, fading blade trail.
- **Task 15:** Floor crack decals for slams; faint crack telegraphs.
- **Task 16:** Boss inner glow; heat haze on Windows.

Each: a pure rule where there is logic, a PlayMode test that the effect spawns and pools, screenshot or GIF evidence, commit.

## Stage 5 — Environment and HUD

Status, 2026-09-16: 17, 18 and 19 done — lancet window heads, banners, floor stains and grime, a drained grade,
an iron-framed HUD with the boss named above its gauge, and a restyled title and outcome screen. Ribs and
candelabras are not in; the arches sit above the camera's usual framing, so they read only when it tilts up.

- **Task 17:** Gothic pointed arches and ribs replace box lintels.
- **Task 18:** Clutter, banners, candelabras, grime and puddle decals; darker, warmer grade.
- **Task 19:** Dark fantasy HUD, boss name plate, defeat screen.

Each: generated from code, screenshot evidence, draw calls inside budget, commit.

## Stage close

- **Task 20:** `verify.sh final setup` green; WebGL build (web tier only), browser check, deploy and confirm Pages serves it; Windows release zip, 60 s capture, perf log row, GitHub release, notification.
