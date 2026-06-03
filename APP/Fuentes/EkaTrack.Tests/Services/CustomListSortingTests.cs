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
}
