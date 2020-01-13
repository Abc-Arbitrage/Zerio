using System;
using Abc.Zerio.Core;
using Abc.Zerio.Interop;
using NUnit.Framework;

namespace Abc.Zerio.Tests
{
    [TestFixture]
    public unsafe class UnmanagedRioBufferTests
    {
        [SetUp]
        public void SetUp()
        {
            WinSock.EnsureIsInitialized();
        }

        [Test]
        public void should_allocate_unmanaged_buffer()
        {
            // Act
            using var buffer = new UnmanagedRioBuffer<RequestEntry>(5, 1024);
            
            // Assert
            Assert.AreEqual(5, buffer.Length);
            Assert.AreEqual(sizeof(RequestEntry) + 1024, buffer.EntryReservedSpaceSize);
            Assert.AreNotEqual(IntPtr.Zero, (IntPtr)buffer.FirstEntry);
        }

        [Test]
        public void should_allow_access_and_mutation_of_entries()
        {
            // Arrange
            using var buffer = new UnmanagedRioBuffer<RequestEntry>(5, 1024);
            
            // Act
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i]->Type = RequestType.Receive;
                buffer[i]->SessionId = 42;
            }
            
            // Assert
            for (var i = 0; i < buffer.Length; i++)
            {
                Assert.AreEqual(RequestType.Receive, buffer[i]->Type);
                Assert.AreEqual(42, buffer[i]->SessionId);
            }
        }
    }
}
