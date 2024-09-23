﻿namespace Bisto;

public class FileStreamProvider : IFileStreamProvider
{
    private readonly Dictionary<string, FileStream> _streams = new();

    public void Dispose()
    {
        foreach (var stream in _streams.Values)
        {
            if (!IsStreamDisposedOrClosed(stream))
            {
                stream.Dispose();
            }
        }

        _streams.Clear();
    }

    public Stream GetFileStream(
        string path,
        FileMode mode = FileMode.OpenOrCreate,
        FileAccess access = FileAccess.ReadWrite)
    {
        if (!_streams.TryGetValue(path, out var stream))
        {
            stream = new FileStream(path, mode, access, FileShare.Read);
            _streams[path] = stream;
        }

        return stream;
    }

    public static bool IsStreamDisposedOrClosed(Stream? stream)
    {
        if (stream == null)
        {
            return true;
        }

        try
        {
            // Check if the stream supports reading or writing
            return !(stream.CanRead || stream.CanWrite);
        }
        catch (ObjectDisposedException)
        {
            // If accessing CanRead or CanWrite throws ObjectDisposedException, the stream is disposed
            return true;
        }
        catch (IOException)
        {
            // An IOException might indicate that the stream is closed
            return true;
        }
    }
}