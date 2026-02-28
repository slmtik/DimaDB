using System.Reflection.PortableExecutable;

namespace DimaDB.Storage.Page.Data;

public class HeapPage : BasePage
{
    public HeapPageHeader Header { get; private set; }

    public HeapPage(uint pageId)
    {
        Header = new HeapPageHeader(pageId, 0, PageSize);
        Header.Write(_buffer);
    }

    public static HeapPage Read(byte[] buffer)
    {
        var page = new HeapPage(0)
        {
            _buffer = (byte[])buffer.Clone(),
            Header = HeapPageHeader.Read(buffer)
        };
        return page;
    }

    public int GetAvailableFreeSpace()
    {
        int slotDirStart = HeapPageHeader.Size;
        int slotDirEnd = slotDirStart + ((int)Header.NumSlots * SlotDirectoryEntry.Size);
        int recordAreaEnd = Header.FreeSpaceOffset;

        return recordAreaEnd - slotDirEnd;
    }

    public int InsertRecord(ReadOnlySpan<byte> recordData)
    {
        int slotId = (int)Header.NumSlots;
        int slotDirStart = HeapPageHeader.Size;
        int slotDirEnd = slotDirStart + (slotId + 1) * SlotDirectoryEntry.Size;

        int recordAreaEnd = Header.FreeSpaceOffset;
        int newRecordStart = recordAreaEnd - recordData.Length;

        if (slotDirEnd > newRecordStart)
        {
            return -1;
        }

        recordData.CopyTo(_buffer.AsSpan(newRecordStart));

        var entry = SlotDirectoryEntry.Record((uint)newRecordStart, (uint)recordData.Length);
        WriteSlotEntry(slotId, entry);

        Header = new HeapPageHeader(Header.PageId, Header.NumSlots + 1, (ushort)newRecordStart);
        Header.Write(_buffer);

        return slotId;
    }

    public byte[]? ReadRecord(int slotId)
    {
        if (slotId < 0 || slotId >= Header.NumSlots)
        {
            return null;
        }

        var entry = ReadSlotEntry(slotId);
        if (entry.IsEmpty || entry.IsTombstone || entry.IsForwarding)
        {
            return null;
        }

        return _buffer.AsSpan((int)entry.Offset, (int)entry.Length).ToArray();
    }

    public bool UpdateRecord(int slotId, ReadOnlySpan<byte> newData)
    {
        if (slotId < 0 || slotId >= Header.NumSlots)
        {
            return false;
        }

        var entry = ReadSlotEntry(slotId);
        if (entry.IsEmpty || entry.IsTombstone || entry.IsForwarding)
        {
            return false;
        }

        if (newData.Length > entry.Length)
        {
            return false;
        }

        newData.CopyTo(_buffer.AsSpan((int)entry.Offset));

        if (newData.Length < entry.Length)
        {
            var newEntry = SlotDirectoryEntry.Record(entry.Offset, (ushort)newData.Length);
            WriteSlotEntry(slotId, newEntry);
        }

        return true;
    }

    public bool DeleteRecord(int slotId)
    {
        if (slotId < 0 || slotId >= Header.NumSlots)
        {
            return false;
        }

        var entry = ReadSlotEntry(slotId);
        if (entry.IsEmpty)
        {
            return false;
        }

        var tombstone = SlotDirectoryEntry.Tombstone();
        WriteSlotEntry(slotId, tombstone);

        return true;
    }

    public void SetForwardingPointer(int slotId, uint newPageId, ushort newSlotId)
    {
        if (slotId >= 0 && slotId < Header.NumSlots)
        {
            var forwarding = SlotDirectoryEntry.Forwarding(newPageId, newSlotId);
            WriteSlotEntry(slotId, forwarding);
        }
    }

    public (uint PageId, int SlotId)? GetForwardingPointer(int slotId)
    {
        if (slotId < 0 || slotId >= Header.NumSlots)
        {
            return null;
        }

        var entry = ReadSlotEntry(slotId);
        if (!entry.IsForwarding || entry.IsTombstone)
        {
            return null;
        }

        return (entry.PageId, entry.SlotId);
    }

    public IEnumerable<(int SlotId, byte[] Data)> ScanRecords()
    {
        for (int i = 0; i < Header.NumSlots; i++)
        {
            var entry = ReadSlotEntry(i);
            if (!entry.IsEmpty && !entry.IsTombstone && !entry.IsForwarding)
            {
                var data = _buffer.AsSpan((int)entry.Offset, (int)entry.Length).ToArray();
                yield return (i, data);
            }
        }
    }

    public override byte[] ToBytes() => _buffer;

    private SlotDirectoryEntry ReadSlotEntry(int slotId)
    {
        int offset = HeapPageHeader.Size + slotId * SlotDirectoryEntry.Size;
        return SlotDirectoryEntry.Read(_buffer.AsSpan(offset, SlotDirectoryEntry.Size));
    }

    private void WriteSlotEntry(int slotId, SlotDirectoryEntry entry)
    {
        int offset = HeapPageHeader.Size + slotId * SlotDirectoryEntry.Size;
        entry.Write(_buffer.AsSpan(offset, SlotDirectoryEntry.Size));
    }
}