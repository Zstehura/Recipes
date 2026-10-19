using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Recipes.Tests.TestData;
using RecipesApp.Services.Drafts;
using Shouldly;

namespace Recipes.Tests.Unit;

public class DraftServiceTests
{
    private readonly InMemoryDraftStore _store = new();
    private readonly ManualTimeProvider _clock = new();
    private readonly DraftService _drafts;

    public DraftServiceTests()
    {
        _drafts = new DraftService(_store, _clock, NullLogger<DraftService>.Instance);
    }

    private sealed record SampleDraft(string Name, List<int> Ids);

    private static readonly SampleDraft Sample = new("Lasagna", new List<int> { 3, 1, 2 });

    [Fact]
    public async Task SaveThenRestore_ReturnsValueAndSavedTime()
    {
        await _drafts.SaveAsync("key", Sample, DraftLifetime.Device);
        _clock.Advance(TimeSpan.FromHours(2));

        var draft = await _drafts.RestoreAsync<SampleDraft>("key", DraftLifetime.Device);

        draft.ShouldNotBeNull();
        draft.Value.Name.ShouldBe("Lasagna");
        draft.Value.Ids.ShouldBe(new[] { 3, 1, 2 });
        draft.SavedAt.ShouldBe(_clock.Now - TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task Restore_NoDraft_ReturnsNull()
    {
        (await _drafts.RestoreAsync<SampleDraft>("missing", DraftLifetime.Device)).ShouldBeNull();
    }

    [Fact]
    public async Task Restore_SameKeyDifferentLifetime_AreSeparate()
    {
        await _drafts.SaveAsync("key", Sample, DraftLifetime.Tab);

        (await _drafts.RestoreAsync<SampleDraft>("key", DraftLifetime.Device)).ShouldBeNull();
        (await _drafts.RestoreAsync<SampleDraft>("key", DraftLifetime.Tab)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Restore_ExactlyAtMaxAge_IsStillRestored()
    {
        await _drafts.SaveAsync("key", Sample, DraftLifetime.Device);
        _clock.Advance(DraftService.DefaultMaxAge);

        (await _drafts.RestoreAsync<SampleDraft>("key", DraftLifetime.Device)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Restore_OlderThanMaxAge_ReturnsNullAndDeletesDraft()
    {
        await _drafts.SaveAsync("key", Sample, DraftLifetime.Device);
        _clock.Advance(DraftService.DefaultMaxAge + TimeSpan.FromSeconds(1));

        (await _drafts.RestoreAsync<SampleDraft>("key", DraftLifetime.Device)).ShouldBeNull();
        _store.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task Restore_CustomMaxAge_IsHonored()
    {
        await _drafts.SaveAsync("key", Sample, DraftLifetime.Tab);
        _clock.Advance(TimeSpan.FromMinutes(31));

        (await _drafts.RestoreAsync<SampleDraft>("key", DraftLifetime.Tab, maxAge: TimeSpan.FromMinutes(30))).ShouldBeNull();
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"version\":1,\"savedAt\":\"2026-10-01T12:00:00+00:00\",\"payload\":\"a string, not an object\"}")]
    [InlineData("{\"version\":1,\"savedAt\":\"2026-10-01T12:00:00+00:00\",\"payload\":null}")]
    public async Task Restore_CorruptOrMalformedDraft_ReturnsNullAndDeletesDraft(string stored)
    {
        _store.Values[(DraftLifetime.Device, "key")] = stored;

        (await _drafts.RestoreAsync<SampleDraft>("key", DraftLifetime.Device)).ShouldBeNull();
        _store.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task Restore_DraftFromOtherFormatVersion_ReturnsNullAndDeletesDraft()
    {
        _store.Values[(DraftLifetime.Device, "key")] =
            "{\"version\":0,\"savedAt\":\"2026-10-01T12:00:00+00:00\",\"payload\":{\"name\":\"Old\",\"ids\":[]}}";

        (await _drafts.RestoreAsync<SampleDraft>("key", DraftLifetime.Device)).ShouldBeNull();
        _store.Values.ShouldBeEmpty();
    }

    public static TheoryData<Exception> StorageFailures => new()
    {
        new CryptographicException("The payload was invalid."),   // data protection keys changed
        new JSException("localStorage is not available"),
        new JSDisconnectedException("The circuit is disconnected."),
        new InvalidOperationException("JavaScript interop calls cannot be issued at this time."),
        new TaskCanceledException(),
    };

    [Theory]
    [MemberData(nameof(StorageFailures))]
    public async Task Restore_StorageFails_ReturnsNullWithoutThrowing(Exception failure)
    {
        _store.FailWith = failure;

        (await _drafts.RestoreAsync<SampleDraft>("key", DraftLifetime.Device)).ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(StorageFailures))]
    public async Task Save_StorageFails_ReturnsFalseWithoutThrowing(Exception failure)
    {
        _store.FailWith = failure;

        (await _drafts.SaveAsync("key", Sample, DraftLifetime.Device)).ShouldBeFalse();
    }

    [Fact]
    public async Task Save_UnexpectedException_IsNotSwallowed()
    {
        // Only browser-storage failures are treated as "no draft"; real bugs still surface
        _store.FailWith = new ArgumentNullException("key");

        await Should.ThrowAsync<ArgumentNullException>(() => _drafts.SaveAsync("key", Sample, DraftLifetime.Device));
    }

    [Fact]
    public async Task Discard_RemovesDraft()
    {
        await _drafts.SaveAsync("key", Sample, DraftLifetime.Device);

        await _drafts.DiscardAsync("key", DraftLifetime.Device);

        (await _drafts.RestoreAsync<SampleDraft>("key", DraftLifetime.Device)).ShouldBeNull();
    }
}
