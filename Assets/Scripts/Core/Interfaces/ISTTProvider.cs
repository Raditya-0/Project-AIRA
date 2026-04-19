using System;

public interface ISTTProvider
{
    event Action<string> OnResultReady;
    void StartListening();
    void StopListening();
}
