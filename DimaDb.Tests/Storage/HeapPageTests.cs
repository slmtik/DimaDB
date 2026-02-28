using DimaDB.Storage.Page.Data;

namespace DimaDB.Tests.Storage;

public class HeapPageTests
{
    [Fact]
    public void InsertRecord_AddsRecordAndReturnsSlotId()
    {
        var page = new HeapPage(0);
        var recordData = new byte[] { 1, 2, 3, 4 };

        int slotId = page.InsertRecord(recordData);

        Assert.Equal(0, slotId);
        Assert.Equal(1u, page.Header.NumSlots);
    }

    [Fact]
    public void InsertRecord_MultipleRecords_IncrementsSlotId()
    {
        var page = new HeapPage(0);
        var record1 = new byte[] { 1, 2, 3 };
        var record2 = new byte[] { 4, 5, 6 };
        var record3 = new byte[] { 7, 8, 9 };

        int slot1 = page.InsertRecord(record1);
        int slot2 = page.InsertRecord(record2);
        int slot3 = page.InsertRecord(record3);

        Assert.Equal(0, slot1);
        Assert.Equal(1, slot2);
        Assert.Equal(2, slot3);
        Assert.Equal(3u, page.Header.NumSlots);
    }

    [Fact]
    public void ReadRecord_RetrievesInsertedData()
    {
        var page = new HeapPage(0);
        var recordData = new byte[] { 1, 2, 3, 4, 5 };

        int slotId = page.InsertRecord(recordData);
        var retrieved = page.ReadRecord(slotId);

        Assert.NotNull(retrieved);
        Assert.Equal(recordData, retrieved);
    }

    [Fact]
    public void ReadRecord_InvalidSlotId_ReturnsNull()
    {
        var page = new HeapPage(0);

        var retrieved = page.ReadRecord(999);

        Assert.Null(retrieved);
    }

    [Fact]
    public void UpdateRecord_ModifiesExistingRecord()
    {
        var page = new HeapPage(0);
        var original = new byte[] { 1, 2, 3, 4 };
        var updated = new byte[] { 5, 6, 7, 8 };

        int slotId = page.InsertRecord(original);
        bool success = page.UpdateRecord(slotId, updated);

        Assert.True(success);
        var retrieved = page.ReadRecord(slotId);
        Assert.Equal(updated, retrieved);
    }

    [Fact]
    public void UpdateRecord_SmallerData_Succeeds()
    {
        var page = new HeapPage(0);
        var original = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var smaller = new byte[] { 9, 10 };

        int slotId = page.InsertRecord(original);
        bool success = page.UpdateRecord(slotId, smaller);

        Assert.True(success);
        var retrieved = page.ReadRecord(slotId);
        Assert.Equal(smaller, retrieved);
    }

    [Fact]
    public void UpdateRecord_LargerData_Fails()
    {
        var page = new HeapPage(0);
        var original = new byte[] { 1, 2, 3 };
        var larger = new byte[] { 1, 2, 3, 4, 5 };

        int slotId = page.InsertRecord(original);
        bool success = page.UpdateRecord(slotId, larger);

        Assert.False(success);
    }

    [Fact]
    public void DeleteRecord_MarksTombstone()
    {
        var page = new HeapPage(0);
        var recordData = new byte[] { 1, 2, 3, 4 };

        int slotId = page.InsertRecord(recordData);
        bool deleted = page.DeleteRecord(slotId);

        Assert.True(deleted);
        var retrieved = page.ReadRecord(slotId);
        Assert.Null(retrieved);
    }

    [Fact]
    public void ScanRecords_ReturnsAllValidRecords()
    {
        var page = new HeapPage(0);
        page.InsertRecord(new byte[] { 1, 2 });
        page.InsertRecord(new byte[] { 3, 4 });
        page.InsertRecord(new byte[] { 5, 6 });

        var records = page.ScanRecords().ToList();

        Assert.Equal(3, records.Count);
    }

    [Fact]
    public void ScanRecords_SkipsTombstones()
    {
        var page = new HeapPage(0);
        int slot0 = page.InsertRecord(new byte[] { 1, 2 });
        int slot1 = page.InsertRecord(new byte[] { 3, 4 });
        int slot2 = page.InsertRecord(new byte[] { 5, 6 });

        page.DeleteRecord(slot1); // Delete middle record

        var records = page.ScanRecords().ToList();

        Assert.Equal(2, records.Count);
        Assert.Equal(slot0, records[0].SlotId);
        Assert.Equal(slot2, records[1].SlotId);
    }

    [Fact]
    public void InsertRecord_PageFull_ReturnsMinus1()
    {
        var page = new HeapPage(0);
        var largeRecord = new byte[3000];

        int slot = 0;
        while (true)
        {
            int result = page.InsertRecord(largeRecord);
            if (result == -1)
            {
                break;
            }
            slot++;

            if (slot > 100)
            {
                Assert.Fail("Page should have filled by now");
            }
        }

        Assert.True(page.Header.NumSlots > 0);
    }

    [Fact]
    public void PageSerialization_PreservesData()
    {
        var page1 = new HeapPage(0);
        var record1 = new byte[] { 1, 2, 3, 4 };
        var record2 = new byte[] { 5, 6, 7, 8 };

        page1.InsertRecord(record1);
        page1.InsertRecord(record2);

        byte[] serialized = page1.ToBytes();
        var page2 = HeapPage.Read(serialized);

        var retrieved1 = page2.ReadRecord(0);
        var retrieved2 = page2.ReadRecord(1);

        Assert.Equal(record1, retrieved1);
        Assert.Equal(record2, retrieved2);
    }

    [Fact]
    public void MultipleUpdates_WorkCorrectly()
    {
        var page = new HeapPage(0);
        var original = new byte[] { 1, 2, 3, 4 };

        int slotId = page.InsertRecord(original);

        var update1 = new byte[] { 5, 6 };
        bool success1 = page.UpdateRecord(slotId, update1);
        Assert.True(success1);

        var update2 = new byte[] { 7 };
        bool success2 = page.UpdateRecord(slotId, update2);
        Assert.True(success2);

        var retrieved = page.ReadRecord(slotId);
        Assert.Equal(update2, retrieved);
    }
}