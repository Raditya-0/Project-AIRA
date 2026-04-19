public interface IMemoryProvider
{
    void AddMessage(string role, string content);
    string GetFullContext();
    string BuildLongTermContext();
    void SaveMemory();
    void LoadMemory();
}
