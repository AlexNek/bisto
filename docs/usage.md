# Basic Usage

1. Create a new BinaryStorage instance:
```csharp
var storage = await BinaryStorage.CreateAsync("mydata.bin", new FileStreamProvider());
```

2. Write data:
```csharp
byte[] data = // ... your data ...
long address = await storage.WriteAsync(data);
```

3. Read data:
```csharp
byte[] retrievedData = await storage.ReadAsync(address);
```

4. Delete data:
```csharp
await storage.DeleteAsync(address);
```
# Usage over interface

Here is an example of how to use the `IBinaryStorage` interface to manage binary data storage:

```csharp
    using Bisto;

    public class Example
    {
        private readonly IBinaryStorage _storage;

        public Example(IBinaryStorage storage)
        {
            _storage = storage;
        }

        public async Task UseStorageAsync()
        {
            // Writing data
            byte[] data = new byte[] { 0x01, 0x02, 0x03 };
            long offset = await _storage.WriteAsync(data);

            // Reading data
            byte[]? readData = await _storage.ReadAsync(offset);

            // Getting block size
            int size = await _storage.ReadDataSizeAsync(offset);

            // Deleting a block
            await _storage.DeleteAsync(offset);

            // get Free block map
            List<(long Offset, int Size)> freeBlocks = await _storage.GetFreeBlockMapAsync();
        }
    }
```
