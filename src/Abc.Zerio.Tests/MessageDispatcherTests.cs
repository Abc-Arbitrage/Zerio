using System.Collections.Generic;
using System.Linq;
using Abc.Zerio.Dispatch;
using NUnit.Framework;

namespace Abc.Zerio.Tests
{
    [TestFixture]
    public class MessageDispatcherTests
    {
        [Test]
        public void should_dispatch_message_to_handler()
        {
            // Arrange
            var testHandler = new TestHandler<TestMessage>();
            var dispatcher = new MessageDispatcher();
            dispatcher.AddHandler<TestMessage>(testHandler);

            // Act
            dispatcher.Dispatch(0, new TestMessage(42));

            // Assert
            var handledMessage = testHandler.HandledMessages.Single();
            Assert.AreEqual(42, handledMessage.Value);
        }

        [Test]
        public void should_dispatch_multiple_messages_to_handler()
        {
            // Arrange
            var testHandler = new TestHandler<TestMessage>();
            var dispatcher = new MessageDispatcher();
            dispatcher.AddHandler<TestMessage>(testHandler);

            // Act
            dispatcher.Dispatch(0, new TestMessage(42));
            dispatcher.Dispatch(0, new TestMessage(43));

            // Assert
            Assert.AreEqual(2, testHandler.HandledMessages.Count);
            Assert.AreEqual(42, testHandler.HandledMessages[0].Value);
            Assert.AreEqual(43, testHandler.HandledMessages[1].Value);
        }

        [Test]
        public void should_dispatch_to_multiple_handler()
        {
            // Arrange
            var testHandler1 = new TestHandler<TestMessage>();
            var testHandler2 = new TestHandler<TestMessage>();
            var dispatcher = new MessageDispatcher();
            dispatcher.AddHandler<TestMessage>(testHandler1);
            dispatcher.AddHandler<TestMessage>(testHandler2);

            // Act
            dispatcher.Dispatch(0, new TestMessage(42));

            // Assert
            var handledMessage1 = testHandler1.HandledMessages.Single();
            Assert.AreEqual(42, handledMessage1.Value);

            var handledMessage2 = testHandler2.HandledMessages.Single();
            Assert.AreEqual(42, handledMessage2.Value);
        }

        [Test]
        public void should_dispatch_different_messages_to_handlers()
        {
            // Arrange
            var testHandler1 = new TestHandler<TestMessage>();
            var testHandler2 = new TestHandler<OtherTestMessage>();
            var dispatcher = new MessageDispatcher();
            dispatcher.AddHandler<TestMessage>(testHandler1);
            dispatcher.AddHandler<OtherTestMessage>(testHandler2);

            // Act
            dispatcher.Dispatch(0, new TestMessage(42));
            dispatcher.Dispatch(0, new OtherTestMessage(42));

            // Assert
            var handledMessage1 = testHandler1.HandledMessages.Single();
            Assert.AreEqual(42, handledMessage1.Value);

            var handledMessage2 = testHandler2.HandledMessages.Single();
            Assert.AreEqual(42, handledMessage2.Value);
        }

        [Test]
        public void should_not_dispatch_messages_if_the_subscription_doesnt_match()
        {
            // Arrange
            var testHandler = new TestHandler<TestMessage>();
            var dispatcher = new MessageDispatcher();
            dispatcher.AddHandler<TestMessage>(testHandler);

            // Act
            dispatcher.Dispatch(0, new OtherTestMessage(42));

            // Assert
            Assert.AreEqual(0, testHandler.HandledMessages.Count);
        }

        [Test]
        public void should_not_dispatch_message_after_unsubscription()
        {
            // Arrange
            var testHandler = new TestHandler<TestMessage>();
            var dispatcher = new MessageDispatcher();
            dispatcher.AddHandler<TestMessage>(testHandler).Dispose();

            // Act
            dispatcher.Dispatch(0, new TestMessage(42));

            // Assert
            Assert.AreEqual(0, testHandler.HandledMessages.Count);
        }

        [Test]
        public void should_unsubscribe_from_a_specific_handler()
        {
            // Arrange
            var testHandler1 = new TestHandler<TestMessage>();
            var testHandler2 = new TestHandler<TestMessage>();
            var dispatcher = new MessageDispatcher();
            dispatcher.AddHandler<TestMessage>(testHandler1);
            dispatcher.AddHandler<TestMessage>(testHandler2).Dispose();

            // Act
            dispatcher.Dispatch(0, new TestMessage(42));
            dispatcher.Dispatch(0, new TestMessage(43));

            // Assert
            Assert.AreEqual(2, testHandler1.HandledMessages.Count);
            Assert.AreEqual(0, testHandler2.HandledMessages.Count);
        }

        [Test]
        public void should_not_dispatch_message_multiple_times_if_same_handler_is_registered_multiple_times()
        {
            // Arrange
            var testHandler = new TestHandler<TestMessage>();
            var dispatcher = new MessageDispatcher();
            dispatcher.AddHandler<TestMessage>(testHandler);
            dispatcher.AddHandler<TestMessage>(testHandler);
            dispatcher.AddHandler<TestMessage>(testHandler);

            // Act
            dispatcher.Dispatch(0, new TestMessage(42));

            // Assert
            Assert.AreEqual(1, testHandler.HandledMessages.Count);
        }

        private class TestMessage
        {
            public readonly int Value;

            public TestMessage(int value)
            {
                Value = value;
            }
        }

        private class OtherTestMessage
        {
            public readonly int Value;

            public OtherTestMessage(int value)
            {
                Value = value;
            }
        }

        private class TestHandler<T> : MessageHandler<T>
        {
            protected override void Handle(int clientId, T message)
            {
                HandledMessages.Add(message);
            }

            public List<T> HandledMessages { get; } = new List<T>();
        }

    }
}
