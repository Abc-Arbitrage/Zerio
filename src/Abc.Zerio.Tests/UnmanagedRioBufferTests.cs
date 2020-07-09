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
            using var buffer = new UnmanagedRioBuffer<RioBufferSegment>(5, 1024);
            
            // Assert
            Assert.AreEqual(5, buffer.Length);
            Assert.AreEqual(sizeof(RioBufferSegment) + 1024, buffer.EntryReservedSpaceSize);
            Assert.AreNotEqual(IntPtr.Zero, (IntPtr)buffer.FirstEntry);
        }
    }
}
