/*
 * Copyright 2014 - 2017 Adaptive Financial Consulting Ltd
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Channel
{
    // This data structure is largely inspired from Agrona ManyToOneRingBuffer. It was modified to use a RIO registered buffer
    //  as memory space, and a new API that supports deferred head cursor move and batching for reads. 
    // https://github.com/AdaptiveConsulting/Aeron.NET/blob/master/src/Adaptive.Agrona/Concurrent/RingBuffer/ManyToOneRingBuffer.cs
    public unsafe class ManyToOneRingBuffer : IDisposable
    {
        private const int _insufficientCapacity = -2;

        private readonly int _capacity;
        private readonly int _maxMsgLength;
        private readonly int _tailPositionIndex;
        private readonly int _headCachePositionIndex;
        private readonly int _headPositionIndex;

        private readonly int _bufferLength;
        private readonly IntPtr _bufferHandle;
        private readonly IntPtr _bufferId;
        private readonly byte* _bufferStart;

        public event ChannelFrameReadDelegate FrameRead;
        private readonly List<ChannelFrame> _currentBatch = new List<ChannelFrame>(1024);
        
        private int _reading;

        public ManyToOneRingBuffer(int minimumSize)
        {
            _bufferLength = BitUtil.FindNextPositivePowerOfTwo(minimumSize) + RingBufferDescriptor.TrailerLength;

            const int allocationType = Kernel32.Consts.MEM_COMMIT | Kernel32.Consts.MEM_RESERVE;
            _bufferHandle = Kernel32.VirtualAlloc(IntPtr.Zero, (uint)_bufferLength, allocationType, Kernel32.Consts.PAGE_READWRITE);
            if (_bufferHandle == IntPtr.Zero)
                WinSock.ThrowLastWsaError();

            WinSock.EnsureIsInitialized();

            _bufferId = WinSock.Extensions.RegisterBuffer(_bufferHandle, (uint)_bufferLength);
            if (_bufferId == WinSock.Consts.RIO_INVALID_BUFFERID)
                WinSock.ThrowLastWsaError();

            _bufferStart = (byte*)_bufferHandle.ToPointer();

            _capacity = _bufferLength - RingBufferDescriptor.TrailerLength;

            _maxMsgLength = _capacity / 8;
            _tailPositionIndex = _capacity + RingBufferDescriptor.TailPositionOffset;
            _headCachePositionIndex = _capacity + RingBufferDescriptor.HeadCachePositionOffset;
            _headPositionIndex = _capacity + RingBufferDescriptor.HeadPositionOffset;
        }

        public bool Write(ReadOnlySpan<byte> messageBytes)
        {
            CheckMessageLength(messageBytes.Length);

            var isSuccessful = false;

            var buffer = _bufferStart;
            var recordLength = messageBytes.Length + ChannelFrame.HeaderLength;
            var requiredCapacity = BitUtil.Align(recordLength, ChannelFrame.Alignment);
            var recordIndex = ClaimCapacity(buffer, requiredCapacity);

            if (_insufficientCapacity != recordIndex)
            {
                PutLongOrdered(buffer, recordIndex, ChannelFrame.MakeHeader(-recordLength, ChannelFrame.DataFrame));
                Thread.MemoryBarrier();
                PutBytes(buffer, ChannelFrame.EncodedDataOffset(recordIndex), messageBytes);
                PutIntOrdered(buffer, ChannelFrame.LengthOffset(recordIndex), recordLength);

                isSuccessful = true;
            }

            return isSuccessful;
        }

        public int Read(int messageCountLimit = int.MaxValue)
        {
            if (Interlocked.CompareExchange(ref _reading, 1, 0) != 0)
                return 0;
            
            var messagesRead = 0;
            var buffer = _bufferStart;
            var head = GetLong(buffer, _headPositionIndex);

            var capacity = _capacity;
            var headIndex = (int)head & (capacity - 1);
            var maxBlockLength = capacity - headIndex;
            var bytesRead = 0;

            _currentBatch.Clear();

            while (bytesRead < maxBlockLength && messagesRead < messageCountLimit)
            {
                var recordIndex = headIndex + bytesRead;
                var header = GetLongVolatile(buffer, recordIndex);

                var recordLength = ChannelFrame.GetFrameLength(header);
                if (recordLength <= 0)
                    break;

                var frameLength = ChannelFrame.GetAlignedFrameLength(recordLength);
                bytesRead += frameLength;

                ++messagesRead;

                var messageTypeId = ChannelFrame.GetFrameTypeId(header);
                if (messageTypeId == ChannelFrame.PaddingFrame)
                {
                    _currentBatch.Add(new ChannelFrame(buffer + recordIndex, frameLength, 0));
                    continue;
                }

                var dataLength = ChannelFrame.GetDataLength(recordLength);
                _currentBatch.Add(new ChannelFrame(buffer + recordIndex, frameLength, dataLength));
            }

            if (messagesRead <= 0)
            {
                Volatile.Write(ref _reading, 0);
                return messagesRead;
            }

            var endOfBatchIndex = messagesRead - 1;
            for (var i = 0; i < messagesRead; i++)
            {
                if (i != endOfBatchIndex)
                {
                    FrameRead?.Invoke(_currentBatch[i], false, CompletionToken.Empty);
                }
                else
                {
                    FrameRead?.Invoke(_currentBatch[i], true, new CompletionToken(bytesRead));
                }
            }

            return messagesRead;
        }

        public void CompleteRead(CompletionToken token)
        {
            try
            {
                if (token == CompletionToken.Empty)
                    return;

                var head = GetLong(_bufferStart, _headPositionIndex);

                var capacity = _capacity;
                var headIndex = (int)head & (capacity - 1);
                var buffer = _bufferStart;

                CheckBounds(headIndex, token.ByteRead);
                Unsafe.InitBlock(buffer + headIndex, 0, (uint)token.ByteRead);
                PutLongOrdered(buffer, _headPositionIndex, head + token.ByteRead);
            }
            finally
            {
                Volatile.Write(ref _reading, 0);   
            }            
        }

        private void CheckMessageLength(int length)
        {
            if (length <= _maxMsgLength)
                return;

            throw new ArgumentException($"encoded message exceeds maxMsgLength of {_maxMsgLength:D}, length={length:D}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PutBytes(byte* buffer, int index, ReadOnlySpan<byte> messageBytes)
        {
            if (messageBytes.Length == 0)
                return;

            CheckBounds(index, messageBytes.Length);

            var destination = new Span<byte>(buffer + index, messageBytes.Length);
            messageBytes.CopyTo(destination);
        }

        private int ClaimCapacity(byte* buffer, int requiredCapacity)
        {
            var capacity = _capacity;
            var tailPositionIndex = _tailPositionIndex;
            var headCachePositionIndex = _headCachePositionIndex;
            var mask = capacity - 1;

            var head = GetLongVolatile(buffer, headCachePositionIndex);

            long tail;
            int tailIndex;
            int padding;
            do
            {
                tail = GetLongVolatile(buffer, tailPositionIndex);
                var availableCapacity = capacity - (int)(tail - head);

                if (requiredCapacity > availableCapacity)
                {
                    head = GetLongVolatile(buffer, _headPositionIndex);

                    if (requiredCapacity > (capacity - (int)(tail - head)))
                        return _insufficientCapacity;

                    PutLongOrdered(buffer, headCachePositionIndex, head);
                }

                padding = 0;
                tailIndex = (int)tail & mask;
                var toBufferEndLength = capacity - tailIndex;

                if (requiredCapacity > toBufferEndLength)
                {
                    var headIndex = (int)head & mask;

                    if (requiredCapacity > headIndex)
                    {
                        head = GetLongVolatile(buffer, _headPositionIndex);
                        headIndex = (int)head & mask;
                        if (requiredCapacity > headIndex)
                            return _insufficientCapacity;

                        PutLongOrdered(buffer, headCachePositionIndex, head);
                    }

                    padding = toBufferEndLength;
                }
            } while (!CompareAndSetLong(buffer, tailPositionIndex, tail, tail + requiredCapacity + padding));

            if (0 != padding)
            {
                PutLongOrdered(buffer, tailIndex, ChannelFrame.MakeHeader(padding, ChannelFrame.PaddingFrame));
                tailIndex = 0;
            }

            return tailIndex;
        }

        public bool CompareAndSetLong(byte* buffer, int index, long expectedValue, long updateValue)
        {
            CheckBounds(index, sizeof(long));

            var original = Interlocked.CompareExchange(ref *(long*)(buffer + index), updateValue, expectedValue);

            return original == expectedValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckBounds(int index, int length)
        {
            var resultingPosition = index + (long)length;
            if (index < 0 || resultingPosition > _bufferLength)
                throw new IndexOutOfRangeException($"index={index}, length={length}, capacity={_capacity}");
        }

        public RIO_BUF CreateBufferSegmentDescriptor(ChannelFrame frame)
        {
            var bufferSegmentDescriptor = new RIO_BUF
            {
                BufferId = _bufferId,
                Length = frame.FrameLength,
                Offset = (int)(frame.FrameStart - _bufferStart)
            };

            return bufferSegmentDescriptor;
        }

        public void Dispose()
        {
            try
            {
                WinSock.Extensions.DeregisterBuffer(_bufferId);
            }
            finally
            {
                Kernel32.VirtualFree(_bufferHandle, 0, Kernel32.Consts.MEM_RELEASE);
            }
        }

        public static long GetLongVolatile(byte* buffer, int index) => Volatile.Read(ref *(long*)(buffer + index));

        public static long GetLong(byte* buffer, int index) => *(long*)(buffer + index);

        public static void PutLongOrdered(byte* buffer, long index, long value) => Volatile.Write(ref *(long*)(buffer + index), value);

        public static void PutIntOrdered(byte* buffer, long index, int value) => Volatile.Write(ref *(int*)(buffer + index), value);

        private static class RingBufferDescriptor
        {
            public static readonly int TailPositionOffset;
            public static readonly int HeadCachePositionOffset;
            public static readonly int HeadPositionOffset;
            public static readonly int TrailerLength;

            static RingBufferDescriptor()
            {
                var offset = 0;
                offset += BitUtil.CacheLineLength * 2;
                TailPositionOffset = offset;

                offset += BitUtil.CacheLineLength * 2;
                HeadCachePositionOffset = offset;

                offset += BitUtil.CacheLineLength * 2;
                HeadPositionOffset = offset;

                offset += BitUtil.CacheLineLength * 2;
                TrailerLength = offset;
            }
        }
    }
}
