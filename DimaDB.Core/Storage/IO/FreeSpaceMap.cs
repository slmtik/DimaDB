namespace DimaDB.Core.Storage.IO;

public class FreeSpaceMap
{
    private readonly Dictionary<uint, int> _pageFreespace = [];

    public int PageCount => _pageFreespace.Count;

    public void UpdatePageFreeSpace(uint pageId, int freeSpace)
    {
        if (freeSpace > 0)
        {
            _pageFreespace[pageId] = freeSpace;
        }
        else
        {
            _pageFreespace.Remove(pageId);
        }
    }

    public uint? FindPageWithSpace(int requiredSpace)
    {
        foreach (var (pageId, freeSpace) in _pageFreespace)
        {
            if (freeSpace >= requiredSpace)
            {
                return pageId;
            }
        }
        return null;
    }

    public Dictionary<uint, int> GetAllPages()
    {
       return new Dictionary<uint, int>(_pageFreespace);
    }

    public int? GetPageFreeSpace(uint pageId)
    {
        return _pageFreespace.TryGetValue(pageId, out var space) ? space : null;
    }

    public void Clear()
    {
        _pageFreespace.Clear();
    }
}