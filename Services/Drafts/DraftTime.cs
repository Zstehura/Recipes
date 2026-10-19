namespace RecipesApp.Services.Drafts;

public static class DraftTime
{
    /// <summary>
    /// Describes when a draft was saved relative to now ("just now", "5 minutes ago"). Relative
    /// times avoid showing the server's time zone, which may not match the user's.
    /// </summary>
    public static string Describe(DateTimeOffset savedAt, DateTimeOffset now)
    {
        var age = now - savedAt;
        if (age < TimeSpan.FromMinutes(1))
            return "just now";
        if (age < TimeSpan.FromHours(1))
            return Plural((int)age.TotalMinutes, "minute") + " ago";
        if (age < TimeSpan.FromDays(1))
            return Plural((int)age.TotalHours, "hour") + " ago";
        return Plural((int)age.TotalDays, "day") + " ago";
    }

    private static string Plural(int count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";
}
