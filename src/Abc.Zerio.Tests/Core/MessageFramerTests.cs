using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Abc.Zerio.Core;
using NUnit.Framework;

namespace Abc.Zerio.Tests.Core
{
    [TestFixture]
    public class MessageFramerTests
    {
        [Test]
        public void should_frame_single_message()
        {
            // Arrange
            var framer = new MessageFramer(1024);
            string framedMessage = null; 
            framer.MessageFramed += message => framedMessage = Encoding.ASCII.GetString(message);

            var expectedMessage = "oui";
            var payLoad = BitConverter.GetBytes(expectedMessage.Length).Concat(Encoding.ASCII.GetBytes(expectedMessage)).ToArray();
            
            // Act
            framer.SubmitBytes(new Span<byte>(payLoad));

            // Assert
            Assert.AreEqual(expectedMessage, framedMessage); 
        }
        
        [Test]
        public void should_frame_single_message_2()
        {
            // Arrange
            var framer = new MessageFramer(1024);
            string framedMessage = null; 
            framer.MessageFramed += message => framedMessage = Encoding.ASCII.GetString(message);
            var expectedMessage = "oui";
            
            // Act
            framer.SubmitBytes(BitConverter.GetBytes(expectedMessage.Length));
            framer.SubmitBytes(Encoding.ASCII.GetBytes(expectedMessage));

            // Assert
            Assert.AreEqual(expectedMessage, framedMessage); 
        }
        
        [Test]
        public void should_frame_single_message_3()
        {
            // Arrange
            var framer = new MessageFramer(1024);
            string framedMessage = null; 
            framer.MessageFramed += message => framedMessage = Encoding.ASCII.GetString(message);
            var expectedMessage = "oui";
            
            // Act
            framer.SubmitBytes(BitConverter.GetBytes(expectedMessage.Length).Take(2).ToArray());
            framer.SubmitBytes(BitConverter.GetBytes(expectedMessage.Length).Skip(2).ToArray());
            framer.SubmitBytes(Encoding.ASCII.GetBytes(expectedMessage));

            // Assert
            Assert.AreEqual(expectedMessage, framedMessage); 
        }
        
        [Test]
        public void should_frame_single_message_4()
        {
            // Arrange
            var framer = new MessageFramer(1024);
            string framedMessage = null; 
            framer.MessageFramed += message => framedMessage = Encoding.ASCII.GetString(message);
            var expectedMessage = "oui";
            
            // Act
            framer.SubmitBytes(BitConverter.GetBytes(expectedMessage.Length).Take(1).ToArray());
            framer.SubmitBytes(BitConverter.GetBytes(expectedMessage.Length).Skip(1).Concat(Encoding.ASCII.GetBytes(expectedMessage)).ToArray());

            // Assert
            Assert.AreEqual(expectedMessage, framedMessage); 
        }
        
        [Test]
        public void should_frame_single_message_5()
        {
            // Arrange
            var framer = new MessageFramer(1024);
            string framedMessage = null; 
            framer.MessageFramed += message => framedMessage = Encoding.ASCII.GetString(message);
            var expectedMessage = "oui";
            
            // Act
            framer.SubmitBytes(BitConverter.GetBytes(expectedMessage.Length).Concat(Encoding.ASCII.GetBytes(expectedMessage).Take(6)).ToArray());
            framer.SubmitBytes(Encoding.ASCII.GetBytes(expectedMessage).Skip(6).ToArray());

            // Assert
            Assert.AreEqual(expectedMessage, framedMessage); 
        }
        
        [Test]
        public void should_frame_multiple_messages()
        {
            // Arrange
            var framer = new MessageFramer(1024);
            var receivedMessages = new List<string>(); 
            framer.MessageFramed += message => receivedMessages.Add(Encoding.ASCII.GetString(message));

            var firstMessage = "oui";
            var secondMessage = "no";
            var payLoad = BitConverter.GetBytes(firstMessage.Length)
                                                .Concat(Encoding.ASCII.GetBytes(firstMessage))
                                                .Concat(BitConverter.GetBytes(secondMessage.Length))
                                                .Concat(Encoding.ASCII.GetBytes(secondMessage))
                                                .ToArray();
            
            // Act
            framer.SubmitBytes(new Span<byte>(payLoad));

            // Assert
            CollectionAssert.AreEquivalent(receivedMessages, new []{ firstMessage, secondMessage}); 
        }
        
        [Test]
        public void should_frame_multiple_messages_2()
        {
            // Arrange
            var framer = new MessageFramer(1024);
            var receivedMessages = new List<string>(); 
            framer.MessageFramed += message => receivedMessages.Add(Encoding.ASCII.GetString(message));

            var firstMessage = "oui";
            var secondMessage = "no";
            var payLoad = BitConverter.GetBytes(firstMessage.Length)
                                                .Concat(Encoding.ASCII.GetBytes(firstMessage))
                                                .Concat(BitConverter.GetBytes(secondMessage.Length))
                                                .Concat(Encoding.ASCII.GetBytes(secondMessage))
                                                .ToArray();
            
            // Act
            framer.SubmitBytes(new Span<byte>(payLoad.Take(2).ToArray()));
            framer.SubmitBytes(new Span<byte>(payLoad.Skip(2).ToArray()));

            // Assert
            CollectionAssert.AreEquivalent(receivedMessages, new []{ firstMessage, secondMessage}); 
        }
        
        [Test]
        public void should_frame_multiple_messages_6()
        {
            // Arrange
            var framer = new MessageFramer(1024);
            var receivedMessages = new List<string>(); 
            framer.MessageFramed += message => receivedMessages.Add(Encoding.ASCII.GetString(message));

            var firstMessage = "oui";
            var secondMessage = "no";
            var payLoad = BitConverter.GetBytes(firstMessage.Length)
                                                .Concat(Encoding.ASCII.GetBytes(firstMessage))
                                                .Concat(BitConverter.GetBytes(secondMessage.Length))
                                                .Concat(Encoding.ASCII.GetBytes(secondMessage))
                                                .ToArray();
            
            // Act
            framer.SubmitBytes(new Span<byte>(payLoad.Take(6).ToArray()));
            framer.SubmitBytes(new Span<byte>(payLoad.Skip(6).ToArray()));

            // Assert
            CollectionAssert.AreEquivalent(receivedMessages, new []{ firstMessage, secondMessage}); 
        }
    }
}
