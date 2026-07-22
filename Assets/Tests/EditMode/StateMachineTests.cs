using System.Collections.Generic;
using AdaptiveBossArena.Core.StateMachine;
using NUnit.Framework;

namespace AdaptiveBossArena.Tests.EditMode
{
    /// <summary>
    /// Tests for the state machine driving both the boss's behaviour and the player's actions.
    /// </summary>
    /// <remarks>
    /// Priority ordering carries real weight here. A death or stagger transition that loses to a
    /// routine attack transition satisfied on the same frame produces a boss that keeps swinging
    /// after it should have been interrupted, which reads to the player as the game ignoring their
    /// hit.
    /// </remarks>
    [TestFixture]
    public sealed class StateMachineTests
    {
        /// <summary>Mutable context that the test transitions read from.</summary>
        private sealed class TestContext
        {
            public bool ShouldAdvance { get; set; }

            public bool IsDead { get; set; }

            public List<string> Log { get; } = new List<string>();
        }

        /// <summary>State that records its lifecycle callbacks into the shared log.</summary>
        private sealed class RecordingState : StateBase<TestContext>
        {
            private readonly string _name;

            public RecordingState(string name) => _name = name;

            public override string Name => _name;

            public int TickCount { get; private set; }

            protected override void OnEnter(TestContext context) => context.Log.Add($"enter:{_name}");

            protected override void OnTick(TestContext context, float deltaTime) => TickCount++;

            protected override void OnExit(TestContext context) => context.Log.Add($"exit:{_name}");
        }

        [Test]
        public void Construction_EntersTheInitialState()
        {
            var context = new TestContext();
            var idle = new RecordingState("Idle");

            var machine = new StateMachine<TestContext>(context, idle);

            Assert.AreSame(idle, machine.CurrentState);
            CollectionAssert.AreEqual(new[] { "enter:Idle" }, context.Log);
        }

        [Test]
        public void Tick_WithUnsatisfiedTransition_StaysInState()
        {
            var context = new TestContext();
            var idle = new RecordingState("Idle");
            var attack = new RecordingState("Attack");

            var machine = new StateMachine<TestContext>(context, idle);
            machine.AddTransition(idle, attack, ctx => ctx.ShouldAdvance);

            machine.Tick(0.1f);

            Assert.AreSame(idle, machine.CurrentState);
            Assert.AreEqual(1, idle.TickCount);
        }

        [Test]
        public void Tick_WithSatisfiedTransition_ExitsThenEntersInOrder()
        {
            var context = new TestContext { ShouldAdvance = true };
            var idle = new RecordingState("Idle");
            var attack = new RecordingState("Attack");

            var machine = new StateMachine<TestContext>(context, idle);
            machine.AddTransition(idle, attack, ctx => ctx.ShouldAdvance);

            machine.Tick(0.1f);

            Assert.AreSame(attack, machine.CurrentState);
            CollectionAssert.AreEqual(new[] { "enter:Idle", "exit:Idle", "enter:Attack" }, context.Log);

            // The newly entered state receives the tick, not the state that was just left.
            Assert.AreEqual(0, idle.TickCount);
            Assert.AreEqual(1, attack.TickCount);
        }

        [Test]
        public void Tick_WithCompetingTransitions_TakesTheHigherPriority()
        {
            var context = new TestContext { ShouldAdvance = true, IsDead = true };
            var idle = new RecordingState("Idle");
            var attack = new RecordingState("Attack");
            var dead = new RecordingState("Dead");

            var machine = new StateMachine<TestContext>(context, idle);
            machine.AddTransition(idle, attack, ctx => ctx.ShouldAdvance, priority: 0);
            machine.AddTransition(idle, dead, ctx => ctx.IsDead, priority: 100);

            machine.Tick(0.1f);

            Assert.AreSame(dead, machine.CurrentState);
        }

        [Test]
        public void GlobalTransition_FiresFromAnyState()
        {
            var context = new TestContext();
            var idle = new RecordingState("Idle");
            var attack = new RecordingState("Attack");
            var dead = new RecordingState("Dead");

            var machine = new StateMachine<TestContext>(context, idle);
            machine.AddTransition(idle, attack, _ => true);
            machine.AddGlobalTransition(dead, ctx => ctx.IsDead, priority: 100);

            machine.Tick(0.1f);
            Assert.AreSame(attack, machine.CurrentState);

            context.IsDead = true;
            machine.Tick(0.1f);

            Assert.AreSame(dead, machine.CurrentState);
        }

        [Test]
        public void GlobalTransition_DoesNotRetriggerIntoItsOwnState()
        {
            var context = new TestContext { IsDead = true };
            var idle = new RecordingState("Idle");
            var dead = new RecordingState("Dead");

            var machine = new StateMachine<TestContext>(context, idle);
            machine.AddGlobalTransition(dead, ctx => ctx.IsDead);

            machine.Tick(0.1f);
            machine.Tick(0.1f);
            machine.Tick(0.1f);

            // Without the self-transition guard, a permanently satisfied global condition would
            // re-enter its target every frame and restart any entry effects.
            Assert.AreEqual(1, CountOccurrences(context.Log, "enter:Dead"));
        }

        [Test]
        public void AddTransition_ToTheActiveState_TakesEffectImmediately()
        {
            var context = new TestContext { ShouldAdvance = true };
            var idle = new RecordingState("Idle");
            var counter = new RecordingState("Counter");

            var machine = new StateMachine<TestContext>(context, idle);

            // This is what runtime adaptation does: registering a new response while the boss is
            // already sitting in the state the response departs from.
            machine.AddTransition(idle, counter, ctx => ctx.ShouldAdvance);
            machine.Tick(0.1f);

            Assert.AreSame(counter, machine.CurrentState);
        }

        [Test]
        public void ForceState_BypassesTransitionConditions()
        {
            var context = new TestContext();
            var idle = new RecordingState("Idle");
            var dead = new RecordingState("Dead");

            var machine = new StateMachine<TestContext>(context, idle);
            machine.ForceState(dead);

            Assert.AreSame(dead, machine.CurrentState);
            CollectionAssert.AreEqual(new[] { "enter:Idle", "exit:Idle", "enter:Dead" }, context.Log);
        }

        [Test]
        public void StateChanged_ReportsBothEndpoints()
        {
            var context = new TestContext { ShouldAdvance = true };
            var idle = new RecordingState("Idle");
            var attack = new RecordingState("Attack");

            var machine = new StateMachine<TestContext>(context, idle);
            machine.AddTransition(idle, attack, ctx => ctx.ShouldAdvance);

            IState<TestContext> reportedFrom = null;
            IState<TestContext> reportedTo = null;
            machine.StateChanged += (from, to) =>
            {
                reportedFrom = from;
                reportedTo = to;
            };

            machine.Tick(0.1f);

            Assert.AreSame(idle, reportedFrom);
            Assert.AreSame(attack, reportedTo);
        }

        [Test]
        public void TimeInCurrentState_AccumulatesAndResetsOnTransition()
        {
            var context = new TestContext();
            var idle = new RecordingState("Idle");
            var attack = new RecordingState("Attack");

            var machine = new StateMachine<TestContext>(context, idle);
            machine.AddTransition(idle, attack, ctx => ctx.ShouldAdvance);

            machine.Tick(0.25f);
            machine.Tick(0.25f);
            Assert.AreEqual(0.5f, machine.TimeInCurrentState, 0.0001f);

            context.ShouldAdvance = true;
            machine.Tick(0.1f);

            Assert.AreEqual(0.1f, machine.TimeInCurrentState, 0.0001f);
        }

        private static int CountOccurrences(IEnumerable<string> log, string entry)
        {
            int count = 0;
            foreach (string item in log)
            {
                if (item == entry)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
