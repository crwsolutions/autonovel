namespace Autonovel.Core.Services;

public interface IFileManager
{
    string ReadFile(string relativePath);
    void WriteFile(string relativePath, string content);
    Task<string> ReadFileAsync(string relativePath, CancellationToken ct = default);
    Task WriteFileAsync(string relativePath, string content, CancellationToken ct = default);
    bool FileExists(string relativePath);
    List<string> ListChapters();
    Task<List<int>> ListChaptersAsync();
    string ReadChapter(int chapterNum);
    void WriteChapter(int chapterNum, string content);
    int CountWordsInChapters();
}

public class FileManager : IFileManager
{
    private readonly string _baseDirectory;
    private readonly string _chaptersDirectory;

    public FileManager(string baseDirectory, string chaptersDirectory)
    {
        _baseDirectory = baseDirectory;
        _chaptersDirectory = Path.Combine(baseDirectory, chaptersDirectory);
        Directory.CreateDirectory(chaptersDirectory);
    }

    public string ReadFile(string relativePath)
    {
        Console.WriteLine($"Reading file: {relativePath}");
        var fullPath = Path.Combine(_baseDirectory, relativePath);
        try
        {
            return File.ReadAllText(fullPath);
        }
        catch (FileNotFoundException)
        {
            return "";
        }
    }

    public void WriteFile(string relativePath, string content)
    {
        Console.WriteLine($"Writing file: {relativePath}");
        var fullPath = Path.Combine(_baseDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, content);
    }

    public bool FileExists(string relativePath) => 
        File.Exists(Path.Combine(_baseDirectory, relativePath));

    public List<string> ListChapters()
    {
        if (!Directory.Exists(_chaptersDirectory))
            return [];
        
        return Directory.GetFiles(_chaptersDirectory, "ch_*.md")
            .Select(Path.GetFileName)
            .OrderBy(n => n)
            .ToList()!;
    }

    public string ReadChapter(int chapterNum) => 
        ReadFile(Path.Combine(_chaptersDirectory, $"ch_{chapterNum:02d}.md"));

    public void WriteChapter(int chapterNum, string content) =>
        WriteFile(Path.Combine(_chaptersDirectory, $"ch_{chapterNum:02d}.md"), content);

    public int CountWordsInChapters()
    {
        if (!Directory.Exists(_chaptersDirectory))
            return 0;
        
        return Directory.GetFiles(_chaptersDirectory, "ch_*.md")
            .Sum(f => File.ReadAllText(f).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    public async Task<string> ReadFileAsync(string relativePath, CancellationToken ct = default)
    {
        await Task.Yield(); // Async boundary
        return ReadFile(relativePath);
    }

    public async Task WriteFileAsync(string relativePath, string content, CancellationToken ct = default)
    {
        await Task.Yield(); // Async boundary
        WriteFile(relativePath, content);
    }

    public async Task<List<int>> ListChaptersAsync()
    {
        await Task.Yield(); // Async boundary
        return ListChapters()
            .Select(n => int.Parse(Path.GetFileNameWithoutExtension(n).Replace("ch_", "")))
            .ToList();
    }
}