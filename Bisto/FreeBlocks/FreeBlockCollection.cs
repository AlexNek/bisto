namespace Bisto.FreeBlocks;

internal class FreeBlockCollection
{
    private readonly SortedDictionary<long, FreeBlock> _freeBlocksByAddress = new();

    private readonly SortedDictionary<int, List<FreeBlock>> _freeBlocksBySize = new();

    public void Add(FreeBlock block)
    {
        if (!_freeBlocksBySize.TryGetValue(block.Size, out var list))
        {
            list = new List<FreeBlock>();
            _freeBlocksBySize[block.Size] = list;
        }

        list.Add(block);
        _freeBlocksByAddress[block.Offset] = block;
    }

    public List<FreeBlock> GetAll() => _freeBlocksByAddress.Values.ToList();

    public FreeBlock GetByAddress(long address) => _freeBlocksByAddress[address];

    public IEnumerable<KeyValuePair<int, List<FreeBlock>>> GetBySize() => _freeBlocksBySize;

    public void Remove(long offset, int size)
    {
        if (_freeBlocksBySize.TryGetValue(size, out var list))
        {
            list.RemoveAll(b => b.Offset == offset);
            if (list.Count == 0)
            {
                _freeBlocksBySize.Remove(size);
            }
        }

        _freeBlocksByAddress.Remove(offset);
    }
}
