using System;
using System.Threading;
using System.Threading.Tasks;

public interface ILLMProvider
{
    event Action<string> OnResponseReceived;
    Task<string> SendMessage(string fullContext, CancellationToken token = default);
    void CancelCurrent();
}
