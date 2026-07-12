namespace FrogPlug.Agent;

public interface IFileSystem
{
    bool FileExists(string path);
    Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default);
    Task WriteTextAsync(string path, string content, CancellationToken cancellationToken = default);
    void CopyFile(string source, string destination, bool overwrite);
    void MoveFile(string source, string destination, bool overwrite);
    void DeleteFile(string path);
    void CreateDirectory(string path);
    string GetTempFileName();
    byte[] ReadAllBytes(string path);
    void WriteAllBytes(string path, byte[] data);
    long GetFileLength(string path);
}

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default) => File.ReadAllTextAsync(path, cancellationToken);
    public Task WriteTextAsync(
        string path,
        string content,
        CancellationToken cancellationToken = default)
        => File.WriteAllTextAsync(path, content, cancellationToken);
    public void CopyFile(string source, string destination, bool overwrite) => File.Copy(source, destination, overwrite);
    public void MoveFile(string source, string destination, bool overwrite) => File.Move(source, destination, overwrite);
    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public string GetTempFileName() => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    public void WriteAllBytes(string path, byte[] data) => File.WriteAllBytes(path, data);
    public long GetFileLength(string path) => new FileInfo(path).Length;
}
