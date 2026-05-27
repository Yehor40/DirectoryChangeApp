namespace DirectoryChangeApp.Repository;

public class JsonStateRepository: IStateRepository
{
    private const string StateFilePath = "state.json";
    
    public Dictionary<string, FileItem> LoadState()
    {
        if (!File.Exists(StateFilePath)) return new Dictionary<string, FileItem>();
        var json = File.ReadAllText(StateFilePath);
        return JsonSerializer.Deserialize<Dictionary<string, FileItem>>(json) ?? new();
    }

    public void SaveState(Dictionary<string, FileItem> state)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StateFilePath, json);    }
}