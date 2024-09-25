using System.Text;

namespace Bisto;

internal class Program
{
    public static async Task FreeBlockMapReportAsync(IBinaryStorage storage, string description)
    {
        var freeBlocksMap = await storage.GetFreeBlockMapAsync();

        var reportBuilder = new StringBuilder();
        reportBuilder.AppendLine($"Free Block Map Report {description}");
        reportBuilder.AppendLine("=====================");
        reportBuilder.AppendLine($"Total Free Blocks: {freeBlocksMap.Count}");
        reportBuilder.AppendLine();

        foreach (var block in freeBlocksMap)
        {
            reportBuilder.AppendLine($"Offset: {block.Offset}, Size: {block.Size} bytes");
        }

        reportBuilder.AppendLine("=====================");
        reportBuilder.AppendLine("End of Report");

        Console.WriteLine(reportBuilder.ToString());
    }

    private static async Task Main(string[] args)
    {
        Console.WriteLine("Binary Storage System Demo");
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string relativePath = "data/test_storage.bin";
        string fullPath = Path.Combine(baseDirectory, relativePath);
        string directoryPath = Path.GetDirectoryName(fullPath);

        if (Directory.Exists(directoryPath))
        {
            Console.WriteLine($"Directory exists: {directoryPath}");
        }
        else
        {
            Console.WriteLine($"Directory does not exist, create: {directoryPath}");
            Directory.CreateDirectory(directoryPath);
        }

        if (File.Exists(fullPath))
        {
            Console.WriteLine($"File Exists, overwrite: {fullPath}");
        }
        await using (IBinaryStorage storage = await BinaryStorage.CreateAsync(fullPath, new FileStreamProvider(),true))
        {
            Console.WriteLine("BinaryStorage Demo");
            await FreeBlockMapReportAsync(storage,"Start");
            
            byte[] data1 = Encoding.UTF8.GetBytes("Hello, World!");
            long offset1 = await storage.WriteAsync(data1);
            Console.WriteLine($"Wrote 'Hello, World!' at offset: {offset1}");

            byte[] data2 = Encoding.UTF8.GetBytes("This is another piece of data.");
            long offset2 = await storage.WriteAsync(data2);
            Console.WriteLine($"Wrote 'This is another piece of data.' at offset: {offset2}");

            byte[]? readData1 = await storage.ReadAsync(offset1);
            Console.WriteLine($"Read from offset {offset1}: {Encoding.UTF8.GetString(readData1)}");

            byte[]? readData2 = await storage.ReadAsync(offset2);
            Console.WriteLine($"Read from offset {offset2}: {Encoding.UTF8.GetString(readData2)}");

            byte[] rootData = Encoding.UTF8.GetBytes("This is the root block data.");
            await storage.WriteRootBlockAsync(rootData);
            Console.WriteLine("Wrote root block data");

            byte[]? readRootData = await storage.ReadRootBlockAsync();
            if (readRootData != null)
            {
                Console.WriteLine($"Read root block: {Encoding.UTF8.GetString(readRootData)}");
            }
            else
            {
                Console.WriteLine("No root block found");
            }

            byte[] updatedRootData = Encoding.UTF8.GetBytes("This is updated root block data.");
            await storage.WriteRootBlockAsync(updatedRootData);
            Console.WriteLine("Updated root block data");

            byte[]? readUpdatedRootData = await storage.ReadRootBlockAsync();
            Console.WriteLine($"Read updated root block: {Encoding.UTF8.GetString(readUpdatedRootData)}");

            await storage.DeleteAsync(offset1);
            Console.WriteLine($"Deleted data at offset: {offset1}");

            await storage.DeleteAsync(offset2);
            Console.WriteLine($"Deleted data at offset: {offset2}");

            await FreeBlockMapReportAsync(storage, "After Deletions");
            
            byte[]? deletedData1 = await storage.ReadAsync(offset1);
            Console.WriteLine(
                $"Attempted to read deleted data from offset {offset1}: {Encoding.UTF8.GetString(deletedData1)}");

            byte[] data3 = Encoding.UTF8.GetBytes("This is new data after deletions.");
            long offset3 = await storage.WriteAsync(data3);
            Console.WriteLine($"Wrote 'This is new data after deletions.' at offset: {offset3}");

            byte[]? readData3 = await storage.ReadAsync(offset3);
            Console.WriteLine($"Read from offset {offset3}: {Encoding.UTF8.GetString(readData3)}");
            
            await FreeBlockMapReportAsync(storage,"After Write");
        }
    }
}
