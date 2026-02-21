using DimaDB.Core.Storage.IO;

namespace DimaDB.Core.Tests.Storage;

public class FreeSpaceMapTests
{
    [Fact]
    public void UpdatePageFreeSpace_TracksFreeSpage()
    {
        var fsm = new FreeSpaceMap();

        fsm.UpdatePageFreeSpace(0, 1000);

        Assert.Equal(1000, fsm.GetPageFreeSpace(0));
    }

    [Fact]
    public void UpdatePageFreeSpace_RemovesPageWhenZero()
    {
        var fsm = new FreeSpaceMap();

        fsm.UpdatePageFreeSpace(0, 1000);
        fsm.UpdatePageFreeSpace(0, 0);

        Assert.Null(fsm.GetPageFreeSpace(0));
    }

    [Fact]
    public void UpdatePageFreeSpace_RemovesPageWhenNegative()
    {
        var fsm = new FreeSpaceMap();

        fsm.UpdatePageFreeSpace(0, 1000);
        fsm.UpdatePageFreeSpace(0, -100);

        Assert.Null(fsm.GetPageFreeSpace(0));
    }

    [Fact]
    public void FindPageWithSpace_FindsPageWithSufficientSpace()
    {
        var fsm = new FreeSpaceMap();

        fsm.UpdatePageFreeSpace(0, 500);
        fsm.UpdatePageFreeSpace(1, 1000);
        fsm.UpdatePageFreeSpace(2, 100);

        var result = fsm.FindPageWithSpace(750);

        Assert.Equal(1u, result);
    }

    [Fact]
    public void FindPageWithSpace_ReturnsNullWhenNoPageHasSufficientSpace()
    {
        var fsm = new FreeSpaceMap();

        fsm.UpdatePageFreeSpace(0, 100);
        fsm.UpdatePageFreeSpace(1, 200);

        var result = fsm.FindPageWithSpace(500);

        Assert.Null(result);
    }

    [Fact]
    public void FindPageWithSpace_ReturnsFirstMatchingPage()
    {
        var fsm = new FreeSpaceMap();

        fsm.UpdatePageFreeSpace(0, 1000);
        fsm.UpdatePageFreeSpace(1, 2000);
        fsm.UpdatePageFreeSpace(2, 1500);

        var result = fsm.FindPageWithSpace(1200);

        Assert.NotNull(result);
        Assert.True(result == 1u || result == 2u);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var fsm = new FreeSpaceMap();

        fsm.UpdatePageFreeSpace(0, 100);
        fsm.UpdatePageFreeSpace(1, 200);
        fsm.UpdatePageFreeSpace(2, 300);

        fsm.Clear();

        Assert.Null(fsm.FindPageWithSpace(50));
        Assert.Equal(0, fsm.PageCount);
    }

    [Fact]
    public void PageCount_ReturnsCorrectNumberOfPages()
    {
        var fsm = new FreeSpaceMap();

        fsm.UpdatePageFreeSpace(0, 100);
        fsm.UpdatePageFreeSpace(1, 200);
        fsm.UpdatePageFreeSpace(2, 300);

        Assert.Equal(3, fsm.PageCount);

        fsm.UpdatePageFreeSpace(1, 0);

        Assert.Equal(2, fsm.PageCount);
    }

    [Fact]
    public void GetAllPages_ReturnsAllTrackedPages()
    {
        var fsm = new FreeSpaceMap();

        fsm.UpdatePageFreeSpace(0, 100);
        fsm.UpdatePageFreeSpace(1, 200);
        fsm.UpdatePageFreeSpace(5, 300);

        var all = fsm.GetAllPages();

        Assert.Equal(3, all.Count);
        Assert.Equal(100, all[0]);
        Assert.Equal(200, all[1]);
        Assert.Equal(300, all[5]);
    }

    [Fact]
    public void MultipleUpdates_TracksLatestValue()
    {
        var fsm = new FreeSpaceMap();

        fsm.UpdatePageFreeSpace(0, 1000);
        Assert.Equal(1000, fsm.GetPageFreeSpace(0));

        fsm.UpdatePageFreeSpace(0, 500);
        Assert.Equal(500, fsm.GetPageFreeSpace(0));

        fsm.UpdatePageFreeSpace(0, 750);
        Assert.Equal(750, fsm.GetPageFreeSpace(0));
    }

    [Fact]
    public void FindPageWithSpace_ExactMatch()
    {
        var fsm = new FreeSpaceMap();

        fsm.UpdatePageFreeSpace(0, 500);
        fsm.UpdatePageFreeSpace(1, 1000);

        var result = fsm.FindPageWithSpace(1000);

        Assert.Equal(1u, result);
    }
}