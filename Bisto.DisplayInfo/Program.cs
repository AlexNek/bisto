using Bisto;
using Bisto.DisplayInfo;
using Bisto.Journal;

internal class Program
{
    public static bool IsFileLocked(string filePath, out string foundLockers)
    {
        foundLockers = string.Empty;
        // Attempt to open the file exclusively (for reading)
        // This will throw an IOException if the file is already locked by another process
        try
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                // If we reach here, the file is not locked
                return false;
            }
        }
        catch (IOException ex)
        {
            // Check for specific error codes related to file locking
            // Error code 32 (ERROR_SHARING_VIOLATION) is common for file locks
            const int errorSharingViolation = 32;
            if (ex.HResult == errorSharingViolation)
            {
                var lockers = LockedFileInfoHelper.FindLockers(filePath);
                // Extract process names into a list of strings
                List<string> processNames = lockers.Select(p => p.ProcessName).ToList();
                foundLockers = String.Join(", ", processNames);
                return true; // The file is locked
            }

            // Handle other IOExceptions as needed
            // ...
            return false; // Or rethrow if you want to propagate the exception
        }
    }

    private static async Task<bool> DisplayJournalInfo(string journalFileName)
    {
        // Check if the journal file exists
        if (!File.Exists(journalFileName))
        {
            Console.WriteLine($"Journal file '{journalFileName}' not found.");
            return false;
        }

        if (IsFileLocked(journalFileName, out string foundLockers))
        {
            Console.WriteLine($"The journal file is currently locked by another process {foundLockers}");
            return false;
        }

        // Open the journal file and read its content
        using (var journalStream = new FileStream(journalFileName, FileMode.Open, FileAccess.Read))
        {
            var journalManager = new JournalManager(journalStream);
            Console.WriteLine("\n=== Journal Information ===");
            Console.WriteLine($"Journal for file: {journalFileName}");
            Console.WriteLine($"Journal Description: {journalManager.Description}");
            Console.WriteLine($"Journal Version: {journalManager.Version}");
            Console.WriteLine($"Journal Created On: {journalManager.CreationTime}");
            Console.WriteLine($"Journal Last Modified On: {journalManager.LastModifiedTime}");
            Console.WriteLine($"Journal Entry Count: {journalManager.EntryCount}");
            Console.WriteLine($"Journal Size: {journalManager.Size} bytes");

            // Read all entries
            var allEntries = await journalManager.ReadAllEntriesAsync();

            // Separate committed and uncommitted entries
            var committedEntries = allEntries.Where(entry => entry.IsCommitted).ToList();
            var uncommittedEntries = allEntries.Where(entry => !entry.IsCommitted).ToList();

            // Display committed entries as "info"
            if (committedEntries.Count > 0)
            {
                Console.WriteLine("Committed Entries (Info):");
                Console.WriteLine("-----------------------------------------------------");
                foreach (var entry in committedEntries)
                {
                    Console.WriteLine(
                        $"Operation: {entry.Entry.Operation}, " +
                        $"Offset: {entry.Entry.Offset}, " +
                        $"Block Size: {entry.Entry.BlockSize}, " +
                        $"Data Size: {entry.Entry.DataSize}, " +
                        $"TimeOffset: {entry.Entry.TimeOffset.ToTimeSpan()}");
                }
            }
            else
            {
                Console.WriteLine("No committed entries found.");
            }

            // Display uncommitted entries as "error"
            if (uncommittedEntries.Count > 0)
            {
                Console.WriteLine("\nUncommitted Entries (Error):");
                Console.WriteLine("-----------------------------------------------------");
                foreach (var entry in uncommittedEntries)
                {
                    Console.WriteLine(
                        $"Operation: {entry.Entry.Operation}, " +
                        $"Offset: {entry.Entry.Offset}, " +
                        $"Block Size: {entry.Entry.BlockSize}, " +
                        $"Data Size: {entry.Entry.DataSize}, " +
                        $"TimeOffset: {entry.Entry.TimeOffset.ToTimeSpan()}");
                }
            }
            else
            {
                Console.WriteLine("\nNo uncommitted entries found.");
            }
        }

        return true;
    }

    private static async Task<bool> DisplayStorageInfo(string fileName)
    {
        if (!File.Exists(fileName))
        {
            Console.WriteLine($"Storage file '{fileName}' not found.");
            return false;
        }

        if (IsFileLocked(fileName, out string foundLockers))
        {
            Console.WriteLine($"The journal file is currently locked by another process {foundLockers}");
            return false;
        }

        var binaryStorage = await BinaryStorage.CreateAsync(fileName, new FileStreamProvider());
        var freeBlocks = await binaryStorage.GetFreeBlockMapAsync();
        var headerInfo = binaryStorage.GetHeaderRo();

        Console.WriteLine("\n=== Header Information ===");
        Console.WriteLine($"Signature: {headerInfo.Signature}");
        Console.WriteLine($"Version: {headerInfo.Version}");
        Console.WriteLine($"Description: {headerInfo.Description}");
        //Console.WriteLine($"FirstFreeBlock: {headerInfo.FirstFreeBlock}");
        Console.WriteLine($"RootUsedBlock: {headerInfo.RootUsedBlock}");
        Console.WriteLine($"{nameof(headerInfo.FreeBlocksTableOffset)}: {headerInfo.FreeBlocksTableOffset}");
        Console.WriteLine($"{nameof(headerInfo.FreeBlocksTableEntriesPerBlock)}: {headerInfo.FreeBlocksTableEntriesPerBlock}");
        //Console.WriteLine($"EntriesPerBlock: {headerInfo.EntriesPerBlock}");
        Console.WriteLine($"{nameof(headerInfo.StorageFlags)}: {headerInfo.StorageFlags}");

        Console.WriteLine("\n=== Free Block Map ===");
        if (freeBlocks.Any())
        {
            foreach (var freeBlock in freeBlocks)
            {
                Console.WriteLine($"Offset: {freeBlock.Offset}, Size: {freeBlock.Size}");
            }
        }
        else
        {
            Console.WriteLine("No free blocks available.");
        }

        return true;
    }

    private static async Task Main(string[] args)
    {
        // Check if the file name is passed as an argument
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide a file name.");
            return;
        }
        string currentDirectory = Directory.GetCurrentDirectory();
        Console.WriteLine("Current directory: " + currentDirectory);
        // Get the file name from the argument
        string fileName = args[0];

        var directoryName = Path.GetDirectoryName(fileName);
        if (string.IsNullOrEmpty(directoryName))
        {
            fileName = Path.Combine(currentDirectory, fileName);
        }
        // Replace the file extension with ".journal"
        string journalFileName = Path.ChangeExtension(fileName, ".journal");

        try
        {
            await DisplayJournalInfo(journalFileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine("journal Error: " + ex.Message);
        }

        try
        {
            await DisplayStorageInfo(fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Binary storage Error: " + ex.Message);
        }
    }
}
