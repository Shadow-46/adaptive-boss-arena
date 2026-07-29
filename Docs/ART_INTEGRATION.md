# Art Integration Guide

The game ships as a fully playable placeholder: capsule characters, a procedural arena, procedural
animation, no external art. That is deliberate — it keeps attention on how the game *handles*. This
document is for the later phase where real dark-fantasy art replaces the placeholders.

Four **dormant seams** were built for exactly this. Each is additive and null-safe: with nothing
assigned the game plays identically to today, and each seam is filled independently — you can drop in
an environment without touching characters, or a weapon model without touching abilities. None of
this requires a code change; it is all asset assignment plus, for characters, one Animator Controller.

> Keep the firewall in mind: art is **presentation only**. Nothing here feeds the boss information or
> changes combat timing. The hitboxes stay on the character root and are driven by the attack
> timelines, not by any imported clip. A flashy new sword changes how a swing *looks*, never when it
> *hits*.

---

## The four seams at a glance

| Asset type | Seam | Where it plugs in | Dormant today because |
|---|---|---|---|
| **Characters** | Skeletal animation bridge | `Animator` under the visual root | no rig present → bridge no-ops |
| **Weapons** | Hand mesh socket | `WeaponSocket` on the visual root | weapon `ModelPrefab` is null |
| **Abilities** | Attack cast VFX | `AttackDefinition.CastVfxPrefab` | cast VFX prefab is null |
| **Backgrounds** | Environment prefab | `ArenaConfig.EnvironmentPrefab` | environment prefab is null |

After assigning any art, re-run **`Adaptive Boss Arena/Setup/Run Full Setup`** so the prefabs and
scene regenerate with it. Generators load existing assets rather than overwriting, so hand-tuned
values survive.

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
- Skybox: a stormy/night HDRI from **[Poly Haven](https://polyhaven.com/hdris)** (CC0). Assign under
  *Window ▸ Rendering ▸ Lighting ▸ Environment*.

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
3. Open `Assets/_Project/Prefabs/Player.prefab` (and `Boss.prefab`). Under the **`Visual`** child,
   add the rigged model as a child and set its `Animator.Controller` to your controller. Delete or
   hide the placeholder `Body`/`FacingMarker` capsule.
4. That's it — `CharacterAnimationBridge` finds the `Animator` in its children at Awake and drives it.
   Re-run **Run Full Setup** if you edited via the generators instead of the prefab directly.

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
4. Assign the skybox under *Lighting ▸ Environment*; the reactive `ArenaAtmosphere` still tints the
   ambient/directional light per boss phase on top of it.

---

## After integrating

- Run the headless chain (compile → **Run Full Setup** → EditMode tests). All tests are art-agnostic
  and must stay green; the firewall and asset-integrity validators confirm nothing crossed a boundary.
- Sanity-check in Play mode: attacks still land on the same frames (art never moved a hitbox), the
  boss still adapts, and every seam left unfilled still behaves exactly as before.
