using System.Collections.Concurrent;

namespace Bisto.FreeBlocks;

/// <summary>
/// Class MemoryCacheManager with LRU eviction policy.
/// </summary>
public class MemoryCacheManager
{
    private readonly ConcurrentDictionary<long, CacheItem> _cache;

    private readonly object _lock = new object();

    private readonly LinkedList<long> _lruList;

    private readonly int _maxCacheSize;

    public int Count => _cache.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryCacheManager"/> class.
    /// </summary>
    /// <param name="maxCacheSize">
    /// Maximum number of items that can be stored in the cache at any given time.
    /// When this limit is reached, the least recently used item will be evicted
    /// before adding a new item. This parameter determines the upper bound of the 
    /// cache's item count, not the actual memory usage. Choose a value based on your 
    /// application's memory constraints and expected item sizes. For example, if 
    /// each cached item is roughly 1MB and you want to limit the cache to about 
    /// 1GB of memory, you might set this to 1000.
    /// </param>
    public MemoryCacheManager(int maxCacheSize)
    {
        _cache = new ConcurrentDictionary<long, CacheItem>();
        _lruList = new LinkedList<long>();
        _maxCacheSize = maxCacheSize;
    }

    public void Remove(long key)
    {
        if (_cache.TryRemove(key, out var removedItem))
        {
            lock (_lock)
            {
                _lruList.Remove(removedItem.Node);
            }
        }
    }

    public void Set(long key, byte[] value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var existingItem))
            {
                // Update existing item
                existingItem.Value = value;
                _lruList.Remove(existingItem.Node);
                _lruList.AddFirst(existingItem.Node);
            }
            else
            {
                // Add new item
                if (_cache.Count >= _maxCacheSize)
                {
                    // Evict least recently used item
                    var lruKey = _lruList.Last.Value;
                    _lruList.RemoveLast();
                    _cache.TryRemove(lruKey, out _);
                }

                var newNode = _lruList.AddFirst(key);
                _cache[key] = new CacheItem { Value = value, Node = newNode };
            }
        }
    }

    public bool TryGetValue(long key, out byte[] value)
    {
        if (_cache.TryGetValue(key, out var cacheItem))
        {
            lock (_lock)
            {
                // Move accessed item to the front of the LRU list
                _lruList.Remove(cacheItem.Node);
                _lruList.AddFirst(cacheItem.Node);
            }

            value = cacheItem.Value;
            return true;
        }

        value = null;
        return false;
    }

    private class CacheItem
    {
        public LinkedListNode<long> Node { get; set; }

        public byte[] Value { get; set; }
    }
}

/*
Enhanced documentation for the maxCacheSize parameter:

Basic definition:
"Maximum number of items that can be stored in the cache at any given time."
This clearly states that the parameter is about the count of items, not memory size.
Eviction policy:
"When this limit is reached, the least recently used item will be evicted before adding a new item."
This explains what happens when the cache reaches its capacity.
Clarification on size vs. count:
"This parameter determines the upper bound of the cache's item count, not the actual memory usage."
This important distinction helps prevent misunderstandings about what the parameter controls.
Guidance for choosing a value:
"Choose a value based on your application's memory constraints and expected item sizes."
This advises the user to consider their specific use case when setting this parameter.
Example:
"For example, if each cached item is roughly 1MB and you want to limit the cache to about 1GB of memory, you might set this to 1000."
This concrete example helps users understand how to translate memory constraints into an appropriate item count.
 */