using Bisto.Helpers;

using MemoryPack;

namespace Bisto.Journal;

[MemoryPackable]
public sealed partial class JournalEntry
{
    public int BlockSize { get; }

    public int DataSize { get; }

    public long Offset { get; }

    public EJournalOperation Operation { get; }

    public CompactTimeOffset TimeOffset { get; }

    [MemoryPackConstructor]
    public JournalEntry(
        CompactTimeOffset timeOffset,
        EJournalOperation operation,
        long offset,
        int blockSize,
        int dataSize = 0)
    {
        Operation = operation;
        Offset = offset;
        BlockSize = blockSize;
        DataSize = dataSize;
        TimeOffset = timeOffset;
    }

    public static JournalEntry Create(
        DateTime journalCreationTime,
        EJournalOperation operation,
        long offset,
        int blockSize,
        int dataSize = 0)
    {
        var timeOffset = new CompactTimeOffset(DateTime.UtcNow - journalCreationTime);
        return new JournalEntry(timeOffset, operation, offset, blockSize, dataSize);
    }
}

/*
 too must problem with data hash. Calculation, reference, deletion, big files sizes etc.
    public byte[] Hash { get; }

 Variable-length hash: While fixed-size entries are ideal for performance, you can still accommodate a variable-length hash field:

   Option 1: Use a fixed maximum size for the hash field, padding shorter hashes with zeros.
   Option 2: Store the hash separately and include a reference (e.g., an offset) in the fixed-size entry.

 *
 */
