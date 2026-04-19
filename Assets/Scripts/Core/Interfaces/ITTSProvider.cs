using System;

public interface ITTSProvider
{
    event Action OnSpeakStart;
    event Action OnSpeakEnd;
    void Speak(string text, string expression = "NEUTRAL");
    void EnqueueSpeak(string text, string expression = "NEUTRAL");
    void StopAll();
}
