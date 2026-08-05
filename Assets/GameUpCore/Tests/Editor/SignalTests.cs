using NUnit.Framework;

namespace GameUp.Core.Tests
{
    public class SignalTests
    {
        [Test]
        public void Dispatch_CallsListener()
        {
            var signal = new Signal();
            var count = 0;
            signal.AddListener(() => count++);

            signal.Dispatch();
            signal.Dispatch();

            Assert.AreEqual(2, count);
        }

        [Test]
        public void RemoveListener_StopsCallback()
        {
            var signal = new Signal();
            var count = 0;
            void Handler() => count++;

            signal.AddListener(Handler);
            signal.Dispatch();
            signal.RemoveListener(Handler);
            signal.Dispatch();

            Assert.AreEqual(1, count);
        }

        [Test]
        public void AddOnce_FiresExactlyOnce()
        {
            var signal = new Signal();
            var count = 0;
            signal.AddOnce(() => count++);

            signal.Dispatch();
            signal.Dispatch();

            Assert.AreEqual(1, count);
        }

        [Test]
        public void GenericSignal_PassesArgument()
        {
            var signal = new Signal<int>();
            var received = 0;
            signal.AddListener(value => received = value);

            signal.Dispatch(42);

            Assert.AreEqual(42, received);
        }

        [Test]
        public void TwoArgSignal_PassesBothArguments()
        {
            var signal = new Signal<int, string>();
            var number = 0;
            var text = string.Empty;
            signal.AddListener((n, s) =>
            {
                number = n;
                text = s;
            });

            signal.Dispatch(7, "seven");

            Assert.AreEqual(7, number);
            Assert.AreEqual("seven", text);
        }
    }
}
