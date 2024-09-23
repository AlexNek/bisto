namespace Bisto.Tests;

public class MemoryFileStreamProvider : IFileStreamProvider
{
    private readonly Dictionary<string, MemoryStream> _streams = new();

    public Stream GetFileStream(
        string path, 
        FileMode mode = FileMode.OpenOrCreate, 
        FileAccess access = FileAccess.ReadWrite)
    {
        if (!_streams.TryGetValue(path, out var stream))
        {
            stream = new MemoryStream();
            _streams[path] = stream;
        }

        // Set the initial position based on the FileMode
        if (mode == FileMode.Append)
        {
            stream.Seek(0, SeekOrigin.End);
        }
        else
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        return stream;
    }

    // Add a method to access a specific stream for assertions
    public MemoryStream GetStream(string path)
    {
        if (_streams.TryGetValue(path, out var stream))
        {
            return stream;
        }

        throw new ArgumentException($"No stream found for path: {path}");
    }
    public void Dispose()
    {
        foreach (var stream in _streams.Values)
        {
            stream.Dispose();
        }
        _streams.Clear();
    }
}
