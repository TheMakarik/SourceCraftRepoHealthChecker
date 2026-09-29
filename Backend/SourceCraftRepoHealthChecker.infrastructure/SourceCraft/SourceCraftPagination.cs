using Microsoft.Extensions.Logging;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal static class SourceCraftPagination
{
    public static async Task<SourceCraftPaginationResult<TItem>> CollectAsync<TItem>(
        int maxItems,
        int maxPages,
        Func<string?, CancellationToken, Task<(IReadOnlyCollection<TItem> Items, string? NextPageToken)>> fetchPageAsync,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        var result = new SourceCraftPaginationResult<TItem>();
        HashSet<string> seenTokens = new(StringComparer.Ordinal);
        string? pageToken = null;

        for (var page = 0; page < maxPages; page++)
        {
            var (pageItems, nextPageToken) = await fetchPageAsync(pageToken, cancellationToken);
            result.AddRange(pageItems);

            if (maxItems > 0 && result.Count >= maxItems)
            {
                var exceededPage = result.Count > maxItems;
                if (exceededPage)
                    result.RemoveRange(maxItems, result.Count - maxItems);

                result.Truncated = exceededPage || !string.IsNullOrEmpty(nextPageToken);
                if (result.Truncated)
                    logger?.LogWarning("SourceCraft pagination truncated at {MaximumItems} items; coverage may be incomplete", maxItems);

                return result;
            }

            if (string.IsNullOrEmpty(nextPageToken) || nextPageToken == pageToken)
                return result;

            if (!seenTokens.Add(nextPageToken))
            {
                logger?.LogWarning("SourceCraft pagination stopped on a repeated page token after {Count} items; coverage may be incomplete", result.Count);
                result.Truncated = true;
                return result;
            }

            pageToken = nextPageToken;
        }

        if (!string.IsNullOrEmpty(pageToken))
        {
            logger?.LogWarning("SourceCraft pagination stopped after {MaximumPages} pages ({Count} items); coverage may be incomplete", maxPages, result.Count);
            result.Truncated = true;
        }

        return result;
    }
}
