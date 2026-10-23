// Dossier temporaire supprimé à la fin du test.
sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "msm-test-" + Guid.NewGuid())).FullName;

    public string File(string name, string content = "")
    {
        var path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public string Dir(params string[] parts) => Directory.CreateDirectory(System.IO.Path.Combine([Path, .. parts])).FullName;

    public void Dispose() => Directory.Delete(Path, true);
}
