using RecipesApp.Services.Drafts;
using Shouldly;

namespace Recipes.Tests.Unit;

public class DraftTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1 minute ago")]
    [InlineData(5 * 60, "5 minutes ago")]
    [InlineData(59 * 60 + 59, "59 minutes ago")]
    [InlineData(60 * 60, "1 hour ago")]
    [InlineData(3 * 60 * 60, "3 hours ago")]
    [InlineData(24 * 60 * 60, "1 day ago")]
    [InlineData(6 * 24 * 60 * 60, "6 days ago")]
    public void Describe_Age_UsesFriendlyRelativeTime(int secondsAgo, string expected)
    {
        DraftTime.Describe(Now.AddSeconds(-secondsAgo), Now).ShouldBe(expected);
    }
}
