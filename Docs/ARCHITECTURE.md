# Architecture

## The problem this structure solves

An adaptive boss has one failure mode that ruins it: feeling like a cheat. If the player suspects
the boss is reading their controller, every clever counter becomes an insult rather than a
challenge, and the central mechanic collapses.

Suspicion is easy to earn and hard to shake. A boss that dodges the instant the attack button goes
down feels wrong even if it did so by coincidence. So the design does not merely avoid cheating —
it makes cheating **structurally impossible**, and then adds enough latency and fallibility that
the boss's behaviour is legibly human.

## Three layers of defence

### 1. The assembly graph

```
Utilities  ── pure algorithms, no dependencies
    ↑
   Core     ── interfaces, data types, services
    ↑
  Combat
    ↑                    ↑
 Player               Learning ←── AI            UI
(input lives           (no reference to Player, no reference to InputSystem)
 only here)
    ↑                    ↑         ↑              ↑
    └──────────────── Game (composition root) ────┘
                          ↑
                       Editor
```

`AdaptiveBossArena.AI` and `AdaptiveBossArena.Learning` do not reference
`AdaptiveBossArena.Player` or `Unity.InputSystem`. Those types are not in scope in those
assemblies. Code that reads the player's input from the boss's decision-making **does not compile**.

Unity assembly references are not transitive, so this cannot be circumvented by an intermediate
hop, and `ArchitectureValidator` follows the graph transitively anyway in case someone tries.

### 2. The observation interface

The only channel through which player state reaches the AI is `IObservablePlayer`, which produces a
`PlayerObservation`. It carries position, velocity, facing, the visually distinct action being
performed, how long that action has been running, and normalised health — all things visible on
screen.

What it deliberately omits, and why:

| Omitted | Reason |
|---------|--------|
| Input vector, buffered input, held buttons | Reading intent before it becomes motion is the definition of the cheat |
| Stamina | An opponent cannot see a number; exhaustion must be inferred from a dash that did not come |
| Cooldown timers | The boss learns ability rhythm from observed frequency, as a human would |
| Invincibility flag | The boss discovers a dash beat its attack by whiffing — and that whiff feeds the learning system |

Adding a field here is a design decision, not a refactor. The test is always: *could a skilled human
opponent perceive this by watching?*

### 3. Perception latency and fallibility

`DelayedPerceptionSource` records observations into a fixed-capacity ring buffer and serves them
back delayed by a configurable latency (default ~140 ms). The boss is always reasoning about what
the player was doing a moment ago.

Two things follow from this, both desirable:

- **Feints work.** A commitment the boss makes during the latency window cannot be taken back, so
  baiting is a real tactic rather than a thing the player imagines is happening.
- **Reaction times are human.** `ReactionProfile` clamps decisions to a 220–420 ms band with
  variance, and includes a `MissedOpportunityChance` so the boss sometimes simply fails to punish an
  opening. Later phases tighten toward the fast end of that band but never below it.

Sampling is pushed by the composition root rather than pulled by the AI. The boss cannot request a
fresher reading, because it never holds a reference to the player at all.

## How learning will work (Phase 5)

```
combat events ──▶ CombatMemory ──▶ PatternRecognizer ──▶ BehaviourProfile
                 (ring buffers)    (EWMA + histograms)   (feature + confidence)
                                                                │
                                                                ▼
ThreatEvaluator ──────────────────────────────────────▶ CounterSelector
(situation read)                                        (weighted, seeded random)
                                                                │
                                                                ▼
                                                       AdaptationManager
                                              (gradual lerps, cooldowns, phase gating)
                                                                │
                                                                ▼
                                                          BossTuning
                                                   (numbers the FSM reads)
```

Design properties this shape enforces:

- **Adaptation is slow and forgetful.** Features are exponentially weighted moving averages with
  half-lives. A habit the player abandons decays, which is what makes switching tactics a real
  answer to being countered rather than a permanent penalty.
- **Confidence gates action.** Every feature carries a confidence derived from sample count. A habit
  seen twice nudges the boss; a habit seen thirty times drives it. The boss cannot react to a
  coincidence.
- **Selection is probabilistic.** If the strongest habit always produced the same counter, the
  player would learn the lookup table and the fight would become memorisation — the exact failure
  the design exists to avoid. Selection is weighted-random from a seeded `IRandomProvider`, which
  also keeps it testable.
- **Adaptation changes numbers, never rules.** `BossTuningParameter` covers things like attack
  delay, preferred range, parry chance and dodge-prediction strength. The boss cannot gain a
  capability it did not already have, only lean harder on one. Everything it does in phase three was
  visible, in weaker form, in phase one.
- **Every adaptation announces itself.** `CounterStrategy.TellMessage` is required, not decorative.
  An adaptation the player cannot perceive is indistinguishable from cheating.

## Key decisions and their reasons

**Attacks are timed phases, not animation events.** `AttackDefinition` declares startup, active and
recovery in seconds; `IAttackTimeline` raises the callbacks an animation event would. This works
with primitive placeholder art, keeps frame data testable in edit mode, and survives clip
re-imports. Swapping to animation-driven timing later replaces the driver and nothing else.

**One owner of time.** Hit-stop, the perfect-dodge slow-motion reward and the pause menu all want to
control time. Letting each write `Time.timeScale` produces the classic bug where unpausing restores
the wrong speed. Everything routes through `ITimeService`, and combat ticks on scaled time while UI
ticks on unscaled.

**Scenes are generated, not authored.** Unity scene files merge badly and are unreviewable in a
diff. `ArenaSceneBuilder` builds the arena from `ArenaConfig`, so changing the radius and rebuilding
is the whole workflow, and a fresh clone reproduces the scene exactly.

**A registry, not a DI container.** The project has about a dozen long-lived services. Constructor
and inspector injection cover nearly everything; `ServiceRegistry` exists for the cases where Unity
instantiates an object and leaves no seam to inject through. It is the single global access point
in the project, and intentionally the only one.

**State machines hold their transitions.** Transitions are objects registered with the machine, not
branches inside states. That is what lets a counter-strategy add a parry response at runtime without
the existing attack states knowing anything changed.
