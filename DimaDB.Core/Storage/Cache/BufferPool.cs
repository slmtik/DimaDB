using DimaDB.Core.Storage.Page;

namespace DimaDB.Core.Storage;

public class BufferPool(int capacity = 100)
{
    private readonly int _capacity = capacity;
    private readonly Dictionary<string, BasePage> _pages = [];
    private readonly LinkedList<string> _lru = new();
    private readonly Dictionary<string, LinkedListNode<string>> _lruMap = [];

    public BasePage GetOrNull(string cacheKey)
    {
        if (_pages.TryGetValue(cacheKey, out var page))
        {
            if (_lruMap.TryGetValue(cacheKey, out var node))
            {
                _lru.Remove(node);
                _lru.AddLast(node);
            }
            return page;
        }
        return null!;
    }

    public void Put(string cacheKey, BasePage page)
    {
        if (_pages.ContainsKey(cacheKey))
        {
            _pages[cacheKey] = page;
            return;
        }

        if (_pages.Count >= _capacity)
        {
            var toEvict = _lru.First!.Value;
            _lru.RemoveFirst();
            _lruMap.Remove(toEvict);
            _pages.Remove(toEvict);
        }

        _pages[cacheKey] = page;
        var node = _lru.AddLast(cacheKey);
        _lruMap[cacheKey] = node;
    }

    public void Clear()
    {
        _pages.Clear();
        _lru.Clear();
        _lruMap.Clear();
    }
}