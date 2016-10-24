using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Abc.Zerio.Framing;
using Abc.Zerio.Tests.Utils;
using NUnit.Framework;

namespace Abc.Zerio.Tests
{
    [TestFixture]
    public unsafe class UnsafeBinaryReaderTests
    {
        private const uint _marker = 0xdeadbeef;

        private BufferSegmentProvider _segmentProvider;
        private UnsafeBinaryReader _reader;
        private BinaryWriter _writer;

        private void CreateContext(Encoding encoding, int bufferSegmentSize, int bufferSegmentCount = 512)
        {
            _segmentProvider = new BufferSegmentProvider(bufferSegmentCount, bufferSegmentSize);

            _reader = new UnsafeBinaryReader(encoding);
            _reader.SetBuffers(_segmentProvider.GetBufferSegments());

            _writer = _segmentProvider.GetBinaryWriter(encoding);
        }

        [TearDown]
        public void Teardown()
        {
            _segmentProvider.Dispose();
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

        private static IEnumerable<Encoding> GetEncodings
        {
            get
            {
                yield return Encoding.UTF7;
                yield return Encoding.UTF8;
                yield return Encoding.UTF32;
                yield return Encoding.ASCII;
                yield return Encoding.GetEncoding("UTF-16LE");
                yield return Encoding.GetEncoding("UTF-16BE");
            }
        }

        [Test]
        public void should_read_numeric_values([ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
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
        public void should_read_chars([ValueSource(nameof(GetEncodings))] Encoding encoding,
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
        public void should_read_char_array([ValueSource(nameof(GetEncodings))] Encoding encoding,
                                           [ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(encoding, bufferSegmentSize);

            var chars = "Hello world!".ToCharArray();
            _writer.Write(chars, 0, chars.Length);
            _writer.Write(_marker);

            var readChars = _reader.ReadChars(chars.Length);
            Assert.AreEqual(chars, readChars);

            Assert.AreEqual(_marker, _reader.ReadUInt32());
        }

        [Test, Combinatorial]
        public void should_read_char_array_with_buffer([ValueSource(nameof(GetEncodings))] Encoding encoding,
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

        [Test, Combinatorial]
        public void should_read_char_array_with_small_buffer([ValueSource(nameof(GetEncodings))] Encoding encoding,
                                                             [ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(encoding, bufferSegmentSize);

            var chars = "Hello!".ToCharArray();
            _writer.Write(chars);
            _writer.Write(_marker);

            var buffer = new char[100];
            Assert.AreEqual(chars.Length, _reader.Read(buffer, 0, chars.Length));
            Assert.AreEqual(chars, buffer.Take(chars.Length).ToArray());

            Assert.AreEqual(_marker, _reader.ReadUInt32());
        }

        [Test, Combinatorial]
        public void should_read_char_array_at_end_of_file([ValueSource(nameof(GetEncodings))] Encoding encoding,
                                                          [ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(encoding, bufferSegmentSize);

            var chars = "Hello World!".ToCharArray();
            _writer.Write(chars, 0, chars.Length);

            var charsSubstring = chars.Take(5).ToArray();
            var buffer = new char[5];
            _reader.SetBuffer(_segmentProvider.GetUnderlyingBufferPointer(), encoding.GetByteCount(charsSubstring));

            Assert.AreEqual(5, _reader.Read(buffer, 0, buffer.Length));
            Assert.AreEqual(charsSubstring, buffer);
        }

        [Test, Combinatorial]
        public void should_read_char_at_end_of_file([ValueSource(nameof(GetEncodings))] Encoding encoding,
                                                    [ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(encoding, bufferSegmentSize);

            _reader.SetBuffer(_segmentProvider.GetUnderlyingBufferPointer(), 0);

            Assert.AreEqual(-1, _reader.Read());
            Assert.AreEqual(-1, _reader.PeekChar());
        }

        [Test]
        public void should_read_bytes([ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
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

        [Test]
        public void should_read_bytes_with_buffer([ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(Encoding.UTF8, bufferSegmentSize);

            var bytes = new byte[bufferSegmentSize];
            new Random().NextBytes(bytes);
            _writer.Write(bytes, 0, bytes.Length);
            _writer.Write(_marker);

            var read = new byte[bytes.Length];
            _reader.Read(read, 0, read.Length);

            Assert.AreEqual(bytes, read);
            Assert.AreEqual(_marker, _reader.ReadUInt32());
        }

        [Test, Combinatorial]
        public void should_read_string([ValueSource(nameof(GetEncodings))] Encoding encoding,
                                       [ValueSource(nameof(GetBufferSegmentSizes))] int bufferSegmentSize)
        {
            CreateContext(encoding, bufferSegmentSize);

            _writer.Write("Hello World!");
            _writer.Write(_marker);

            Assert.AreEqual("Hello World!", _reader.ReadString());
            Assert.AreEqual(_marker, _reader.ReadUInt32());
        }

        [Test]
        public void should_read_value_with_exact_buffer_size()
        {
            CreateContext(Encoding.UTF8, sizeof(int));

            _writer.Write(0);

            Assert.DoesNotThrow(() => _reader.ReadInt32());
        }

        private static IEnumerable<object[]> GetSmallBufferTestCases
        {
            get
            {
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadInt32()), sizeof(int) - 1 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadUInt32()), sizeof(uint) - 1 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadInt16()), sizeof(short) - 1 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadUInt16()), sizeof(ushort) - 1 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadInt64()), sizeof(long) - 1 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadUInt64()), sizeof(ulong) - 1 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadByte()), sizeof(byte) - 1 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadSByte()), sizeof(sbyte) - 1 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadBoolean()), sizeof(bool) - 1 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadSingle()), sizeof(float) - 1 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadDouble()), sizeof(double) - 1 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadDecimal()), sizeof(decimal) - 1 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadChar()), 0 };
                yield return new object[] { (Action<UnsafeBinaryReader>)(r => r.ReadString()), 0 };
            }
        }

        [TestCaseSource(nameof(GetSmallBufferTestCases))]
        public void should_fail_when_buffer_size_is_too_small(Action<UnsafeBinaryReader> readerAction, int bufferSegmentSize)
        {
            CreateContext(Encoding.ASCII, bufferSegmentSize, 1);

            Assert.Throws<InvalidOperationException>(() => readerAction(_reader));
        }
    }
}
