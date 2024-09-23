using System.Runtime.InteropServices;
using System.Text;

namespace Bisto
{
    // Bitwise enum to define storage options
    [Flags]
    public enum StorageFlags
    {
        None = 0,

        UseRoundedBlockSize = 1
    }

    internal class BinaryStorageHeader
    {
        private const int ActualVersion = 1;

        private const string ConstDescription = "BinaryStorage V1";

        public const int HeaderSize = 80;

        private const int MaxDescriptionSize = 16;

        private const int SignatureId = 0x534E4942; // 'BINS' in ASCII

        private HeaderStruct _header;

        // Properties to expose the header's data fields
        public string Description => Encoding.UTF8.GetString(_header.Description).TrimEnd('\0');

        //public int EntriesPerBlock
        //{
        //    get => _header.EntriesPerBlock;
        //    set => _header.EntriesPerBlock = value;
        //}

        //public long FirstFreeBlock
        //{
        //    get => _header.FirstFreeBlock;
        //    set => _header.FirstFreeBlock = value;
        //}

        public virtual int FreeBlocksTableEntriesPerBlock
        {
            get => _header.FreeBlocksTableEntriesPerBlock;
            set => _header.FreeBlocksTableEntriesPerBlock = value;
        }

        public virtual long FreeBlocksTableOffset
        {
            get => _header.FreeBlocksTableOffset;
            set => _header.FreeBlocksTableOffset = value;
        }

        public long RootUsedBlock
        {
            get => _header.RootUsedBlock;
            set => _header.RootUsedBlock = value;
        }

        public int Signature => _header.Signature;

        public StorageFlags StorageFlags
        {
            get => (StorageFlags)_header.Flags;
            set => _header.Flags = (int)value;
        }

        public int Version => _header.Version;

        // Constructor to initialize the header, if needed
        public BinaryStorageHeader(bool initialize = false)
        {
            if (initialize)
            {
                _header = new HeaderStruct
                              {
                                  Signature = SignatureId,
                                  Version = ActualVersion,
                                  Description =
                                      Encoding.UTF8.GetBytes(ConstDescription.PadRight(MaxDescriptionSize, '\0')),
                                  RootUsedBlock = 0,
                                  FreeBlocksTableEntriesPerBlock = 256, // Default value
                                  Flags = (int)StorageFlags.UseRoundedBlockSize // Default to true
                              };
            }
        }

        // Async method to read the header from a stream
        public static async Task<BinaryStorageHeader> ReadFromStreamAsync(Stream stream)
        {
            int size = Marshal.SizeOf<HeaderStruct>();
            if (size > HeaderSize)
            {
                throw new InvalidDataException(
                    $"Not enough data to read the full header. Expected max {HeaderSize}, but read {size}");
            }

            byte[] buffer = new byte[size];
            stream.Seek(0, SeekOrigin.Begin);
            await stream.ReadAsync(buffer, 0, size);

            HeaderStruct headerStruct = SerializationUtils.BytesToStructure<HeaderStruct>(buffer);

            return new BinaryStorageHeader { _header = headerStruct };
        }

        // Method to validate the header
        public void Validate()
        {
            if (Signature != SignatureId)
            {
                throw new InvalidDataException("Invalid file signature");
            }

            if (Version != ActualVersion)
            {
                throw new InvalidDataException($"Unsupported file version: {Version}");
            }

            if (Description != ConstDescription)
            {
                throw new InvalidDataException("Invalid file description");
            }

            //if (options is not null)
            {
                //will not be changed, only for the new file. Don't need to check

                //if (options.EntriesPerBlock != FreeBlocksTableEntriesPerBlock)
                //{
                //    throw new InvalidDataException(
                //        $"Cannot change entries per block from {FreeBlocksTableEntriesPerBlock} to {options.EntriesPerBlock}");
                //}

                //if (options.UseRoundedBlockSize != StorageFlags.HasFlag(StorageFlags.UseRoundedBlockSize))
                //{
                //    throw new InvalidDataException(
                //        $"Cannot change use rounded block size from {StorageFlags} to {options.UseRoundedBlockSize}");
                //}
            }
        }

        // Async method to write the header back to the stream
        public async Task WriteToStreamAsync(Stream? stream, CancellationToken cancellationToken = default)
        {
            int size = Marshal.SizeOf<HeaderStruct>();
            if (size > HeaderSize)
            {
                throw new InvalidOperationException(
                    $"Header size mismatch: Expected {HeaderSize}, but calculated {size}");
            }

            // Initialize a buffer with HeaderSize length
            byte[] buffer = new byte[HeaderSize];

            // Serialize the structure to a temporary buffer
            byte[] tempBuffer = SerializationUtils.StructureToBytes(_header);

            // Copy the serialized data to the buffer
            Array.Copy(tempBuffer, buffer, tempBuffer.Length);

            if (stream != null)
            {
                // Ensure the stream position is at the beginning
                stream.Seek(0, SeekOrigin.Begin);

                // Write HeaderSize bytes to the stream
                await stream.WriteAsync(buffer, 0, HeaderSize, cancellationToken);

                // Flush the stream
                await stream.FlushAsync(cancellationToken);
            }
        }

        // Struct to define the structure of the binary storage header
        [StructLayout(LayoutKind.Sequential)]
        private struct HeaderStruct
        {
            public int Signature; // 4 bytes

            public int Version; // 4 bytes

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxDescriptionSize)]
            public byte[] Description; // 16 bytes

            //public long FirstFreeBlock; // 8 bytes

            public long RootUsedBlock; // 8 bytes

            public long FreeBlocksTableOffset; // 8 bytes

            public int FreeBlocksTableEntriesPerBlock; // 4 bytes

            //public int EntriesPerBlock; // 4 bytes

            public int Flags; // 4 bytes (to store bitwise enum)

            public int Reserved1; // 4 bytes reserved

            public int Reserved2; // 4 bytes reserved

            public long Reserved3; // 8 bytes reserved

            public long Reserved4; // 8 bytes reserved

            public int Reserved5; // 4 bytes reserved
        }
    }
}
