using DimaDB.Core.Storage.IO;
using DimaDB.Core.Storage.Page.Data;
using DimaDB.Core.Storage.Page.Overflow;
using DimaDB.Core.Storage.Serialization;
using DimaDB.Core.Storage.Types;

namespace DimaDB.Core.Storage;

public class TableHandle
{
    private readonly string _tableName;
    private readonly Schema _schema;
    private readonly StorageManager _storageManager;
    private readonly RecordSerializer _serializer;
    private readonly OverflowManager _overflowManager;
    private readonly FreeSpaceMap _freeSpaceMap;

    public TableHandle(string tableName, Schema schema, StorageManager storageManager, OverflowManager overflowManager, FreeSpaceMap freeSpaceMap)
    {
        _tableName = tableName;
        _schema = schema;
        _storageManager = storageManager;
        _overflowManager = overflowManager;
        _overflowManager.SetTableContext(tableName);
        _serializer = new RecordSerializer(schema, overflowManager);
        _freeSpaceMap = freeSpaceMap;
    }

    public Schema Schema => _schema;

    public RecordId Insert(object?[] values)
    {
        byte[] recordData = _serializer.Serialize(values);
        int requiredSpace = recordData.Length + SlotDirectoryEntry.Size;

        if (_freeSpaceMap.FindPageWithSpace(requiredSpace) is uint pageIdWithSpace)
        {
            var page = _storageManager.GetHeapPage(_tableName, pageIdWithSpace);
            int slotId = page.InsertRecord(recordData);

            if (slotId >= 0)
            {
                _storageManager.PutHeapPage(_tableName, page);
                UpdateFreeSpaceMap(page);
                return new RecordId(pageIdWithSpace, (ushort)slotId);
            }
        }

        uint maxPageId = _storageManager.GetMaxPageId(_tableName);

        for (uint i = 0; i <= maxPageId; i++)
        {
            var page = _storageManager.GetHeapPage(_tableName, i);
            int slotId = page.InsertRecord(recordData);

            if (slotId >= 0)
            {
                _storageManager.PutHeapPage(_tableName, page);
                UpdateFreeSpaceMap(page);
                return new RecordId(i, (ushort)slotId);
            }
        }

        uint pageId = maxPageId + 1;
        var newPage = new HeapPage(pageId);
        int newSlotId = newPage.InsertRecord(recordData);

        if (newSlotId >= 0)
        {
            _storageManager.PutHeapPage(_tableName, newPage);
            UpdateFreeSpaceMap(newPage);
            return new RecordId(pageId, (ushort)newSlotId);
        }

        throw new InvalidOperationException("Failed to insert record: record too large for page");
    }

    public object?[]? Get(RecordId rid)
    {
        var actualRid = ResolveForwardingPointers(rid);
        var page = _storageManager.GetHeapPage(_tableName, actualRid.PageId);
        var data = page.ReadRecord((int)actualRid.SlotId);

        if (data is null)
            return null;

        return _serializer.Deserialize(data);
    }

    public bool Update(RecordId rid, object?[] newValues)
    {
        var actualRid = ResolveForwardingPointers(rid);
        var page = _storageManager.GetHeapPage(_tableName, actualRid.PageId);
        var oldData = page.ReadRecord((int)actualRid.SlotId);

        if (oldData is null)
        {
            return false;
        }

        CleanupOverflowData(oldData);

        byte[] newData = _serializer.Serialize(newValues);

        if (page.UpdateRecord((int)actualRid.SlotId, newData))
        {
            _storageManager.PutHeapPage(_tableName, page);
            UpdateFreeSpaceMap(page);
            return true;
        }

        var newRid = Insert(newValues);

        var oldPage = _storageManager.GetHeapPage(_tableName, actualRid.PageId);
        oldPage.SetForwardingPointer((int)actualRid.SlotId, newRid.PageId, newRid.SlotId);
        _storageManager.PutHeapPage(_tableName, oldPage);
        UpdateFreeSpaceMap(oldPage);

        return true;
    }

    public bool Delete(RecordId rid)
    {
        var actualRid = ResolveForwardingPointers(rid);
        var page = _storageManager.GetHeapPage(_tableName, actualRid.PageId);
        var data = page.ReadRecord((int)actualRid.SlotId);

        if (data is not null)
        {
            CleanupOverflowData(data);
        }

        bool success = page.DeleteRecord((int)actualRid.SlotId);

        if (success)
        {
            _storageManager.PutHeapPage(_tableName, page);
            UpdateFreeSpaceMap(page);
        }

        return success;
    }

    public IEnumerable<(RecordId, object?[])> Scan()
    {
        uint maxPageId = _storageManager.GetMaxPageId(_tableName);

        for (uint pageId = 0; pageId <= maxPageId; pageId++)
        {
            var page = _storageManager.GetHeapPage(_tableName, pageId);

            if (page.Header.NumSlots == 0)
                continue;

            foreach (var (slotId, data) in page.ScanRecords())
            {
                var values = _serializer.Deserialize(data);
                var rid = new RecordId(pageId, (ushort)slotId);
                yield return (rid, values);
            }
        }
    }

    private RecordId ResolveForwardingPointers(RecordId rid)
    {
        const int maxHops = 100;
        int hops = 0;

        while (hops < maxHops)
        {
            var page = _storageManager.GetHeapPage(_tableName, rid.PageId);
            var forwarding = page.GetForwardingPointer((int)rid.SlotId);

            if (forwarding is null)
            {
                return rid;
            }

            rid = new RecordId(forwarding.Value.PageId, (ushort)forwarding.Value.SlotId);
            hops++;
        }

        throw new InvalidOperationException($"Forwarding pointer chain exceeded {maxHops} hops. Possible cycle detected.");
    }

    private void CleanupOverflowData(byte[] recordData)
    {
        using var ms = new MemoryStream(recordData);
        using var br = new BinaryReader(ms);

        for (int i = 0; i < _schema.Columns.Length; i++)
        {
            var column = _schema.Columns[i];
            bool isNotNull = br.ReadByte() != 0;

            if (!isNotNull)
                continue;

            switch (column.Type)
            {
                case ColumnType.Int:
                    br.ReadInt32();
                    break;
                case ColumnType.Bool:
                    br.ReadBoolean();
                    break;
                case ColumnType.Text:
                    byte marker = br.ReadByte();
                    if (marker == 0)
                    {
                        int length = br.ReadInt32();
                        br.ReadBytes(length);
                    }
                    else if (marker == 1)
                    {
                        var pointerBytes = br.ReadBytes(OverflowPointer.Size);
                        var pointer = OverflowPointer.Read(pointerBytes);
                        _overflowManager.DeleteOverflow(pointer);
                    }
                    break;
            }
        }
    }

    private void UpdateFreeSpaceMap(HeapPage page)
    {
        int freeSpace = page.GetAvailableFreeSpace();
        _freeSpaceMap.UpdatePageFreeSpace(page.Header.PageId, freeSpace);
    }
}