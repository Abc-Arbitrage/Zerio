using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Abc.Zerio.Buffers;
using Abc.Zerio.Framing;
using Abc.Zerio.Interop;
using NUnit.Framework;

namespace Abc.Zerio.Tests
{
    [TestFixture]
    public unsafe class MessageFramerTests : IDisposable
    {
        private byte[] _underlyingBuffer;
        private RIO_BUF[] _bufferDescriptors;
        private GCHandle _bufferHandle;
        private GCHandle _segmentbufferHandle;

        [SetUp]
        public void SetUp()
        {
            _underlyingBuffer = new byte[2048];
            _bufferDescriptors = new RIO_BUF[2048];

            _bufferHandle = GCHandle.Alloc(_underlyingBuffer, GCHandleType.Pinned);
            _segmentbufferHandle = GCHandle.Alloc(_bufferDescriptors, GCHandleType.Pinned);
        }

        private static IEnumerable<object[]> GetTestCases()
        {
            // 01  02  03  04  05  06  07 ' 08  09  10  11  12  13  14  15 ' 16  17  18  19  20 |
            // --  --  --  03   L   O   L ' --  --  --  04   T   O   T   O ' --  --  --  01   R '
            // [0]                        '                                '                    |
            //                            |>                               |>                   |>,0 
            yield return new object[] { "lol|toto|r", 20, "-|-|-|0" };

            // 01  02  03  04  05 ' 06  07  08  09  10 ' 11  12  13  14  15 ' 16  17  18  19  20 |
            // --  --  --  01   A ' --  --  --  01   B ' --  --  --  01   C ' --  --  --  01   D '
            // [0]                '                    '                    '                    |
            //                    |>                   |>                   |>                   |>,0
            yield return new object[] { "a|b|c|d", 20, "-|-|-|-|0" };

            // 01  02  03 | 04  05 ' 06 | 07  08  09 | 10 ' 11  12 | 13  14  15 | 16  17  18 | 19  20  |
            // --  --  -- | 01   A ' -- | --  --  01 |  B ' --  -- | --  01   C | --  --  -- | 01   D  |
            // [0]        | [1]    '    | [2]        | [3]'        | [4]        | [5]        | [6]     |
            //            |>       |>0  |>0          |>2  |>2      |>2          |>3,4        |>4       |>5,6
            yield return new object[] { "a|b|c|d", 3, "-|0|0|2|2|2|3|4|4|5|6" };

            // 01  02  03  04  05  06  07  08  09  10  11  12  13  14  15  16  17  18  19  20 |
            // --  --  --  01   A   B   C   D   E   F   G   H   I   J   K   L   M   N   O   P |
            // [0]                                                                            |
            //                                                                                |>,0
            yield return new object[] { "abcdefghijklmnop", 20, "-|0" };

            // 01  02  03  04  05  06  07  08  09  10 |
            // --  --  --  06   R   O   M   A   I   N |
            // [0]                                    |
            //                                        |>,0
            yield return new object[] { "romain", 20, "-|0" };

            // 01  02  03  04  05  06  07  08  09  10 | 11  12  13  14  15  16  17  18  19  20 |
            // --  --  --  06   R   O   M   A   I   N | --  --  --  06   V   I   A   N   D   E |
            // [0]                                    | [1]                                    |
            //                                        |>,0                                     |>0,1
            yield return new object[] { "romain|viande", 10, "-|0|0|1" };

            // 01  02  03  04  05  06  07  08  09 ' 10 | 11  12  13  14  15  16  17  18  19 |
            // --  --  --  05   R   O   M   A   I ' -- | --  --  06   V   I   A   N   D   E |
            // [0]                                '    | [1]                                |
            //                                    |>   |>                                   |>0,1
            yield return new object[] { "romai|viande", 10, "-|-|0|1" };

            // 01  02  03  04  05  06  07  08 ' 09  10 | 11  12  13  14  15  16  17  18 |
            // --  --  --  04   R   O   M   A ' --  -- | --  06   V   I   A   N   D   E |
            // [0]                            '        | [1]                            |
            //                                |>       |>                               |>0,1
            yield return new object[] { "roma|viande", 10, "-|-|0|1" };

            // 01  02  03  04  05  06  07 ' 08  09  10 | 11  12  13  14  15  16  17 |
            // --  --  --  03   R   O   M ' --  --  -- | 06   V   I   A   N   D   E |
            // [0]                        '            | [1]                        |
            //                            |>           |>                           |>0,1
            yield return new object[] { "rom|viande", 10, "-|-|0|1" };

            // 01  02  03  04  05  06 ' 07  08  09  10 | 11  12  13  14  15  16 |
            // --  --  --  02   R   O ' --  --  --  06 |  V   I   A   N   D   E |
            // [0]                    '                | [1]                    |
            //                        |>               |>0                      |>0,1
            yield return new object[] { "ro|viande", 10, "-|0|0|1" };

            // 01  02  03  04  05 ' 06  07  08  09  10 | 11  12  13  14  15 |
            // --  --  --  01   R ' --  --  --  06   V |  I   A   N   D   E |
            // [0]                '                    | [1]                |
            //                    |>                   |>                   |>,1
            yield return new object[] { "r|viande", 10, "-|-|-|1" };

            // 01  02  03  04  05  06  07  08 | 09  10  11  12  13  14  15  16 | 17  18  19  20  21  22  23 ' 24 | 25  26  27  28  29  30 |
            // --  --  --  20   J   E   F   R |  A   M   E   C   O   M   M   E |  U   N   G   R   A   N   D ' -- | --  --  03   O   U   I |
            // [0]                            | [1]                            | [2]                        '    | [3]                    |
            //                                |>                               |>                           |>   |>1                      |>2,3
            yield return new object[] { "jeframecommeungrand|oui", 8, "-|-|-|1|2|3" };

            // 01  02  03  04  05  06  07 ' 08  09  10 | 11  12  13  14  15 ' 16  17  18  19  20 | 21  22  23  24  25 |
            // --  --  --  03   L   O   L ' --  --  -- | 04   T   O   T   O ' --  --  --  06   R |  O   M   A   I   N |
            // [0]                        '            | [1]                '                    | [2]                |
            //                            |>           |>                   |>0                  |>0                  |>0,2
            yield return new object[] { "lol|toto|romain", 10, "-|-|0|0|0|2" };
        }

        [TestCaseSource(nameof(GetTestCases))]
        public void should_frame(string messageData, int bufferLength, string releasedBufferData)
        {
            // Arrange
            var releaser = new TestReleaser();
            var framer = new MessageFramer(releaser);

            var expectedLastReleasedSegmentIds = releasedBufferData.Split('|').Select(x => x == "-" ? -1 : int.Parse(x)).ToArray();
            var expectedFramedMessages = messageData.Split('|');
            var length = WriteLengthPrefixedMessages(_underlyingBuffer, expectedFramedMessages);
            var segments = CreateBuffers((byte*)_bufferHandle.AddrOfPinnedObject(), (RIO_BUF*)_segmentbufferHandle.AddrOfPinnedObject(), length, bufferLength);

            var framedMessages = new List<string>();
            var lastReleasedSegmentIds = new List<int>();

            // Act
            foreach (var segment in segments)
            {
                List<BufferSegment> frame;
                while (framer.TryFrameNextMessage(segment, out frame))
                {
                    var message = CreateMessage(frame);

                    framedMessages.Add(message);
                    lastReleasedSegmentIds.Add(releaser.LastReleasedSegmentId);
                }

                lastReleasedSegmentIds.Add(releaser.LastReleasedSegmentId);
            }

            // Assert
            Assert.AreEqual(expectedFramedMessages.Length, framedMessages.Count);
            for (var j = 0; j < expectedFramedMessages.Length; j++)
            {
                Assert.AreEqual(expectedFramedMessages[j], framedMessages[j]);
            }

            Assert.AreEqual(expectedLastReleasedSegmentIds.Length, lastReleasedSegmentIds.Count);
            for (var j = 0; j < expectedLastReleasedSegmentIds.Length; j++)
            {
                Assert.AreEqual(expectedLastReleasedSegmentIds[j], lastReleasedSegmentIds[j]);
            }
        }

        [Test, Ignore("Performance")]
        public void PerformanceTest()
        {
            var iterationCount = 3;
            var buffer = new byte[1024 * 1024 * 1024];
            var gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var distinctMessages = new[]
                {
                    new string('a', 42),
                    new string('b', 256),
                    new string('c', 512),
                    new string('d', 1024 + 512),
                };

                var messages = new List<string>();
                var messageCount = 0;
                var dataLength = 0;
                while (dataLength < buffer.Length - 4096)
                {
                    var message = distinctMessages[messageCount % distinctMessages.Length];
                    messages.Add(message);
                    dataLength += message.Length + sizeof(int);
                }

                var length = WriteLengthPrefixedMessages(buffer, messages.ToArray());
                var segments = CreateBuffers((byte*)gcHandle.AddrOfPinnedObject(), (RIO_BUF*)gcHandle.AddrOfPinnedObject(), length, 1024);

                Console.WriteLine($"{iterationCount} x {messages.Count:N0} messages");

                var releaser = new TestReleaser();
                var framer = new MessageFramer(releaser);
                var framedMessageCount = 0;

                GC.Collect();
                var sw = Stopwatch.StartNew();

                for (var iterations = 0; iterations < iterationCount; iterations++)
                {
                    foreach (var segment in segments)
                    {
                        List<BufferSegment> frame;
                        while (framer.TryFrameNextMessage(segment, out frame))
                        {
                            framedMessageCount++;
                        }
                    }
                }

                sw.Stop();
                var fps = framedMessageCount / sw.Elapsed.TotalSeconds;
                Console.WriteLine($"{framedMessageCount:N0} frames in {sw.Elapsed.TotalMilliseconds:N0} ms ({fps:N0} fps)");
            }
            finally
            {
                gcHandle.Free();
            }
        }

        private static string CreateMessage(IEnumerable<BufferSegment> frame)
        {
            var messageBytes = stackalloc byte[2048];
            var data = messageBytes;
            var dataLength = 0;
            foreach (var segment in frame)
            {
                Buffer.MemoryCopy(segment.Data, messageBytes, segment.Length, segment.Length);
                messageBytes += segment.Length;
                dataLength += segment.Length;
            }

            return Encoding.ASCII.GetString(data, dataLength);
        }

        private static int WriteLengthPrefixedMessages(byte[] buffer, params string[] messages)
        {
            var dataLength = 0;
            var memoryStream = new MemoryStream(buffer);
            var binaryWriter = new BinaryWriter(memoryStream);
            foreach (var message in messages)
            {
                var asciiMessage = Encoding.ASCII.GetBytes(message);
                binaryWriter.Write(asciiMessage.Length);
                binaryWriter.Write(asciiMessage);
                dataLength += sizeof(int) + asciiMessage.Length;
            }
            return dataLength;
        }

        private static List<RioBuffer> CreateBuffers(byte* buffer, RIO_BUF* segmentBuffer, int maxLength, int segmentLength = 10)
        {
            var segments = new List<RioBuffer>();
            var pBuffer = buffer;
            var readDataLength = 0;
            var segmentId = 0;
            while (readDataLength != maxLength)
            {
                var dataLength = Math.Min(segmentLength, maxLength - readDataLength);

                segmentBuffer->Length = dataLength;
                var segment = new RioBuffer(segmentId++, pBuffer, segmentBuffer, segmentLength)
                {
                    DataLength = dataLength
                };
                segments.Add(segment);
                readDataLength += dataLength;
                pBuffer += segmentLength;
                segmentBuffer++;
            }
            return segments;
        }

        private class TestReleaser : IRioBufferReleaser
        {
            internal int LastReleasedSegmentId { get; private set; } = -1;

            public void ReleaseBuffer(RioBuffer buffer)
            {
                LastReleasedSegmentId = buffer.Id > LastReleasedSegmentId ? buffer.Id : LastReleasedSegmentId;
            }
        }

        [TearDown]
        public void Teardown()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_bufferHandle.IsAllocated)
                _bufferHandle.Free();

            if(_segmentbufferHandle.IsAllocated)
                _segmentbufferHandle.Free();
        }
    }
}
