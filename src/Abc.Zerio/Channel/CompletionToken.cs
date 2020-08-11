using System;
using System.Runtime.InteropServices;

namespace Abc.Zerio.Channel
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct CompletionToken : IEquatable<CompletionToken>
    {
        [FieldOffset(0)]
        public readonly int ByteRead;
        
        public static readonly CompletionToken Empty = new CompletionToken(0);
            
        public CompletionToken( int byteRead)
        {
            ByteRead = byteRead;
        }
        
        public unsafe static explicit operator long(CompletionToken token)
        {
            return *(long*)&token;
        }  
        
        public unsafe static explicit operator CompletionToken(long correlationId)
        {
            return *(CompletionToken*)&correlationId;
        }

        public bool Equals(CompletionToken other) => ByteRead == other.ByteRead;

        public override bool Equals(object obj) => obj is CompletionToken other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ByteRead);

        public static bool operator ==(CompletionToken left, CompletionToken right) => left.Equals(right);

        public static bool operator !=(CompletionToken left, CompletionToken right) => !left.Equals(right);
    }
}
