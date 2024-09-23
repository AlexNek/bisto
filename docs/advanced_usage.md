# Advanced Usage

## Custom Storage Options

You can customize the storage behavior by providing `StorageOptions`:

```csharp
var options = new StorageOptions
{
    UseRoundedBlockSize = true,
    EntriesPerBlock = 1000,
    CacheSize = 1024 * 1024, // 1MB cache
    StreamInactivityTimeout = 10 // 10 minutes
};

var storage = await BinaryStorage.CreateAsync("mydata.bin", new FileStreamProvider(), options: options);
```

## Working with Root Blocks

BinaryStorage supports a special root block for storing important data:
```csharp
// Write root block
await storage.WriteRootBlockAsync(rootData);

// Read root block
byte[] rootData = await storage.ReadRootBlockAsync();
```
## Handling Inactivity

The system automatically manages stream resources based on inactivity:
```csharp
// Manually close stream if needed
await storage.CloseStreamIfNeededAsync();
```
## Logging

BinaryStorage integrates with Microsoft's `ILoggerFactory` interface. To enable logging:
```csharp
var loggerFactory = LoggerFactory.Create(builder => 
{
    builder.AddConsole();
    // Add other log providers as needed
});

var storage = await BinaryStorage.CreateAsync("mydata.bin", new FileStreamProvider(), loggerFactory: loggerFactory);
```

```csharp
Or with Serilog

services.AddLogging(configure => { configure.AddSerilog(); });
...
public MyStorage(
     IFileStreamProvider header,
     ILoggerFactory? loggerFactory,
     {
        // Initialization  
        ...
     }
```

## Error Handling

The system throws various exceptions for error conditions:

- `DataException`: For data integrity issues.
- `ArgumentNullException`: For null arguments.
- Other standard .NET exceptions as appropriate.

Always wrap operations in try-catch blocks and handle exceptions appropriately.

## Disposal

Proper disposal of BinaryStorage is crucial:

await storage.DisposeAsync();

This ensures all resources, including file streams and semaphores, are properly released.
