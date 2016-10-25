using System.Text;
using Abc.Zerio.Core;
using NUnit.Framework;

namespace Abc.Zerio.Tests
{
    [TestFixture]
    public class MessageTypeIdTests
    {
        [Test]
        public void should_register_specific_type_id()
        {
            // Arrange
            var messageType = typeof(MessageType1);
            var messageTypeId = new MessageTypeId(42);

            // Act
            MessageTypeId.Register(messageType, messageTypeId);

            // Assert
            Assert.AreEqual(new MessageTypeId(42), MessageTypeId.Get(messageType));
        }

        public class MessageType1
        {
        }

        [Test]
        public void should_use_default_type_id_factory_when_no_specific_registration_occured()
        {
            // Arrange
            var messageType = typeof(MessageType2);

            // Act / Assert
            var expectedMessageTypeId = new MessageTypeId(Crc32.Compute(Encoding.ASCII.GetBytes(messageType.FullName)));
            Assert.AreEqual(expectedMessageTypeId, MessageTypeId.Get(messageType));
        }

        public class MessageType2
        {
        }

        [Test]
        public void should_use_specific_type_id_factory_if_registered()
        {
            // Arrange
            var messageType = typeof(MessageType3);
            MessageTypeId.RegisterFactory(t => new MessageTypeId(42));

            // Act / Assert
            Assert.AreEqual(new MessageTypeId(42), MessageTypeId.Get(messageType));
        }

        public class MessageType3
        {
        }
    }
}
