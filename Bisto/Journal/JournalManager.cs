using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using MemoryPack;

namespace Bisto.Journal;

public class JournalManager: IAsyncDisposable
{
    private const string ConstDescription = "Journal V1";

    public const int HeaderSize = 64;

    private const int MaxDescriptionSize = 16;

    private const int SignatureId = 0x4C4E524A; // 'JRNL' in ASCII

    private readonly ConcurrentQueue<JournalEntry> _journal = new();

    private readonly Stream _journalStream;

    private JournalHeader _header;

    private long _journalSize;

    public DateTime CreationTime => DateTime.FromFileTimeUtc(_header.CreationTime);

    public string Description => Encoding.UTF8.GetString(_header.Description).TrimEnd('\0');

    public long EntryCount => _header.EntryCount;

    public DateTime LastModifiedTime => DateTime.FromFileTimeUtc(_header.LastModifiedTime);

    public long Size => _journalSize;

    public int Version => _header.Version;

    public JournalManager(Stream journalStream)
    {
        _journalStream = journalStream;
        _journalSize = _journalStream.Length;

        if (_journalSize == 0)
        {
            InitializeNewJournal();
        }
        else
        {
            ReadHeader();
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        _journalStream.Seek(HeaderSize, SeekOrigin.Begin);
        long position = HeaderSize;

        while (position < _journalSize)
        {
            // Read entry length
            byte[] lengthBytes = new byte[sizeof(int)];
            await _journalStream.ReadAsync(lengthBytes, cancellationToken);
            int entryLength = BitConverter.ToInt32(lengthBytes);

            // Skip the entry data
            _journalStream.Seek(entryLength, SeekOrigin.Current);

            // Mark as committed
            await _journalStream.WriteAsync(BitConverter.GetBytes(true), cancellationToken);

            position += sizeof(int) + entryLength + sizeof(bool);
        }

        await _journalStream.FlushAsync(cancellationToken);
    }

    public async Task LogOperationAsync(
        EJournalOperation operation,
        long offset,
        int blockSize,
        int dataSize,
        CancellationToken cancellationToken = default)
    {
        var entry = JournalEntry.Create(CreationTime, operation, offset, blockSize, dataSize);
        _journal.Enqueue(entry);
        await WriteJournalEntryAsync(entry, cancellationToken);
        _header.EntryCount++;
        UpdateLastModifiedTime();
    }

    public async Task<List<(JournalEntry Entry, bool IsCommitted)>> ReadAllEntriesAsync()
    {
        var entries = new List<(JournalEntry Entry, bool IsCommitted)>();
        _journalStream.Seek(HeaderSize, SeekOrigin.Begin);

        while (_journalStream.Position < _journalSize)
        {
            // Read entry length
            byte[] lengthBytes = new byte[sizeof(int)];
            await _journalStream.ReadAsync(lengthBytes);
            int entryLength = BitConverter.ToInt32(lengthBytes);

            // Read entry data
            byte[] entryBytes = new byte[entryLength];
            await _journalStream.ReadAsync(entryBytes);

            // Read committed flag
            byte[] committedBytes = new byte[sizeof(bool)];
            await _journalStream.ReadAsync(committedBytes);
            bool isCommitted = BitConverter.ToBoolean(committedBytes);

            var entry = MemoryPackSerializer.Deserialize<JournalEntry>(entryBytes);

            entries.Add((entry, isCommitted));
        }

        return entries;
    }

    public async Task<List<JournalEntry>> ReadUncommittedEntriesAsync()
    {
        var uncommittedEntries = new List<JournalEntry>();
        _journalStream.Seek(HeaderSize, SeekOrigin.Begin);
        long position = HeaderSize;

        while (position < _journalSize)
        {
            // Read entry length
            byte[] lengthBytes = new byte[sizeof(int)];
            await _journalStream.ReadAsync(lengthBytes);
            int entryLength = BitConverter.ToInt32(lengthBytes);

            // Read entry data
            byte[] entryBytes = new byte[entryLength];
            await _journalStream.ReadAsync(entryBytes);

            // Read committed flag
            byte[] committedBytes = new byte[sizeof(bool)];
            await _journalStream.ReadAsync(committedBytes);
            bool isCommitted = BitConverter.ToBoolean(committedBytes);

            if (!isCommitted)
            {
                var entry = MemoryPackSerializer.Deserialize<JournalEntry>(entryBytes);
                uncommittedEntries.Add(entry);
            }

            position += sizeof(int) + entryLength + sizeof(bool);
        }

        return uncommittedEntries;
    }

    private int GetJournalEntrySize()
    {
        return sizeof(int) + sizeof(int) + sizeof(long) + sizeof(EJournalOperation) + sizeof(uint);
    }

    private void InitializeNewJournal()
    {
        _header = new JournalHeader
                      {
                          SignatureId = SignatureId,
                          Description = new byte[16],
                          Version = 1,
                          CreationTime = DateTime.UtcNow.ToFileTimeUtc(),
                          LastModifiedTime = DateTime.UtcNow.ToFileTimeUtc(),
                          EntryCount = 0,
                          Reserved = 0
                      };

        byte[] descriptionBytes = Encoding.UTF8.GetBytes(ConstDescription.PadRight(16, '\0'));
        Array.Copy(descriptionBytes, _header.Description, 16);

        WriteHeader();
        _journalSize = HeaderSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct JournalHeader
    {
        public int SignatureId;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Description;

        public int Version;

        public long CreationTime;

        public long LastModifiedTime;

        public long EntryCount;

        public int Reserved; // For future use
    }

    private async Task MarkEntryCommittedAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        long entryPosition = _journalSize - (GetJournalEntrySize() + sizeof(bool));
        _journalStream.Seek(entryPosition + GetJournalEntrySize(), SeekOrigin.Begin);

        using (var writer = new BinaryWriter(_journalStream, Encoding.UTF8, true))
        {
            writer.Write(true); // Mark as committed
        }

        await _journalStream.FlushAsync(cancellationToken);
    }

    //private void ReadHeader()
    //{
    //    _journalStream.Seek(0, SeekOrigin.Begin);
    //    byte[] headerBytes = new byte[HeaderSize];
    //    _journalStream.Read(headerBytes, 0, HeaderSize);
    //    _header = MemoryMarshal.Read<JournalHeader>(headerBytes);

    //    if (_header.SignatureId != SignatureId)
    //    {
    //        throw new InvalidDataException("Invalid journal file signature.");
    //    }
    //}

    private void ReadHeader()
    {
        _journalStream.Seek(0, SeekOrigin.Begin);

        byte[] headerBytes = new byte[HeaderSize];
        _journalStream.Read(headerBytes, 0, headerBytes.Length);

        int position = 0;

        // Read SignatureId
        _header.SignatureId = BitConverter.ToInt32(headerBytes, position);
        position += sizeof(int);

        if (_header.SignatureId != SignatureId)
        {
            throw new InvalidDataException("Invalid journal file signature.");
        }

        // Read Description
        _header.Description = new byte[16];
        Array.Copy(headerBytes, position, _header.Description, 0, 16);
        position += 16;

        // Read the rest of the header
        _header.Version = BitConverter.ToInt32(headerBytes, position);
        position += sizeof(int);
        _header.CreationTime = BitConverter.ToInt64(headerBytes, position);
        position += sizeof(long);
        _header.LastModifiedTime = BitConverter.ToInt64(headerBytes, position);
        position += sizeof(long);
        _header.EntryCount = BitConverter.ToInt64(headerBytes, position);
        position += sizeof(long);
        _header.Reserved = BitConverter.ToInt32(headerBytes, position);
    }

    private void UpdateLastModifiedTime()
    {
        _header.LastModifiedTime = DateTime.UtcNow.ToFileTimeUtc();
        WriteHeader();
    }

    //private void WriteHeader()
    //{
    //    _journalStream.Seek(0, SeekOrigin.Begin);
    //    Span<byte> headerBytes = stackalloc byte[HeaderSize];
    //    MemoryMarshal.Write(headerBytes, ref _header);
    //    _journalStream.Write(headerBytes);
    //    _journalStream.Flush();
    //}

    private void WriteHeader()
    {
        _journalStream.Seek(0, SeekOrigin.Begin);

        byte[] headerBytes = new byte[HeaderSize];
        int position = 0;

        // Write SignatureId
        BitConverter.GetBytes(_header.SignatureId).CopyTo(headerBytes, position);
        position += sizeof(int);

        // Write Description
        _header.Description.CopyTo(headerBytes, position);
        position += 16;

        // Write the rest of the header
        BitConverter.GetBytes(_header.Version).CopyTo(headerBytes, position);
        position += sizeof(int);
        BitConverter.GetBytes(_header.CreationTime).CopyTo(headerBytes, position);
        position += sizeof(long);
        BitConverter.GetBytes(_header.LastModifiedTime).CopyTo(headerBytes, position);
        position += sizeof(long);
        BitConverter.GetBytes(_header.EntryCount).CopyTo(headerBytes, position);
        position += sizeof(long);
        BitConverter.GetBytes(_header.Reserved).CopyTo(headerBytes, position);

        _journalStream.Write(headerBytes, 0, headerBytes.Length);
        _journalStream.Flush();
    }

    private async Task WriteJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        _journalStream.Seek(_journalSize, SeekOrigin.Begin);

        var entryBytes = MemoryPackSerializer.Serialize(entry);

        // Write the length of the serialized entry
        await _journalStream.WriteAsync(BitConverter.GetBytes(entryBytes.Length), cancellationToken);

        // Write the serialized entry
        await _journalStream.WriteAsync(entryBytes, cancellationToken);

        // Write the committed flag
        await _journalStream.WriteAsync(BitConverter.GetBytes(false), cancellationToken);

        _journalSize += sizeof(int) + entryBytes.Length + sizeof(bool);
        await _journalStream.FlushAsync(cancellationToken);
    }

    

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources asynchronously.</summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await _journalStream.FlushAsync();
        await _journalStream.DisposeAsync();
    }
}
