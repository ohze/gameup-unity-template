using NUnit.Framework;
using GameUp.Core;

namespace GameUp.Core.Tests
{
    struct TestEvent : IEvent
    {
        public int Value;
    }

    struct AnotherEvent : IEvent
    {
        public string Message;
    }

    public class EventBusTests
    {
        [TearDown]
        public void TearDown()
        {
            EventBus<TestEvent>.Clear();
            EventBus<AnotherEvent>.Clear();
        }

        [Test]
        public void Subscribe_And_Publish_ReceivesEvent()
        {
            int received = 0;
            using var binding = EventBus<TestEvent>.Subscribe(e => received = e.Value);
            EventBus<TestEvent>.Publish(new TestEvent { Value = 42 });
            Assert.AreEqual(42, received);
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            int count = 0;
            var binding = EventBus<TestEvent>.Subscribe(_ => count++);
            EventBus<TestEvent>.Publish(new TestEvent());
            Assert.AreEqual(1, count);

            binding.Dispose();
            EventBus<TestEvent>.Publish(new TestEvent());
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Multiple_Subscribers_AllReceive()
        {
            int a = 0, b = 0;
            using var ba = EventBus<TestEvent>.Subscribe(e => a = e.Value);
            using var bb = EventBus<TestEvent>.Subscribe(e => b = e.Value);

            EventBus<TestEvent>.Publish(new TestEvent { Value = 10 });
            Assert.AreEqual(10, a);
            Assert.AreEqual(10, b);
        }

        [Test]
        public void Different_EventTypes_AreIsolated()
        {
            int testVal = 0;
            string anotherVal = null;

            using var b1 = EventBus<TestEvent>.Subscribe(e => testVal = e.Value);
            using var b2 = EventBus<AnotherEvent>.Subscribe(e => anotherVal = e.Message);

            EventBus<TestEvent>.Publish(new TestEvent { Value = 5 });
            Assert.AreEqual(5, testVal);
            Assert.IsNull(anotherVal);

            EventBus<AnotherEvent>.Publish(new AnotherEvent { Message = "hello" });
            Assert.AreEqual("hello", anotherVal);
        }

        [Test]
        public void Clear_RemovesAllSubscribers()
        {
            int count = 0;
            EventBus<TestEvent>.Subscribe(_ => count++);
            EventBus<TestEvent>.Subscribe(_ => count++);

            EventBus<TestEvent>.Clear();
            EventBus<TestEvent>.Publish(new TestEvent());

            Assert.AreEqual(0, count);
        }
    }
}
