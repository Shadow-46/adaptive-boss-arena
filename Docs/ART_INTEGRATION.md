# Art Integration Guide

Everything you see is generated from code: a jointed knight and a horned brute assembled from
primitives, blade models built to each weapon's own proportions, a pillared arena with braziers, and
a procedural sky. No external art is shipped. This document is for replacing any of it with real
downloaded assets.

> **Licensing.** Only use art you have the right to ship. This repository is public with a live
> public demo, so anything from a commercial game, film or anime — a recognisable character, a
> trademarked weapon — is out, regardless of where a copy can be found. Every source listed below is
> free and licensed for this use. Everything currently in the game is original and generated, which
> is why it can be published without a second thought.

Five **seams** exist for dropping art in. Each is additive and null-safe: with nothing assigned the
game plays and looks exactly as it does now, and each is filled independently — an environment
without touching characters, a weapon model without touching abilities, one sound without touching
the rest of the bank. None of it requires a code change.

> Keep the firewall in mind: art is **presentation only**. Nothing here feeds the boss information or
> changes combat timing. The hitboxes stay on the character root and are driven by the attack
> timelines, not by any imported clip. A flashy new sword changes how a swing *looks*, never when it
> *hits*.

---

## The seams at a glance

| Asset type | Where it plugs in | State today |
|---|---|---|
| **Characters** | `CharacterAnimationConfig.RigPrefab`, plus an Animator Controller on the rig | empty → the generated body is built |
| **Weapons** | `WeaponDefinition.ModelPrefab` | **filled** by the generated blade models |
| **Abilities** | `AttackDefinition.CastVfxPrefab` | empty → no cast effect |
| **Backgrounds** | `ArenaConfig.EnvironmentPrefab` | empty → the generated arena is built |
| **Sound** | `AudioService` ▸ Cue Overrides | empty → the synthesised bank is used |

After assigning any art, re-run **`Adaptive Boss Arena/Setup/Run Full Setup`** so the prefabs and
scene regenerate with it.

> **Assign art through an asset — never by editing a prefab.**
> `PlayerPrefabBuilder` and `BossPrefabBuilder` call `SaveAsPrefabAsset`, which **replaces the entire
> prefab**. A model dragged into `Player.prefab` by hand is destroyed the next time setup runs, with
> no warning. Earlier versions of this guide told you to do exactly that; the `RigPrefab` field
> exists so the builder puts your model back on every run instead.
>
> The weapon, ability, environment and sound seams are all fields on `.asset` files, which the
> generator never overwrites — those are safe to assign directly.

---

## Dark-fantasy shopping list (all free)

Sourcing decision on record: **free ready-made packs**, **skeletal animation**, **dark-fantasy**.

### Characters — [Mixamo](https://www.mixamo.com) (free, Adobe account)
- **Player**: a lithe, agile humanoid — e.g. *"Paladin J Nordstrom"* or *"Knight"* stripped to a
  hooded swordsman silhouette. Download **FBX for Unity**, **with skin**.
- **Boss**: a larger, heavier silhouette — e.g. *"Mutant"*, *"Warrok"*, or a big armoured knight — so
  its mass reads at a glance against the player.
- **Animation set** (download each as *In Place*, FBX for Unity, then retarget to both rigs):
  `Idle`, `Walking`/`Running` (for the locomotion blend), `Standing Melee Attack Downward` +
  `Slash`/`Great Sword Slash` (light/heavy), `Cast` or `Magic Attack` (ability), `Dodge`/`Roll`
  (dash), `Blocking`/`Bracing` (guard), `Standing React` (hit), `Dying`/`Death From Front` (death).
  These map one-to-one onto the Animator triggers below.

### Backgrounds — CC0 environment + skybox
- Environment: a ruined cathedral, crypt, or blasted courtyard from **[Poly Pizza](https://poly.pizza)**
  (CC0), **[Kenney](https://kenney.nl/assets)** (CC0), or **[Quaternius](https://quaternius.com)** (CC0).
  Model it around a circular fighting floor of radius **~16 units** (the current `ArenaConfig.Radius`).
- Skybox: a stormy/night HDRI from **[Poly Haven](https://polyhaven.com/hdris)** (CC0), replacing the
  generated `ArenaSky.mat`. See Seam 4.

### Sound — CC0 audio
- **[Freesound](https://freesound.org)** (filter to CC0) or **[Kenney](https://kenney.nl/assets)**
  (CC0) for impacts, swings and a music bed. Replace cues one at a time; see Seam 5.

### Weapons — CC0 weapon pack
- A greatsword, an arming sword, and a glowing/rune blade from Kenney or Quaternius (CC0), matching
  the three weapons (`WeaponGreatsword`, `WeaponBlade`, `WeaponEnergyBlade`).

---

## Seam 1 — Characters (skeletal animation)

**Code:** `Combat/Feel/CharacterAnimationBridge.cs`, driven by `Combat/Feel/AnimatorDriveMap.cs`
(pure, unit-tested). The bridge sits on the visual root beside `CharacterAnimator` and receives the
same state stream. When no `Animator` is present it does nothing; when one is present it drives it,
and `CharacterAnimator` automatically stands its full-body procedural pose down to leave only additive
impact recoil, so the two never fight.

### The Animator parameter contract (author these on your Animator Controller)

| Parameter | Type | Meaning |
|---|---|---|
| `Speed` | float | Planar speed as a fraction of top speed (0–1). Drive a locomotion blend tree with it. |
| `Grounded` | bool | Always `true` in this arena (no jumping). Present so a standard locomotion controller works unmodified. |
| `Attack` | trigger | A light-chain swing began (re-fires on each chained hit). |
| `Heavy` | trigger | A heavy swing began. |
| `Ability` | trigger | A special / ability cast began. |
| `Dash` | trigger | A dash began. |
| `Guard` | trigger | A guard was raised. |
| `Hit` | trigger | The character took a hit (blocked or not). |
| `Death` | trigger | The character died. |

Missing parameters are harmless — Unity skips a `SetTrigger`/`SetFloat` for a name the controller
does not define — so you can wire the controller up incrementally.

### Steps
1. Import the Mixamo FBX; set **Rig ▸ Animation Type = Humanoid**, **Avatar = Create From This Model**.
2. Build an **Animator Controller** exposing the parameters above; put `Idle`/locomotion in the base
   state driven by `Speed`, and each trigger transitioning to its clip and back.
3. Save the rigged model — with its `Animator.Controller` already set — as its **own prefab**.
4. Assign that prefab to `DefaultPlayerAnimation.asset` (or `DefaultBossAnimation.asset`) ▸ **Rig
   Prefab**.
5. Run **Run Full Setup**. The builder instantiates your rig under `Visual` and skips the generated
   body entirely, and it does so again on every future run.
6. That's it — `CharacterAnimationBridge` finds the `Animator` in its children at Awake and drives
   it, and `CharacterAnimator` stands down to additive recoil only so the two never fight over the
   pose.

> **Do not add the model to `Player.prefab` directly.** The builders replace the whole prefab on
> every run, so it would be destroyed without warning. That is what the **Rig Prefab** field is for.
> Colliders on the rig are stripped automatically — a downloaded character usually ships with some,
> and one inside the character controller would fight it.

---

## Seam 2 — Weapons (hand mesh)

**Code:** `Combat/Feel/WeaponSocket.cs` (a `WeaponSocket` GameObject already exists under each visual
root), field `WeaponDefinition.ModelPrefab`. `PlayerController.EquipWeapon` calls
`socket.Equip(weapon.ModelPrefab)` on every draw and swap — null clears it (today's behaviour), a
prefab instantiates it in hand.

### Steps
1. Make each weapon model a prefab (pivot at the grip, blade pointing +Z).
2. Assign it to the weapon asset's **Model Prefab** field:
   - `Assets/_Project/ScriptableObjects/Weapons/WeaponGreatsword.asset`
   - `Assets/_Project/ScriptableObjects/Weapons/WeaponBlade.asset`
   - `Assets/_Project/ScriptableObjects/Weapons/WeaponEnergyBlade.asset`
3. Play. The correct model now appears on draw and swaps on weapon change.
4. Once a rig exists, re-parent the `WeaponSocket` transform to the model's right-hand bone and clear
   its local offset — no code changes; the controller only asks the socket to swap, wherever it lives.

> The boss's weapon comes as part of its imported model (bosses do not swap weapons), so there is no
> boss weapon socket to fill.

---

## Seam 3 — Abilities (cast VFX)

**Code:** `AttackDefinition.CastVfxPrefab`, spawned by `Combat/AttackExecutor.Begin` at the attacker,
parented so it rides the lunge, and auto-destroyed after a safety lifetime. Shared by player and boss
because both resolve attacks through the same executor.

### Steps
1. Build a particle-system prefab per ability (rune flare, shockwave, ember burst…). Let it stop and
   clean itself; the executor's 4-second destroy is only a safety net.
2. Assign it to the attack asset's **Cast Vfx Prefab** field. Good candidates:
   - `Assets/_Project/ScriptableObjects/Attacks/PlayerSpecial.asset`
   - `Assets/_Project/ScriptableObjects/Attacks/EnergySpecial.asset`
   - `Assets/_Project/ScriptableObjects/Attacks/GreatswordSpecial.asset`
   - Boss casts: `BossShockwave.asset`, `BossSlam.asset`, `BossCharge.asset`.
3. Play. The effect now spawns the instant that attack begins.

---

## Seam 4 — Backgrounds (environment)

**Code:** `ArenaConfig.EnvironmentPrefab` + `ArenaConfig.HideProceduralMeshesWithEnvironment`,
instantiated by `Editor/ArenaSceneBuilder.BuildEnvironment` under the arena root. The procedural
floor and wall **colliders stay live** and keep bounding the fight; only their meshes are hidden when
an environment is supplied (toggle off to see both while aligning).

### Steps
1. Assemble your environment as a single prefab, centred at the origin, sized so its walkable floor
   matches `ArenaConfig.Radius` (~16). Keep it **renderers only** — the placeholder colliders are the
   real boundary.
2. Assign it to `Assets/_Project/ScriptableObjects/Config/DefaultArenaConfig.asset` ▸ **Environment
   Prefab**. Leave **Hide Procedural Meshes With Environment** on to replace the placeholder look; turn
   it off temporarily to check your floor lines up with the collision disc.
3. Run **Run Full Setup** to rebuild the scene with the environment dressed in.
4. To replace the sky, assign your HDRI to a skybox material and set it on
   `RenderSettings.skybox` (the generator writes one there, so the simplest route is to replace the
   generated `ArenaSky.mat`). The camera already clears to the skybox. Ambient light deliberately
   stays on the three-band Trilight that `ArenaAtmosphere` drives per boss phase rather than coming
   from the sky, so the phase mood keeps working; switch `RenderSettings.ambientMode` to `Skybox`
   only if you would rather give that up.

---

## Seam 5 — Sound (recorded clips)

**Code:** `Managers/AudioService.cs` ▸ **Cue Overrides**. Every sound is synthesised at startup by
`Utilities/Audio/ToneGenerator.cs`, which ships nothing and costs nothing but will never beat a
recorded sound. An override replaces exactly one cue and leaves every caller untouched.

### Steps
1. Import the clip. **Decompress On Load** for short one-shots, **Streaming** for music.
2. On the `--- Managers ---` object in the arena scene, find **Audio Service** ▸ **Cue Overrides**,
   add an entry, and set **Cue Id** to one of the identifiers in `AudioService.Cues`
   (`hit.heavy`, `guard.deflect`, `boss.roar`, `player.death`, …) with your clip beside it.
3. Press play. A cue id that matches nothing logs a warning rather than failing silently, because a
   mistyped identifier would otherwise be a sound that simply never plays.

Note the mix is separate from the clips: `AudioService.CueGains` decides how loud each cue sits
relative to the others, so a replacement clip that is too loud or too quiet is adjusted there rather
than by re-exporting the file.

---

## After integrating

- Run the headless chain (compile → **Run Full Setup** → EditMode tests → **PlayMode tests**). All
  tests are art-agnostic and must stay green; the firewall and asset-integrity validators confirm
  nothing crossed a boundary, and the play-mode suite confirms the scene still assembles with real
  art in it — which is the check that actually catches a broken drop-in.
- Sanity-check in Play mode: attacks still land on the same frames (art never moved a hitbox), the
  boss still adapts, and every seam left unfilled still behaves exactly as before.
- Watch for **colliders arriving with downloaded art**. Rigs are stripped automatically, but an
  environment or weapon prefab is not — a stray collider on the arena floor or inside the character
  controller will change the fight. The play-mode suite asserts the generated content is clean; it
  cannot vouch for yours.
