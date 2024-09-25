# Bisto BinaryStorage Library
## Overview
The **Bisto** BinaryStorage library provides a block-based binary storage system with asynchronous operations for reading, writing, and deleting data.

BinaryStorage is a robust, thread-safe binary storage system designed for efficient data management in C# applications. It provides a flexible and performant solution for storing and retrieving binary data, with features such as free block management, journaling, and automatic stream management.

## Key Features

- **Asynchronous Operations**: All major operations are asynchronous, ensuring optimal performance in multi-threaded environments.
- **Block-based Operations**: Asynchronous read, write, and delete operations for binary data blocks. For different needs, you can choose between compact blocks and rounded blocks.
- **Free Block Management**: Efficiently manages and reuses free blocks to minimize fragmentation and optimize storage usage.
- **Root Block Handling**: Dedicated root block for easy access to important data.
- **Journaling**: Implements a journaling system to ensure data integrity and support recovery in case of unexpected shutdowns.
- **Automatic Stream Management**: Intelligently manages file streams, opening and closing them as needed to conserve system resources.
- **Thread-Safety**: Utilizes semaphores to ensure thread-safe operations for concurrent read and write access.
- **Customizable Storage Options**: Allows configuration of various storage parameters through `StorageOptions`.
- **Logging Integration**: Supports integration with logging framework for comprehensive system monitoring.


## Installation

You can install the `Bisto` BinaryStorage library via NuGet Package Manager or the .NET CLI.

### NuGet Package Manager

```bash
    Install-Package Bisto
```

### .NET CLI

```bash
    dotnet add package Bisto
```

### PackageReference

```bash
    <PackageReference Include="Bisto" Version="1.0.0" />
```
## Usage
### Basic Usage

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
### Usage over interface

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
## Use Cases Overview

The **Bisto BinaryStorage** library addresses a variety of data storage challenges in modern applications. 
It allows users to efficiently store, retrieve, and manage binary data in a block-based format with asynchronous operations. 
Common use cases include caching API responses, storing user session data, logging for auditing, managing file versions, and handling IoT device data. 

With features like free block management, journaling for crash recovery, and thread-safe access,
the library is ideal for tasks requiring optimized storage, fast access, and reliable data integrity in multi-threaded environments.

## More Info

Binary Storage [repository](https://github.com/AlexNek/bisto)

## History
- **V0.6.2** - Fix error when recreating existing file. Replace `LogInformation` with `LogDebug`
- **v0.6.0** - inital version

> **Note**: unlisted version, may not have relevant changes
