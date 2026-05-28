namespace DirectoryChangeApp.Repository;

public class JsonStateRepository: IStateRepository
{
    private const string StateFilePath = "state.json";
    
    public Dictionary<string, FileItem> LoadState(string directoryPath)
    {
        if (!File.Exists(StateFilePath)) return new Dictionary<string, FileItem>();
        var json = File.ReadAllText(StateFilePath);
        try
        {
            var stateStore = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, FileItem>>>(json);
            return stateStore?.GetValueOrDefault(GetStateKey(directoryPath)) ?? new();
        }
        catch (JsonException)
        {
            return new Dictionary<string, FileItem>();
        }
    }

    public void SaveState(string directoryPath, Dictionary<string, FileItem> state)
    {
        var stateStore = LoadStateStore();
        stateStore[GetStateKey(directoryPath)] = state;
        var json = JsonSerializer.Serialize(stateStore, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StateFilePath, json);    }

    private Dictionary<string, Dictionary<string, FileItem>> LoadStateStore()
    {
        if (!File.Exists(StateFilePath)) return new Dictionary<string, Dictionary<string, FileItem>>();
        var json = File.ReadAllText(StateFilePath);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, FileItem>>>(json) ?? new();
        }
        catch (JsonException)
        {
            return new Dictionary<string, Dictionary<string, FileItem>>();
        }
    }

    private static string GetStateKey(string directoryPath)
    {
        return Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
