using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zerio.Buffers;
using Abc.Zerio.Interop;
using NUnit.Framework;

namespace Abc.Zerio.Tests
{
    [TestFixture]
    public unsafe class RioBufferManagerTests
    {
        [SetUp]
        public void SetUp()
        {
            WinSock.EnsureIsInitialized();
        }

        [Test]
        public void should_acquire_buffer()
        {
            // Arrange
            using (var bufferManager = RioBufferManager.Allocate(10, 64))
            {
                // Act
                var buffer = bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10));

                // Assert
                Assert.AreEqual(64, buffer.Length);
            }
        }

        [Test]
        public void should_read_an_acquired_buffer()
        {
            // Arrange
            using (var bufferManager = RioBufferManager.Allocate(10, 64))
            {
                var buffer = bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10));
                WriteTo(buffer, 42);

                // Act
                var readValue = ReadFrom(bufferManager.ReadBuffer(buffer.Id));

                // Assert
                Assert.AreEqual(42, readValue);
            }
        }

        [Test]
        public void should_block_when_no_buffer_is_available()
        {
            // Arrange
            using (var bufferManager = RioBufferManager.Allocate(1, 64))
            {
                bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10));

                // Act / Assert
                Assert.Throws<TimeoutException>(() => bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10)));
            }
        }

        [Test]
        public void should_unblock_when_a_buffer_is_available_again()
        {
            // Arrange
            using (var bufferManager = RioBufferManager.Allocate(1, 64))
            {
                var releaseBufferSignal = new AutoResetEvent(false);
                var bufferAcquiredFirst = bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10));

                var acquiringTask = Task.Run(() =>
                {
                    try
                    {
                        bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10));
                    }
                    catch (TimeoutException)
                    {
                        releaseBufferSignal.Set();
                    }

                    try
                    {
                        return bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10));
                    }
                    catch (TimeoutException)
                    {
                        return null;
                    }
                });

                if (!releaseBufferSignal.WaitOne(TimeSpan.FromMilliseconds(100)))
                    Assert.Fail();

                // Act
                bufferManager.ReleaseBuffer(bufferAcquiredFirst);

                // Assert
                var bufferAcquiredSecond = acquiringTask.Result;
                Assert.IsNotNull(bufferAcquiredSecond);
            }
        }

        [Test]
        public void should_not_acquire_buffer_when_acquiring_is_completed()
        {
            // Arrange
            using (var bufferManager = RioBufferManager.Allocate(1, 64))
            {
                bufferManager.CompleteAcquiring();

                // Act / Assert
                Assert.Throws<InvalidOperationException>(() => bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10)));
            }
        }

        [Test]
        public void should_unblock_acquiring_and_throw_completing_acquiring()
        {
            // Arrange
            using (var bufferManager = RioBufferManager.Allocate(1, 64))
            {
                bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10));

                var acquiringTask = Task.Run(() =>
                {
                    try
                    {
                        bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(200));
                        return null;
                    }
                    catch (Exception ex)
                    {
                        return ex;
                    }
                });

                // Act
                bufferManager.CompleteAcquiring();

                // Assert
                Assert.IsInstanceOf<InvalidOperationException>(acquiringTask.Result);
            }
        }

        [Test]
        public void should_release_buffers_on_reset()
        {
            // Arrange
            using (var bufferManager = RioBufferManager.Allocate(1, 64))
            {
                bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10));

                // Act
                bufferManager.Reset();

                // Assert
                var buffer = bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10));
                Assert.IsNotNull(buffer);
            }
        }

        [Test]
        public void should_allow_acquiring_again_on_reset()
        {
            // Arrange
            using (var bufferManager = RioBufferManager.Allocate(1, 64))
            {
                bufferManager.CompleteAcquiring();

                // Act
                bufferManager.Reset();

                // Assert
                var buffer = bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10));
                Assert.IsNotNull(buffer);
            }
        }

        [Test]
        public void should_reset_buffer_on_release()
        {
            // Arrange
            using (var bufferManager = RioBufferManager.Allocate(1, 64))
            {
                var buffer = bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10));
                buffer.DataLength = 12;

                // Act
                bufferManager.ReleaseBuffer(buffer);

                // Assert
                buffer = bufferManager.ReadBuffer(buffer.Id);
                Assert.AreEqual(64, buffer.DataLength);
            }
        }

        [Test]
        public void should_write_and_read_to_and_from_all_acquired_buffers()
        {
            // Arrange
            using (var bufferManager = RioBufferManager.Allocate(10, 64))
            {
                // Act / Assert
                var bufferIds = new List<int>();
                for (var i = 0; i < 10; i++)
                {
                    var buffer = bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(10));

                    for (var j = 0; j < buffer.Length; j++)
                    {
                        *(buffer.Data + j) = (byte)i;
                    }

                    bufferIds.Add(buffer.Id);
                }

                for (var i = 0; i < bufferIds.Count; i++)
                {
                    var bufferId = bufferIds[i];
                    var buffer = bufferManager.ReadBuffer(bufferId);
                    for (var j = 0; j < buffer.Length; j++)
                    {
                        var value = *(buffer.Data + j);
                        Assert.AreEqual(i, value);
                    }
                }
            }
        }

        [Test]
        public void should_acquire_and_release_from_multiple_threads()
        {
            // Arrange
            using (var bufferManager = RioBufferManager.Allocate(128, 64))
            {
                // Act
                var tasks = new Task[8];
                for (var i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        for (var j = 0; j < 100 * 1000; j++)
                        {
                            var buffers = new RioBuffer[10];
                            for (var k = 0; k < buffers.Length; k++)
                            {
                                buffers[k] = bufferManager.AcquireBuffer(TimeSpan.FromMilliseconds(100));
                            }

                            for (var k = 0; k < buffers.Length; k++)
                            {
                                bufferManager.ReleaseBuffer(buffers[k]);
                            }
                        }
                    });
                }

                // Assert
                var allTaskAreCompleted = Task.WaitAll(tasks, TimeSpan.FromSeconds(1));
                Assert.IsTrue(allTaskAreCompleted);
            }
        }

        private void WriteTo(RioBuffer buffer, int value)
        {
            if (buffer.Length < sizeof(int))
                throw new ArgumentException("buffer is too small", nameof(buffer));

            *(int*)buffer.Data = value;
        }

        private int ReadFrom(RioBuffer buffer)
        {
            if (buffer.Length < sizeof(int))
                throw new ArgumentException("buffer is too small", nameof(buffer));

            return *(int*)buffer.Data;
        }
    }
}
