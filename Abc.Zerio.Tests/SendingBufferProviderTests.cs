using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zerio.Buffers;
using Abc.Zerio.Core;
using Abc.Zerio.Framing;
using Abc.Zerio.Interop;
using NUnit.Framework;

namespace Abc.Zerio.Tests
{
    [TestFixture]
    public unsafe class SendingBufferProviderTests
    {
        private RioBufferManager _bufferManager;
        private RioConfiguration _configuration;

        [SetUp]
        public void SetUp()
        {
            WinSock.EnsureIsInitialized();
            _bufferManager = RioBufferManager.Allocate(64, 64);
            _configuration = new RioConfiguration { BufferAcquisitionTimeout = TimeSpan.FromMilliseconds(10) };
        }

        [TearDown]
        public void Teardown()
        {
            _bufferManager?.Dispose();
        }

        [Test]
        public void should_keep_tracks_of_all_provided_buffers()
        {
            // Arrange
            var bufferProvider = new SendingBufferProvider(_configuration, _bufferManager);

            // Act
            var bufferSegments = new List<BufferSegment>();
            for (var i = 0; i < 10; i++)
            {
                bufferSegments.Add(bufferProvider.GetBufferSegment());
            }

            // Assert
            var rioBuffers = GetProvidedRioBuffers(bufferProvider).ToList();
            Assert.AreEqual(bufferSegments.Count, rioBuffers.Count);

            for (var i = 0; i < bufferSegments.Count; i++)
            {
                var bufferSegment = bufferSegments[i];
                var rioBuffer = rioBuffers[i];

                Assert.AreEqual((long)bufferSegment.Data, (long)rioBuffer.Data);
                Assert.AreEqual((long)bufferSegment.EndOfBuffer, (long)(rioBuffer.Data + rioBuffer.DataLength));
            }
        }

        [Test]
        public void should_reset()
        {
            // Arrange
            var bufferProvider = new SendingBufferProvider(_configuration, _bufferManager);

            // Act
            var bufferSegments = new List<BufferSegment>();
            for (var i = 0; i < 10; i++)
            {
                bufferSegments.Add(bufferProvider.GetBufferSegment());
            }

            // Assert
            bufferProvider.Reset();

            // Assert
            var rioBuffers = GetProvidedRioBuffers(bufferProvider);

            Assert.IsEmpty(rioBuffers);
        }

        [TestCase(42, 0, 42)]
        [TestCase(64, 0, 64)]
        [TestCase(65, 1, 1)]
        [TestCase(100, 1, 36)]
        [TestCase(128, 1, 64)]
        [TestCase(555, 8, 43)]
        public void should_prepare_buffer_length(int messageLength, int expectedLastBufferIndex, int expectedLastBufferLength)
        {
            // Arrange
            var bufferProvider = new SendingBufferProvider(_configuration, _bufferManager);
            var bufferSegments = new List<BufferSegment>();
            for (var i = 0; i < 10; i++)
            {
                bufferSegments.Add(bufferProvider.GetBufferSegment());
            }

            bufferProvider.SetMessageLength(messageLength);

            // Assert
            var rioBuffers = GetProvidedRioBuffers(bufferProvider).ToList();

            var bufferIndex = 0;
            while (bufferIndex < expectedLastBufferIndex)
            {
                var rioBuffer = rioBuffers[bufferIndex];
                Assert.AreEqual(rioBuffer.Length, rioBuffer.DataLength);
                bufferIndex++;
            }

            Assert.AreEqual(expectedLastBufferLength, rioBuffers[expectedLastBufferIndex].DataLength);
        }

        private static IEnumerable<RioBuffer> GetProvidedRioBuffers(SendingBufferProvider sendingBufferProvider)
        {
            foreach (var rioBuffer in sendingBufferProvider)
            {
                yield return rioBuffer;
            }
        }
    }
}
