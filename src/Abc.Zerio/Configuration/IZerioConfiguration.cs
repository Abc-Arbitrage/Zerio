namespace Abc.Zerio.Configuration
{
    public interface IZerioConfiguration
    {
        int SendingBufferCount { get; set; }
        int SendingBufferLength { get; set; }

        int ReceivingBufferCount { get; set; }
        int ReceivingBufferLength { get; set; }

        int MaxSendBatchSize { get; set; }

        int MaxSendCompletionResults { get; set; }
        int MaxReceiveCompletionResults { get; set; }
        int SessionCount { get; set; }
    }
}
