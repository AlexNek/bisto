namespace Bisto
{
    public class BinaryStorageHeaderInfo
    {
        public string Description { get; }

        //public int EntriesPerBlock { get; }

        public int FreeBlocksTableEntriesPerBlock { get; }

        public long FreeBlocksTableOffset { get; }

        public long RootUsedBlock { get; }

        public int Signature { get; }

        public StorageFlags StorageFlags { get; }

        public int Version { get; }

        // Internal constructor for BinaryStorage to create instances
        internal BinaryStorageHeaderInfo(BinaryStorageHeader header)
        {
            Signature = header.Signature;
            Version = header.Version;
            Description = header.Description;

            RootUsedBlock = header.RootUsedBlock;
            FreeBlocksTableOffset = header.FreeBlocksTableOffset;
            FreeBlocksTableEntriesPerBlock = header.FreeBlocksTableEntriesPerBlock;

            //EntriesPerBlock = header.FreeBlocksTableEntriesPerBlock;
            StorageFlags = header.StorageFlags;
        }

        // Private constructor to prevent external instantiation
        private BinaryStorageHeaderInfo()
        {
        }
    }
}
