using System.Runtime.InteropServices;

namespace Abc.Zerio.Channel
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct SendCompletionToken
    {        
        [FieldOffset(0)]
        public int ByteRead;

        [FieldOffset(4)]
        public bool IsEndOfBatch;
        
        public static readonly SendCompletionToken Empty = new SendCompletionToken(0, false);
            
        public SendCompletionToken(int byteRead, bool isEndOfBatch)
        {
            ByteRead = byteRead;
            IsEndOfBatch = isEndOfBatch;
        }
        
        public unsafe static explicit operator long(SendCompletionToken token)
        {
            return *(long*)&token;
        }  
        
        public unsafe static explicit operator SendCompletionToken(long correlationId)
        {
            return *(SendCompletionToken*)&correlationId;
        }
    }
}
