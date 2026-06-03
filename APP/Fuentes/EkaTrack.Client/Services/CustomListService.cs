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

    public static void ClearInteractions()
    {
        _interactionTimes.Clear();
    }

    public static Dictionary<string, DateTime> GetInteractions()
    {
        return _interactionTimes;
    }
}
