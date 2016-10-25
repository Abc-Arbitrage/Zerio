namespace Abc.Zerio.Core
{
    public static unsafe class Crc32
    {
        public static uint Compute(byte[] source)
        {
            fixed (byte* pSource = source)
            {
                return Compute(pSource, source.Length);
            }
        }

        public static uint Compute(byte* bytes, int length)
        {
            const uint poly = 0xedb88320;
            const int tableLength = 256;

            var table = stackalloc uint[tableLength];

            for (uint i = 0; i < tableLength; ++i)
            {
                var temp = i;
                for (var j = 8; j > 0; --j)
                {
                    if ((temp & 1) == 1)
                        temp = (temp >> 1) ^ poly;
                    else
                        temp >>= 1;
                }
                table[i] = temp;
            }

            var crc = 0xffffffff;
            for (var i = 0; i < length; ++i)
            {
                var index = (byte)((crc & 0xff) ^ bytes[i]);
                crc = (crc >> 8) ^ table[index];
            }

            return ~crc;
        }
    }
}