using DimaDB.Storage.Page;
using DimaDB.Storage.Page.Overflow;

namespace DimaDB.Tests.Storage;

public class OverflowPageTests
{
    [Fact]
    public void WriteData_StoresData()
    {
        var page = new OverflowPage(0, 0, 100);
        var data = new byte[] { 1, 2, 3, 4, 5 };

        page.WriteData(data);
        var retrieved = page.ReadData(data.Length);

        Assert.Equal(data, retrieved);
    }

    [Fact]
    public void WriteData_ExceedingAvailableSpace_ThrowsException()
    {
        var page = new OverflowPage(0, 0, 100);
        var tooLargeData = new byte[10000];

        var ex = Assert.Throws<InvalidOperationException>(() => page.WriteData(tooLargeData));
        Assert.Contains("too large", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadData_PartialRead()
    {
        var page = new OverflowPage(0, 0, 100);
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        page.WriteData(data);
        var partialRead = page.ReadData(5);

        Assert.Equal(5, partialRead.Length);
        Assert.Equal(data.AsSpan(0, 5).ToArray(), partialRead);
    }

    [Fact]
    public void UpdateHeader_UpdatesNextPointer()
    {
        var page = new OverflowPage(0, 0, 100);

        Assert.Equal(0u, page.Header.NextPageId);

        page.UpdateHeader(2);

        Assert.Equal(2u, page.Header.NextPageId);
    }

    [Fact]
    public void OverflowPageSerialization_PreservesData()
    {
        var page1 = new OverflowPage(0, 0, 100);
        var data = new byte[] { 1, 2, 3, 4, 5 };

        page1.WriteData(data);
        byte[] serialized = page1.ToBytes();

        var page2 = OverflowPage.Read(serialized);

        Assert.Equal(0u, page2.Header.PageId);
        Assert.Equal(0u, page2.Header.NextPageId);

        var retrieved = page2.ReadData(data.Length);
        Assert.Equal(data, retrieved);
    }

    [Fact]
    public void GetAvailableSpace_ReturnsCorrectSize()
    {
        int available = OverflowPage.GetAvailableSpace();

        Assert.Equal(BasePage.PageSize - OverflowPageHeader.Size, available);
    }

    [Fact]
    public void HeaderPreservation_AcrossSerializationCycle()
    {
        var page1 = new OverflowPage(123, 456, 789);
        var data = new byte[100];
        page1.WriteData(data);

        byte[] serialized = page1.ToBytes();
        var page2 = OverflowPage.Read(serialized);

        Assert.Equal(123u, page2.Header.PageId);
        Assert.Equal(456u, page2.Header.NextPageId);
        Assert.Equal(789u, page2.Header.DataLength);
    }

    [Fact]
    public void WriteData_MultipleChunks()
    {
        var page1 = new OverflowPage(0, 0, 2000);
        var page2 = new OverflowPage(0, 0, 2000);

        var chunk1 = new byte[BasePage.PageSize - OverflowPageHeader.Size];
        Array.Fill(chunk1, (byte)1);

        var chunk2 = new byte[BasePage.PageSize - OverflowPageHeader.Size];
        Array.Fill(chunk2, (byte)2);

        page1.WriteData(chunk1);
        page2.WriteData(chunk2);

        var retrieved1 = page1.ReadData(chunk1.Length);
        var retrieved2 = page2.ReadData(chunk2.Length);

        Assert.All(retrieved1, b => Assert.Equal(1, b));
        Assert.All(retrieved2, b => Assert.Equal(2, b));
    }
}