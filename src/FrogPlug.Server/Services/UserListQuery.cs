using FrogPlug.Server.Data;

namespace FrogPlug.Server.Services;

public sealed record UserListPage(
    IReadOnlyList<ClientUser> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public static class UserListQuery
{
    public static UserListPage Apply(
        IEnumerable<ClientUser> users,
        string? search,
        string? status,
        int page,
        int pageSize,
        DateTime nowUtc)
    {
        var normalizedStatus = status?.Trim().ToLowerInvariant();
        var matching = users
            .Where(user => MatchesSearch(user, search))
            .Where(user => MatchesStatus(user, normalizedStatus, nowUtc))
            .OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var safePageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;
        var totalPages = Math.Max(1, (int)Math.Ceiling(matching.Count / (double)safePageSize));
        var safePage = Math.Clamp(page, 1, totalPages);
        var items = matching.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList();

        return new UserListPage(items, matching.Count, safePage, safePageSize, totalPages);
    }

    private static bool MatchesSearch(ClientUser user, string? search)
    {
        return string.IsNullOrWhiteSpace(search)
            || user.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (user.Note?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool MatchesStatus(ClientUser user, string? status, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (status == "expiring")
        {
            return user.ExpiresAt is { } expiresAt
                && expiresAt > nowUtc
                && expiresAt <= nowUtc.AddDays(7);
        }

        return Enum.TryParse<ClientStatus>(status, true, out var parsed)
            && ClientUserService.GetStatus(user, nowUtc) == parsed;
    }
}
