using System;
using System.Runtime.InteropServices;

namespace Abc.Zerio.Channel
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct SendCompletionToken : IEquatable<SendCompletionToken>
    {        
        [FieldOffset(0)]
        public readonly int RemainingPaddingBytes;

        [FieldOffset(4)]
        public readonly int ByteRead;
        
        public static readonly SendCompletionToken Empty = new SendCompletionToken(0, 0);
            
        public SendCompletionToken(int remainingPaddingBytes, int byteRead)
        {
            RemainingPaddingBytes = remainingPaddingBytes;
            ByteRead = byteRead;
        }
        
        public unsafe static explicit operator long(SendCompletionToken token)
        {
            return *(long*)&token;
        }  
        
        public unsafe static explicit operator SendCompletionToken(long correlationId)
        {
            return *(SendCompletionToken*)&correlationId;
        }

        public bool Equals(SendCompletionToken other) => RemainingPaddingBytes == other.RemainingPaddingBytes
                                                         && ByteRead == other.ByteRead;

        public override bool Equals(object obj) => obj is SendCompletionToken other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(RemainingPaddingBytes, ByteRead);

        public static bool operator ==(SendCompletionToken left, SendCompletionToken right) => left.Equals(right);

        public static bool operator !=(SendCompletionToken left, SendCompletionToken right) => !left.Equals(right);
    }
}
