# Grim dark quality pass — design

**Goal.** Replace the look and feel of a game-jam prototype with that of a grim dark fantasy action game:
heavy plate armour against a hulking brute, deliberate weighty combat, ruined gothic stone. Windows is
the quality bar; the WebGL demo gets the same characters, animation and feel with lighter effects.

**Approved approach.** Character-first, then feel. Real characters and their animation decide timing and
silhouette, so everything tuned before them would be redone.

## 1. Characters and animation pipeline

- **Source (Mixamo, downloaded by the user).** Knight: *Paladin J Nordstrom* with the *Sword and Shield*
  pack. Boss: *Warrok W Kurniawan* with the *Great Sword* pack (a brute with a massive two-handed blade).
  Shared: *Stunned*, *Getting Up*, *Falling Back Death*, *Flying Back Death*. The *Mutant Pack* was not
  downloaded; it can replace the boss's great sword set later without code changes.
- **Location.** `Assets/_Project/Art/Licensed/Mixamo/{Characters,Animations/Knight,Animations/Boss,Animations/Shared}`,
  git-ignored. Mixamo's terms allow the art in games and their builds, not redistribution of the files.
  How to fetch it is in `Docs/ART_INTEGRATION.md`.
- **Import in code.** An `AssetPostprocessor` for that folder: Humanoid rig; each animation file copies
  its avatar from its character; clips in place (root motion baked into the pose); loop flags from the
  clip's name; materials extracted so the generator can rebuild them for URP.
- **Clip selection is data.** A table maps each game action to a named file. Numbered duplicates
  (`slash (2)`) are chosen by viewing a contact sheet of rendered poses, not by guessing.
- **Fallback.** If the Mixamo folder is absent, the generators use the existing CC0 Quaternius rigs.
  A clone of the public repo still sets up, builds and passes its tests.
- **Controller.** The generated Animator controller gains per-character clip sets: locomotion with
  start and stop, turn in place, strafing while locked on, a combo string per light and heavy attack,
  block, parry, directional hit, stun, fall (front and back), get up, death.

## 2. Weight and movement

- **Hit reactions.** A directional hit clip plays on an additive upper-body layer, so a landed blow
  visibly moves the body without interrupting an unbroken stance. Hit-stop scales with the blow's weight;
  the camera punches along the hit direction; staggers and kills get a short slow motion.
- **Grounded locomotion.** Speed from the motor drives a blend with real acceleration and stop clips;
  a sharp change of direction plays a turn instead of rotating the body on the spot; while standing the
  feet are held planted with foot IK, so a character never skates. The boss moves at a heavier cadence.
- **Attacks.** Timing is authored from each clip's own wind-up, contact and recovery. The hitbox window
  stays the gameplay truth, and the clip warp maps each new clip's contact frame onto it.

## 3. Knockdowns and ragdoll

- Launches, falls and get-ups use purpose-made clips. The fall is picked by the blow's direction relative
  to the body.
- Death starts the ragdoll from the current pose and velocity instead of snapping, then lets it settle.
- Get-up waits until the body has actually come to rest.

## 4. Effects

- Impacts: dark blood and sparks on flesh and armour, stone dust on the floor, embers on heavy blows.
- A textured, fading blade trail.
- Slams crack the floor with decals instead of flat discs; ground warnings become faint glowing cracks.
- The boss's glow comes from within its chest; heat haze on the Windows tier.

## 5. Environment and HUD

- Gothic pointed arches and ribs instead of box lintels; rubble clusters, banners, candelabras, grime and
  puddle decals; a darker, warmer colour grade.
- A dark fantasy HUD: framed health and stamina, a boss name plate, a styled defeat screen.

## 6. Verification

Each stage ends with evidence judged by eye as well as by tests:
- Before/after screenshots from the Windows build, from the same fixed moment.
- A captured frame sequence stitched into a GIF, so motion is judged in motion.
- The performance scenario against the budgets: Windows p95 ≤ 8.3 ms and ≤ 600 draw calls; WebGL ≤ 250.
- All EditMode and PlayMode tests green; the architecture firewall intact.

## Rules that do not change

The boss never cheats. Everything is generated from code. No `UnityEngine.Random`; no `Time.timeScale`
writes outside `TimeService`. The hitbox timeline, not the animation, decides when a blow lands.
