using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Abc.Zerio.Framing;
using NUnit.Framework;

namespace Abc.Zerio.Tests
{
    public unsafe class UnsafeBinaryWriterTests
    {
        private const uint _marker = 0xdeadbeef;
        private const int _bufferLength = 32 * 1024;

        private UnsafeBinaryWriter _writer;
        private byte[] _buffer;
        private GCHandle _handle;
        private byte* _pointer;
        private BinaryReader _reader;
        private MemoryStream _readerStream;

        [SetUp]
        public void SetUp()
        {
            _buffer = new byte[_bufferLength];
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            _pointer = (byte*)_handle.AddrOfPinnedObject();
        }

        private void CreateContext(Encoding encoding, int bufferSegmentSize)
        {
            _writer = new UnsafeBinaryWriter(encoding);
            _readerStream = new MemoryStream(_buffer);
            _reader = new BinaryReader(_readerStream, encoding);

            SetBuffers(bufferSegmentSize);
        }

        private void SetBuffers(int segmentSize)
        {
            var segments = new List<BufferSegment>();

            var pointer = _pointer;
            while (pointer <= _pointer + _buffer.Length - segmentSize)
            {
                segments.Add(new BufferSegment(pointer, segmentSize));
                pointer += segmentSize;
            }

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(segments));
        }

        [TearDown]
        public void Teardown()
        {
            _handle.Free();
        }

        private static IEnumerable<int> GetBufferSegmentSizes
        {
            get
            {
                for (var i = 1; i < 10; i++)
                {
                    yield return i;
                }

                yield return 128;
                yield return 200;
                yield return 512;
                yield return 1024;
                yield return 4096;
                yield return 10 * 1024;
            }
        }

        //// UnsafeBinaryWriter is not yet capable to handle string/chars writes over multiple buffers
        //private static IEnumerable<int> GetCharRelatedTestsBufferSegmentSizes
        //{
        //    get
        //    {
        //        yield return 128;
        //        yield return 200;
        //        yield return 512;
        //        yield return 1024;
        //        yield return 4096;
        //        yield return 10 * 1024;
        //    }
        //}

        private static IEnumerable<Encoding> GetEncodings
        {
            get
            {
                yield return Encoding.UTF8;
                yield return Encoding.UTF32;
                yield return Encoding.ASCII;
                yield return Encoding.GetEncoding("UTF-16LE");
                yield return Encoding.GetEncoding("UTF-16BE");
            }
        }

        private static IEnumerable<Encoding> GetVariableLenghtEncodings
        {
            get
            {
                yield return Encoding.UTF8;
                yield return Encoding.UTF32;
                yield return Encoding.GetEncoding("UTF-16LE");
                yield return Encoding.GetEncoding("UTF-16BE");
            }
        }
        
        [Test]
        public void should_write_numeric_values([ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(Encoding.UTF8, bufferSegmentSize);

            _writer.Write(1);
            _writer.Write(-1);
            _writer.Write(int.MaxValue);
            _writer.Write(int.MinValue);

            _writer.Write((uint)2);
            _writer.Write(uint.MaxValue);
            _writer.Write(uint.MinValue);

            _writer.Write((short)3);
            _writer.Write((short)-3);
            _writer.Write(short.MaxValue);
            _writer.Write(short.MinValue);

            _writer.Write((ushort)4);
            _writer.Write(ushort.MaxValue);
            _writer.Write(ushort.MinValue);

            _writer.Write((long)5);
            _writer.Write((long)-5);
            _writer.Write(long.MaxValue);
            _writer.Write(long.MinValue);

            _writer.Write((ulong)6);
            _writer.Write(ulong.MaxValue);
            _writer.Write(ulong.MinValue);

            _writer.Write((byte)7);
            _writer.Write(byte.MaxValue);
            _writer.Write(byte.MinValue);

            _writer.Write((sbyte)8);
            _writer.Write((sbyte)-8);
            _writer.Write(sbyte.MaxValue);
            _writer.Write(sbyte.MinValue);

            _writer.Write(true);
            _writer.Write(false);

            _writer.Write(2.2f);
            _writer.Write(-2.2f);
            _writer.Write(float.MaxValue);
            _writer.Write(float.MinValue);

            _writer.Write(3.3d);
            _writer.Write(-3.3d);
            _writer.Write(double.MaxValue);
            _writer.Write(double.MinValue);

            _writer.Write(4.4m);
            _writer.Write(-4.4m);
            _writer.Write(decimal.MaxValue);
            _writer.Write(decimal.MinValue);

            _writer.Write(_marker);

            Assert.AreEqual(1, _reader.ReadInt32());
            Assert.AreEqual(-1, _reader.ReadInt32());
            Assert.AreEqual(int.MaxValue, _reader.ReadInt32());
            Assert.AreEqual(int.MinValue, _reader.ReadInt32());

            Assert.AreEqual(2, _reader.ReadUInt32());
            Assert.AreEqual(uint.MaxValue, _reader.ReadUInt32());
            Assert.AreEqual(uint.MinValue, _reader.ReadUInt32());

            Assert.AreEqual(3, _reader.ReadInt16());
            Assert.AreEqual(-3, _reader.ReadInt16());
            Assert.AreEqual(short.MaxValue, _reader.ReadInt16());
            Assert.AreEqual(short.MinValue, _reader.ReadInt16());

            Assert.AreEqual(4, _reader.ReadUInt16());
            Assert.AreEqual(ushort.MaxValue, _reader.ReadUInt16());
            Assert.AreEqual(ushort.MinValue, _reader.ReadUInt16());

            Assert.AreEqual(5, _reader.ReadInt64());
            Assert.AreEqual(-5, _reader.ReadInt64());
            Assert.AreEqual(long.MaxValue, _reader.ReadInt64());
            Assert.AreEqual(long.MinValue, _reader.ReadInt64());

            Assert.AreEqual(6, _reader.ReadUInt64());
            Assert.AreEqual(ulong.MaxValue, _reader.ReadUInt64());
            Assert.AreEqual(ulong.MinValue, _reader.ReadUInt64());

            Assert.AreEqual(7, _reader.ReadByte());
            Assert.AreEqual(byte.MaxValue, _reader.ReadByte());
            Assert.AreEqual(byte.MinValue, _reader.ReadByte());

            Assert.AreEqual(8, _reader.ReadSByte());
            Assert.AreEqual(-8, _reader.ReadSByte());
            Assert.AreEqual(sbyte.MaxValue, _reader.ReadSByte());
            Assert.AreEqual(sbyte.MinValue, _reader.ReadSByte());

            Assert.AreEqual(true, _reader.ReadBoolean());
            Assert.AreEqual(false, _reader.ReadBoolean());

            Assert.AreEqual(2.2f, _reader.ReadSingle());
            Assert.AreEqual(-2.2f, _reader.ReadSingle());
            Assert.AreEqual(float.MaxValue, _reader.ReadSingle());
            Assert.AreEqual(float.MinValue, _reader.ReadSingle());

            Assert.AreEqual(3.3d, _reader.ReadDouble());
            Assert.AreEqual(-3.3d, _reader.ReadDouble());
            Assert.AreEqual(double.MaxValue, _reader.ReadDouble());
            Assert.AreEqual(double.MinValue, _reader.ReadDouble());

            Assert.AreEqual(4.4m, _reader.ReadDecimal());
            Assert.AreEqual(-4.4m, _reader.ReadDecimal());
            Assert.AreEqual(decimal.MaxValue, _reader.ReadDecimal());
            Assert.AreEqual(decimal.MinValue, _reader.ReadDecimal());

            Assert.AreEqual(_marker, _reader.ReadUInt32());
        }

        [Test, Combinatorial]
        public void should_write_chars([ValueSource(nameof(GetEncodings))] Encoding encoding,
                                       [ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(encoding, bufferSegmentSize);

            _writer.Write('X');
            _writer.Write('X');

            Assert.AreEqual((int)'X', _reader.PeekChar());
            Assert.AreEqual('X', _reader.ReadChar());
            Assert.AreEqual((int)'X', _reader.Read());
        }

        [Test, Combinatorial]
        public void should_write_char_array([ValueSource(nameof(GetEncodings))] Encoding encoding,
                                            [ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(encoding, bufferSegmentSize);

            var chars = "Hello World!".ToCharArray();
            _writer.Write(chars, 0, chars.Length);
            _writer.Write(_marker);
            
            Assert.AreEqual("Hello World!", new string(_reader.ReadChars(chars.Length)));
            Assert.AreEqual(_marker, _reader.ReadUInt32());
        }

        [Test, Combinatorial]
        public void should_write_char_array_containing_multibyte_chars([ValueSource(nameof(GetVariableLenghtEncodings))] Encoding encoding,
                                            [ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(encoding, bufferSegmentSize);

            var chars = "إن شاء الله‎‎".ToCharArray();
            _writer.Write(chars, 0, chars.Length);
            _writer.Write(_marker);

            Assert.AreEqual("إن شاء الله‎‎", new string(_reader.ReadChars(chars.Length)));
            Assert.AreEqual(_marker, _reader.ReadUInt32());
        }

        [Test, Combinatorial]
        public void should_write_char_array_with_buffer([ValueSource(nameof(GetEncodings))] Encoding encoding,
                                                        [ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(encoding, bufferSegmentSize);

            var chars = "Hello World!".ToCharArray();
            _writer.Write(chars, 0, chars.Length);
            _writer.Write(_marker);

            var buffer = new char[chars.Length];
            Assert.AreEqual(chars.Length, _reader.Read(buffer, 0, buffer.Length));
            Assert.AreEqual(chars, buffer);

            Assert.AreEqual(_marker, _reader.ReadUInt32());
        }

        [Test]
        public void should_write_bytes([ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(Encoding.UTF8, bufferSegmentSize);

            var bytes = new byte[bufferSegmentSize];
            new Random().NextBytes(bytes);
            _writer.Write(bytes, 0, bytes.Length);
            _writer.Write(_marker);

            var read = _reader.ReadBytes(bytes.Length);

            Assert.AreEqual(bytes, read);
            Assert.AreEqual(_marker, _reader.ReadUInt32());
        }

        [Test, Combinatorial]
        public void should_write_string([ValueSource(nameof(GetEncodings))] Encoding encoding,
                                        [ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(encoding, bufferSegmentSize);

            _writer.Write("Hello World!");
            _writer.Write(_marker);

            Assert.AreEqual("Hello World!", _reader.ReadString());
            Assert.AreEqual(_marker, _reader.ReadUInt32());
        }

        [Test]
        public void should_write_value_with_exact_buffer_size()
        {
            CreateContext(Encoding.UTF8, _bufferLength);

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(int)));
            _writer.Write(56);

            Assert.AreEqual(56, _reader.ReadInt32());
        }

        [Test]
        public void should_get_and_set_position()
        {
            CreateContext(Encoding.UTF8, 100);
            _writer.Write(new byte[99]);

            var position = _writer.GetPosition();
            _writer.Write(0u);
            _writer.SetPosition(position);
            _writer.Write(_marker);

            _reader.ReadBytes(99);

            Assert.AreEqual(_marker, _reader.ReadUInt32());
        }

        [Test]
        public void should_fail_when_buffer_size_is_too_small([ValueSource(nameof(GetEncodings))] Encoding encoding)
        {
            CreateContext(encoding, 64);

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(int) - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write(default(int)));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(uint) - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write(default(uint)));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(short) - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write(default(short)));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(ushort) - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write(default(ushort)));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(long) - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write(default(long)));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(ulong) - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write(default(ulong)));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(byte) - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write(default(byte)));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(sbyte) - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write(default(sbyte)));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, encoding.GetByteCount("X") - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write('X'));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(bool) - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write(default(bool)));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(float) - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write(default(float)));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(double) - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write(default(double)));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, sizeof(decimal) - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write(default(decimal)));

            _writer.SetBufferSegmentProvider(new BufferSegmentProvider(_pointer, encoding.GetByteCount("X") - 1));
            Assert.Throws<InvalidOperationException>(() => _writer.Write("X"));
        }

        private class BufferSegmentProvider : IBufferSegmentProvider
        {
            private readonly Queue<BufferSegment> _segments;

            public BufferSegmentProvider(byte* data, int length)
                : this(new[] { new BufferSegment(data, length) })
            {
            }

            public BufferSegmentProvider(IEnumerable<BufferSegment> segments)
            {
                _segments = new Queue<BufferSegment>(segments);
            }

            public BufferSegment GetBufferSegment()
            {
                return _segments.Dequeue();
            }
        }
    }
}
