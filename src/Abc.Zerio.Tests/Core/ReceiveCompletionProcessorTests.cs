using System;
using System.Threading;
using Abc.Zerio.Core;
using Abc.Zerio.Interop;
using Moq;
using NUnit.Framework;

namespace Abc.Zerio.Tests.Core
{
    [TestFixture]
    public class ReceiveCompletionProcessorTests
    {
        private readonly int _sessionId = 42;
        private InternalZerioConfiguration _configuration;
        private TestCompletionQueue _testCompletionQueue;
        private Mock<ISessionManager> _sessionManagerMock;
        private ReceiveCompletionProcessor _processor;
        private Mock<ISession> _sessionMock;

        [SetUp]
        public void SetUp()
        {
            _configuration = new InternalZerioConfiguration
            {
                MaxReceiveCompletionResults = 5,
            };
            
            _testCompletionQueue = new TestCompletionQueue();
            _sessionManagerMock = new Mock<ISessionManager>();
       
            _sessionMock = new Mock<ISession>();
            var session = _sessionMock.Object;
            _sessionManagerMock.Setup(x => x.TryGetSession(_sessionId, out session)).Returns(true);
            
            _processor = new ReceiveCompletionProcessor(_configuration, _testCompletionQueue, _sessionManagerMock.Object);
        }

        [Test]
        public void should_process_receive_completions()
        {
            try
            {
                // Arrange
                _processor.Start();
                
                var byteReceivedSignal = new AutoResetEvent(false);
                var requestReceiveSignal = new AutoResetEvent(false);
                _sessionMock.Setup(x => x.OnBytesReceived(12, 8)).Callback(delegate { byteReceivedSignal.Set();  });
                _sessionMock.Setup(x => x.RequestReceive(12)).Callback(delegate { requestReceiveSignal.Set();  });
                
                // Act
                var results = new []
                {
                    new RIO_RESULT
                    {
                        BytesTransferred = 8,
                        ConnectionCorrelation = _sessionId,
                        RequestCorrelation = 12
                    }
                };
                
                _testCompletionQueue.AvailableResults.Enqueue(results);
                
                // Assert
                Assert.IsTrue(byteReceivedSignal.WaitOne(TimeSpan.FromSeconds(100)));
                Assert.IsTrue(requestReceiveSignal.WaitOne(TimeSpan.FromSeconds(100)));
            }
            finally
            {
                _processor.Stop();
            }
        }
    }
}
