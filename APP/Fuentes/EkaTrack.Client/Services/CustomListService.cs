using EkaTrack.Client.Models;

namespace EkaTrack.Client.Services;

public static class CustomListService
{
    private static readonly Dictionary<string, DateTime> _interactionTimes = [];

    public static void RecordInteraction(int tmdbId, string mediaType)
    {
        var key = $"{tmdbId}:{mediaType}";
        _interactionTimes[key] = DateTime.UtcNow;
    }

    public static List<ListItemModel> SortByDefault(List<ListItemModel> items)
    {
        if (_interactionTimes.Count == 0)
            return items;

        return [.. items
            .OrderByDescending(i => _interactionTimes.TryGetValue($"{i.TmdbId}:{i.MediaType}", out var t) ? t : DateTime.MinValue)];
    }

    public static List<ListItemModel> SortWithFullyWatched(
        List<ListItemModel> items,
        HashSet<string> fullyWatchedKeys,
        string sortOrder = "none")
    {
        if (sortOrder == "asc")
            return [.. items.OrderBy(i => i.Title)];
        if (sortOrder == "desc")
            return [.. items.OrderByDescending(i => i.Title)];

        var notWatched = items.Where(i => !fullyWatchedKeys.Contains($"{i.TmdbId}:{i.MediaType}"))
            .OrderByDescending(i => string.IsNullOrEmpty(i.LastInteractedAt) ? DateTime.MinValue : DateTime.TryParse(i.LastInteractedAt, out var t) ? t : DateTime.MinValue)
            .ToList();
        var watched = items.Where(i => fullyWatchedKeys.Contains($"{i.TmdbId}:{i.MediaType}"))
            .OrderByDescending(i => string.IsNullOrEmpty(i.LastInteractedAt) ? DateTime.MinValue : DateTime.TryParse(i.LastInteractedAt, out var t) ? t : DateTime.MinValue)
            .ToList();
        return [.. notWatched, .. watched];
    }

    public static void ClearInteractions()
    {
        _interactionTimes.Clear();
    }

    public static Dictionary<string, DateTime> GetInteractions()
    {
        return _interactionTimes;
    }
}
