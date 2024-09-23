namespace Bisto;

public static class BlockUtils
{
    public const int BlockHeaderSize = 10; // 4 (dataSize) + 4 (blockSize) + 2 (state)

    public enum BlockState : short
    {
        Used = 0,
        Deleted = 1,
        Zipped = 2,
        FreeBlocksTable = 3,
        /// <summary>
        /// Temporary state for divided free block
        /// </summary>
        Allocated = 4
    }

    public record BlockHeader(int DataSize, int BlockSize, BlockState State);

    public static async Task WriteBlockHeaderAsync(
        Stream stream,
        BlockHeader header,
        CancellationToken cancellationToken = default)
    {
        byte[] buffer = new byte[BlockHeaderSize];
        BitConverter.GetBytes(header.DataSize).CopyTo(buffer, 0);
        BitConverter.GetBytes(header.BlockSize).CopyTo(buffer, 4);
        BitConverter.GetBytes((short)header.State).CopyTo(buffer, 8);

        await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<BlockHeader> ReadBlockHeaderAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        byte[] buffer = new byte[BlockHeaderSize];
        await stream.ReadAsync(buffer, 0, BlockHeaderSize, cancellationToken);

        int dataSize = BitConverter.ToInt32(buffer, 0);
        int blockSize = BitConverter.ToInt32(buffer, 4);
        BlockState state = (BlockState)BitConverter.ToInt16(buffer, 8);

        return new BlockHeader(dataSize, blockSize, state);
    }
    
    // Helper function to round up to the nearest power of 2
    public static int RoundUpToPowerOfTwo(int n)
    {
        if (n <= 0) return 1; // Handle 0 and negative values appropriately
        n--;
        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;
        n++;
        return n;
    }
}
