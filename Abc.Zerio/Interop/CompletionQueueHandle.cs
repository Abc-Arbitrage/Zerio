using Microsoft.Win32.SafeHandles;

namespace Abc.Zerio.Interop
{
    public class CompletionQueueHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public CompletionQueueHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            WinSock.Extensions.CloseCompletionQueue(handle);
            return true;
        }
    }
}