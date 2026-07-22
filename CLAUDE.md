# Adaptive Boss Arena — Working Agreement

A single-encounter action game in Unity 6. The player fights one boss that builds a statistical
profile of their habits and gradually counter-adapts. The goal is a small, extremely polished
vertical slice, not a large game.

## The one rule that outranks the others

**The boss must never cheat.** It may not read player input, buffered input, stamina, cooldowns, or
invincibility state. It reacts only to what a skilled human opponent could perceive by watching the
screen, and only after a human-plausible delay.

This is enforced structurally, not by discipline:

1. `AdaptiveBossArena.AI` and `AdaptiveBossArena.Learning` do not reference
   `AdaptiveBossArena.Player` or `Unity.InputSystem`. The code to cheat cannot compile there.
2. All player state reaches the AI through `IObservablePlayer` / `PlayerObservation`, which expose
   only visible information.
3. `DelayedPerceptionSource` holds every observation back by a perception latency, so the boss acts
   on slightly stale data and feints genuinely work.
4. `ArchitectureValidator` and `ArchitectureFirewallTests` fail the build if the graph is breached.

If a task seems to need player state on the AI side, do not add the assembly reference. Either
expose it through `IObservablePlayer` — after checking a human could actually perceive it — or find
another way.

## Architecture

```
Utilities  (pure algorithms, no dependencies)
    ↑
   Core     (interfaces, data types, services)
    ↑
  Combat
    ↑                 ↑
 Player            Learning ← AI          UI
    ↑                 ↑       ↑           ↑
    └────────────── Game (composition root) ─┘
                        ↑
                     Editor
```

- Assembly references are **not** transitive in Unity. Each assembly lists what it directly uses.
- `Game` is the only assembly that sees everything. It wires systems together at scene start.
- Nothing outside `Game` should reach for `ServiceRegistry.Current` unless Unity's object model
  leaves no seam to inject through.

## Conventions

- XML documentation on every public type and member. Say *why*, not just *what* — the reasoning
  behind a threshold or an ordering is the part that is expensive to recover later.
- Private fields are `_camelCase`; serialized fields are private with a public read-only property.
- No magic numbers. Structural values go in `GameplayConstants`; tunable values go in a
  ScriptableObject so they can be adjusted in play mode.
- Small methods, single responsibility, no duplicated logic.
- Use `regions` sparingly, if at all.
- Prefer `readonly struct` passed by `in` for hot-path data such as `DamageInfo`.
- `init`-only setters and `record` types work thanks to the `IsExternalInit` polyfill in
  `Utilities/Compat`. Do not declare a second copy of that type anywhere.

## Time, randomness and events

- **Never** write `Time.timeScale` outside `TimeService`. Request hit-stop and slow-motion through
  `ITimeService` so effects compose instead of fighting each other.
- **Never** call `UnityEngine.Random`. Inject `IRandomProvider` so probabilistic AI stays
  reproducible and testable.
- Combat logic ticks on `ITimeService.DeltaTime` (freezes during hit-stop). UI, camera shake and the
  pause menu tick on `UnscaledDeltaTime`.
- Use ScriptableObject event channels for cross-system and UI signals. Use direct C# events on
  interfaces for per-frame combat traffic.

## Editor workflow

Unity is not required to author code, but is required to run the game. After opening the project:

Use `Adaptive Boss Arena/Setup/Run Full Setup`. It runs configure → render pipeline → assets →
input actions → prefabs → scene, which is a hard dependency chain.

Two traps worth knowing, both found by running the game rather than by compiling or testing it:

- Unity primitives do not carry colliders that survive non-uniform scaling. A `Cylinder` has a
  **CapsuleCollider**, which collapses to a sphere when scaled flat. Replace it with a box.
- A `Cylinder` is one unit in **diameter**, not radius. Scale by `radius * 2`.

Scenes, prefabs, materials, input bindings and `.asset` files are **generated from code**, never
hand-authored as YAML. If something needs to exist in a scene, add it to `ArenaSceneBuilder`; if it
needs to exist on a character, add it to the relevant prefab builder. Generators load existing
assets rather than overwriting them, so hand-tuned values survive a re-run — delete an asset to
regenerate it.

## Testing

Edit-mode tests live in `Assets/Tests/EditMode` and must not require play mode. The learning system
is pure C# specifically so it can be tested: feed synthetic combat-event streams into the analysers
and assert the boss adapts as intended. Pin `IRandomProvider` with a seed to make probabilistic
selection deterministic.

## Build phases

1. ✅ Architecture — assemblies, core contracts, utilities, editor tooling, tests
2. ✅ Player controller — movement, dash with i-frames, stamina, input buffering
3. ✅ Combat — hitboxes, combos, poise/stagger, perfect dodge, hit-stop, screen shake
4. ✅ Boss AI — FSM, attack selection, telegraph timing, three phases
5. ✅ Learning — combat memory, pattern recognition, counter-strategy selection, adaptation
6. UI — health/stamina, pause, settings, rebinding, save system
7. Polish — post-processing, audio, particles, tuning

Finish a phase, explain what was built and why, then stop for review before starting the next.

## Learning system rules

- The combat event bus delivers immediately, so it may only feed **statistics**. Anything the boss
  does *in response* to the player must come through `IPerceptionSource`, which enforces the delay.
  Wiring a boss behaviour directly to a combat event reintroduces instant reaction.
- Adaptation changes **numbers, never rules**. `BossTuningParameter` values are eased, never
  assigned. The boss cannot gain a capability it did not already have.
- Every `CounterStrategy` must have a `TellMessage`. An adaptation the player cannot perceive is
  indistinguishable from cheating.
- Every behaviour feature carries a confidence from sample count. Never act on a feature value
  without checking its confidence.
