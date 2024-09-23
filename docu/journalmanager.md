# JournalManager Class

## Table of Contents
- [Overview](#overview)
- [Classes and Structs](#classes-and-structs)
  - [JournalManager](#journalmanager)
  - [JournalEntry](#journalentry)
  - [EJournalOperation](#ejournaloperation)
  - [JournalHeader](#journalheader)
- [Properties](#properties)
  - [CreationTime](#creationtime)
  - [Description](#description)
  - [EntryCount](#entrycount)
  - [LastModifiedTime](#lastmodifiedtime)
  - [Size](#size)
  - [Version](#version)
- [Public Methods](#public-methods)
  - [JournalManager](#journalmanager-methods)
    - [CommitAsync](#commitasync)
    - [LogOperationAsync](#logoperationasync)
    - [ReadAllEntriesAsync](#readallentriesasync)
    - [ReadUncommittedEntriesAsync](#readuncommittedentriesasync)
- [Private Methods](#private-methods)
  - [GetJournalEntrySize](#getjournalentrysize)
  - [InitializeNewJournal](#initializenewjournal)
  - [MarkEntryCommittedAsync](#markentrycommittedasync)
  - [ReadHeader](#readheader)
  - [WriteHeader](#writeheader)
  - [WriteJournalEntryAsync](#writejournalentryasync)
  - [UpdateLastModifiedTime](#updatelastmodifiedtime)
- [DisposeAsync](#disposeasync)


The `JournalManager` class is responsible for managing journal entries in a stream. It handles reading, writing, and committing journal entries asynchronously. The class implements `IAsyncDisposable` to ensure proper cleanup of unmanaged resources.

# Overview

The `JournalManager` class is a core component of the journaling system in the `Bisto.Journal` namespace. It provides mechanisms to track and log operations related to block management, such as allocating, writing, and deleting data blocks. This journaling system ensures the integrity and recoverability of operations by recording each step in the journal. The journal itself consists of a stream of structured entries, each describing an operation performed on the underlying storage.

The journal entries are encapsulated in the `JournalEntry` class, which records essential details such as the type of operation (`EJournalOperation`), the size of the block involved, and the offset in the storage where the operation occurred. Each entry also includes a timestamp (`TimeOffset`) relative to the journal's creation time, making it possible to reconstruct a sequence of operations and ensure the correct order of execution.

The `JournalManager` handles both writing new entries and committing them to the stream. It supports reading back all entries, with options to filter out only uncommitted entries for recovery scenarios. Additionally, the system ensures that each journal entry can be committed asynchronously, ensuring data consistency and durability across operations.

The journal's header provides metadata about the journal itself, such as its creation time, last modification time, version, and description. The journal uses an ASCII signature (`JRNL`) to identify its format.

Overall, the journaling system is designed to facilitate block-level transaction logging for block management operations, ensuring that changes can be tracked, rolled back, or recovered if needed. This guarantees high data integrity and robust error recovery for systems handling large-scale storage operations.


## Constants
- **ConstDescription**: A constant string (`Journal V1`) representing the journal description.
- **HeaderSize**: The size of the journal header (64 bytes).
- **MaxDescriptionSize**: The maximum size for the journal description (16 bytes).
- **SignatureId**: A constant signature (`0x4C4E524A`) representing the ASCII characters 'JRNL'.

## Properties
- **CreationTime**: The UTC time when the journal was created, based on the `CreationTime` field in the journal header.
- **Description**: The journal description, which is a UTF-8 encoded string derived from the `Description` field in the header.
- **EntryCount**: The number of journal entries recorded in the journal, based on the `EntryCount` field in the header.
- **LastModifiedTime**: The UTC time when the journal was last modified, based on the `LastModifiedTime` field in the header.
- **Size**: The total size of the journal stream, in bytes.
- **Version**: The version of the journal, based on the `Version` field in the header.

## Constructors

### `JournalManager(Stream journalStream)`
Initializes a new instance of the `JournalManager` class. If the stream is empty, a new journal header is initialized; otherwise, the existing journal header is read.

- **journalStream**: The stream where the journal entries are stored.

## Methods

### `Task CommitAsync(CancellationToken cancellationToken = default)`
Commits all journal entries by marking them as committed in the journal stream.

- **cancellationToken**: Optional. A `CancellationToken` to observe while waiting for the task to complete.

### `Task LogOperationAsync(EJournalOperation operation, long offset, int blockSize, int dataSize, CancellationToken cancellationToken = default)`
Logs an operation to the journal asynchronously.

- **operation**: The operation type (of type `EJournalOperation`) being logged.
- **offset**: The offset in the storage where the operation occurred.
- **blockSize**: The size of the block being modified.
- **dataSize**: The size of the data being written.
- **cancellationToken**: Optional. A `CancellationToken`.

### `Task<List<(JournalEntry Entry, bool IsCommitted)>> ReadAllEntriesAsync()`
Reads all journal entries from the stream, including both committed and uncommitted entries.

### `Task<List<JournalEntry>> ReadUncommittedEntriesAsync()`
Reads only the uncommitted journal entries from the stream.

### `ValueTask DisposeAsync()`
Performs the asynchronous disposal of the stream and flushes any remaining data to ensure it is written to disk.

## Private Methods

### `int GetJournalEntrySize()`
Returns the size of a journal entry, including the size of its fields.

### `void InitializeNewJournal()`
Initializes a new journal by creating a new header and writing it to the stream.

### `void ReadHeader()`
Reads the journal header from the stream, verifying the signature and parsing the fields.

### `void UpdateLastModifiedTime()`
Updates the last modified time of the journal and writes the updated header to the stream.

### `Task WriteJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default)`
Writes a journal entry to the stream, including its length, serialized data, and a flag indicating whether it is committed.

### `Task MarkEntryCommittedAsync(JournalEntry entry, CancellationToken cancellationToken = default)`
Marks a specific journal entry as committed in the stream.

### `void WriteHeader()`
Writes the journal header to the stream.

---

# JournalEntry Class

The `JournalEntry` class represents a single entry in the journal. It is marked as `MemoryPackable` for efficient serialization and deserialization using the `MemoryPack` serializer.

## Properties
- **BlockSize**: The size of the block related to the journal operation.
- **DataSize**: The size of the data being written or manipulated (default: 0).
- **Offset**: The offset within the journal where the operation occurred.
- **Operation**: The type of journal operation being performed (of type `EJournalOperation`).
- **TimeOffset**: The time offset relative to the journal's creation time.

## Constructors

### `JournalEntry(CompactTimeOffset timeOffset, EJournalOperation operation, long offset, int blockSize, int dataSize = 0)`
Creates a new `JournalEntry` with the specified time offset, operation, offset, block size, and data size.

### `static JournalEntry Create(DateTime journalCreationTime, EJournalOperation operation, long offset, int blockSize, int dataSize = 0)`
Static factory method to create a new `JournalEntry`. It calculates the time offset from the journal's creation time to the current time.

## Methods

### `static JournalEntry Create(DateTime journalCreationTime, EJournalOperation operation, long offset, int blockSize, int dataSize = 0)`
Creates a new journal entry with a calculated time offset based on the journal's creation time.

---

# EJournalOperation Enum

The `EJournalOperation` enum defines the types of operations that can be recorded in the journal.

## Values
- **AddToFreeList**: Represents an operation to add a block to the free list.
- **AllocateFromFree**: Represents an operation to allocate a block from the free list.
- **DeleteBlockFromSplit**: Represents an operation to delete a block due to a split operation.
- **WriteDataBlock**: Represents an operation to write a data block.
- **DeleteDataBlock**: Represents an operation to delete a data block.
- **MergeBlocks**: Represents an operation to merge blocks.
