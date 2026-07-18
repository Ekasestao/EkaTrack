using EkaTrack.Client.Models;
using EkaTrack.Client.Services;

namespace EkaTrack.Tests.Services;

public class CustomListSortingTests : IDisposable
{
    private readonly List<ListItemModel> _items =
    [
        new() { TmdbId = 1, MediaType = "movie", Title = "Alpha" },
        new() { TmdbId = 2, MediaType = "movie", Title = "Beta" },
        new() { TmdbId = 3, MediaType = "tv", Title = "Gamma" },
    ];

    public CustomListSortingTests()
    {
        CustomListService.ClearInteractions();
    }

    public void Dispose()
    {
        CustomListService.ClearInteractions();
    }

    [Fact]
    public void SortByDefault_WithNoInteractions_KeepsOriginalOrder()
    {
        var result = CustomListService.SortByDefault(_items);

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].TmdbId);
        Assert.Equal(2, result[1].TmdbId);
        Assert.Equal(3, result[2].TmdbId);
    }

    [Fact]
    public void SortByDefault_MovesInteractedItemToTop()
    {
        CustomListService.RecordInteraction(2, "movie");

        var result = CustomListService.SortByDefault(_items);

        Assert.Equal(2, result[0].TmdbId);
    }

    [Fact]
    public void SortByDefault_WithMultipleInteractions_MostRecentFirst()
    {
        CustomListService.RecordInteraction(1, "movie");
        Thread.Sleep(5);
        CustomListService.RecordInteraction(3, "tv");

        var result = CustomListService.SortByDefault(_items);

        Assert.Equal(3, result[0].TmdbId);
        Assert.Equal(1, result[1].TmdbId);
        Assert.Equal(2, result[2].TmdbId);
    }

    [Fact]
    public void SortByDefault_MixedInteractions_InteractedFirstThenOriginalOrder()
    {
        CustomListService.RecordInteraction(2, "movie");

        var result = CustomListService.SortByDefault(_items);

        Assert.Equal(2, result[0].TmdbId);
        Assert.Equal(1, result[1].TmdbId);
        Assert.Equal(3, result[2].TmdbId);
    }

    [Fact]
    public void SortByDefault_UpdatesOnNewInteraction_NewestGoesToTop()
    {
        CustomListService.RecordInteraction(2, "movie");
        Thread.Sleep(5);
        var firstSort = CustomListService.SortByDefault(_items);
        Assert.Equal(2, firstSort[0].TmdbId);

        CustomListService.RecordInteraction(1, "movie");
        var secondSort = CustomListService.SortByDefault(_items);

        Assert.Equal(1, secondSort[0].TmdbId);
        Assert.Equal(2, secondSort[1].TmdbId);
    }

    [Fact]
    public void RecordInteraction_DefaultsSortsToTop()
    {
        CustomListService.RecordInteraction(3, "tv");
        CustomListService.RecordInteraction(1, "movie");

        var result = CustomListService.SortByDefault(_items);

        Assert.Equal(1, result[0].TmdbId);
        Assert.Equal(3, result[1].TmdbId);
    }

    [Fact]
    public void SortWithFullyWatched_NoWatchedItems_KeepsOriginalOrder()
    {
        var fullyWatched = new HashSet<string>();
        var result = CustomListService.SortWithFullyWatched(_items, fullyWatched);

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].TmdbId);
        Assert.Equal(2, result[1].TmdbId);
        Assert.Equal(3, result[2].TmdbId);
    }

    [Fact]
    public void SortWithFullyWatched_OneFullyWatched_MovesToBottom()
    {
        var fullyWatched = new HashSet<string> { "2:movie" };
        var result = CustomListService.SortWithFullyWatched(_items, fullyWatched);

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].TmdbId);
        Assert.Equal(3, result[1].TmdbId);
        Assert.Equal(2, result[2].TmdbId);
    }

    [Fact]
    public void SortWithFullyWatched_AllFullyWatched_AllAtBottom()
    {
        var fullyWatched = new HashSet<string> { "1:movie", "2:movie", "3:tv" };
        var result = CustomListService.SortWithFullyWatched(_items, fullyWatched);

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].TmdbId);
        Assert.Equal(2, result[1].TmdbId);
        Assert.Equal(3, result[2].TmdbId);
    }

    [Fact]
    public void SortWithFullyWatched_MultipleFullyWatched_PreservesOrderWithinGroups()
    {
        var items = new List<ListItemModel>
        {
            new() { TmdbId = 1, MediaType = "movie", Title = "Alpha" },
            new() { TmdbId = 2, MediaType = "tv", Title = "Beta" },
            new() { TmdbId = 3, MediaType = "movie", Title = "Gamma" },
            new() { TmdbId = 4, MediaType = "tv", Title = "Delta" },
        };
        var fullyWatched = new HashSet<string> { "2:tv", "4:tv" };

        var result = CustomListService.SortWithFullyWatched(items, fullyWatched);

        Assert.Equal(4, result.Count);
        Assert.Equal(1, result[0].TmdbId);
        Assert.Equal(3, result[1].TmdbId);
        Assert.Equal(2, result[2].TmdbId);
        Assert.Equal(4, result[3].TmdbId);
    }

    [Fact]
    public void SortWithFullyWatched_AlphabeticalSort_IgnoresFullyWatched()
    {
        var fullyWatched = new HashSet<string> { "2:movie" };
        var result = CustomListService.SortWithFullyWatched(_items, fullyWatched, "asc");

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].TmdbId);
        Assert.Equal(2, result[1].TmdbId);
        Assert.Equal(3, result[2].TmdbId);
    }
}
