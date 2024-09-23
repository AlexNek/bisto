namespace Bisto;

public interface IFileStreamProvider:IDisposable
{
    Stream GetFileStream(
        string path,
        FileMode mode = FileMode.OpenOrCreate,
        FileAccess access = FileAccess.ReadWrite);
}
