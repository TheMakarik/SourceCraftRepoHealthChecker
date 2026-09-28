namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal static class SourceCraftPagination
{
    public static async Task<List<TItem>> CollectAsync<TItem>(
        int maxItems,
        int maxPages,
        Func<string?, CancellationToken, Task<(IReadOnlyCollection<TItem> Items, string? NextPageToken)>> fetchPageAsync,
        CancellationToken cancellationToken)
    {
        List<TItem> items = [];
        HashSet<string> seenTokens = new(StringComparer.Ordinal);
        string? pageToken = null;

        for (var page = 0; page < maxPages; page++)
        {
            var (pageItems, nextPageToken) = await fetchPageAsync(pageToken, cancellationToken);
            items.AddRange(pageItems);

            if (maxItems > 0 && items.Count >= maxItems)
                return items.Take(maxItems).ToList();

            if (string.IsNullOrEmpty(nextPageToken) || nextPageToken == pageToken)
                return items;
            if (!seenTokens.Add(nextPageToken))
                return items;

            pageToken = nextPageToken;
        }

        return items;
    }
}
