using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.JSInterop;

namespace RecipesApp.Services.Drafts;

/// <summary>A restored draft and when it was saved.</summary>
public sealed record Draft<T>(T Value, DateTimeOffset SavedAt);

/// <summary>
/// Saves and restores unsaved page state ("drafts") so work survives a lost connection, a page
/// reload, the phone discarding the tab, or a server restart.
/// </summary>
/// <remarks>
/// Drafts are a convenience, never a source of truth, so every failure is handled by treating
/// the draft as missing: storage errors are logged and swallowed, and expired, corrupt, or
/// outdated drafts are deleted rather than shown.
/// </remarks>
public sealed class DraftService
{
    /// <summary>Bump when a draft's shape changes incompatibly; older drafts are then discarded.</summary>
    public const int FormatVersion = 1;

    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDraftStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DraftService> _logger;

    public DraftService(IDraftStore store, TimeProvider timeProvider, ILogger<DraftService> logger)
    {
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Saves a draft. Returns false (and logs) if the browser storage couldn't be written.</summary>
    public async Task<bool> SaveAsync<T>(string key, T value, DraftLifetime lifetime)
    {
        var envelope = new DraftEnvelope(
            FormatVersion,
            _timeProvider.GetUtcNow(),
            JsonSerializer.SerializeToElement(value, JsonOptions));

        try
        {
            await _store.SetAsync(key, JsonSerializer.Serialize(envelope, JsonOptions), lifetime);
            return true;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            _logger.LogWarning(ex, "Couldn't save draft {DraftKey}", key);
            return false;
        }
    }

    /// <summary>
    /// Returns the saved draft, or null if there is none or it can't be used (expired, corrupt,
    /// from an older format, or unreadable). Unusable drafts are deleted.
    /// </summary>
    public async Task<Draft<T>?> RestoreAsync<T>(string key, DraftLifetime lifetime, TimeSpan? maxAge = null)
    {
        string? stored;
        try
        {
            stored = await _store.GetAsync(key, lifetime);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            // For example, a CryptographicException if the data protection keys changed
            _logger.LogWarning(ex, "Couldn't read draft {DraftKey}; discarding it", key);
            await DiscardAsync(key, lifetime);
            return null;
        }

        if (stored == null)
            return null;

        DraftEnvelope? envelope;
        T? value;
        try
        {
            envelope = JsonSerializer.Deserialize<DraftEnvelope>(stored, JsonOptions);
            value = envelope == null ? default : envelope.Payload.Deserialize<T>(JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger.LogInformation("Discarding corrupt draft {DraftKey}", key);
            await DiscardAsync(key, lifetime);
            return null;
        }

        if (envelope == null || value == null || envelope.Version != FormatVersion)
        {
            _logger.LogInformation("Discarding unusable draft {DraftKey} (format version {Version})", key, envelope?.Version);
            await DiscardAsync(key, lifetime);
            return null;
        }

        if (_timeProvider.GetUtcNow() - envelope.SavedAt > (maxAge ?? DefaultMaxAge))
        {
            _logger.LogInformation("Discarding expired draft {DraftKey} saved at {SavedAt}", key, envelope.SavedAt);
            await DiscardAsync(key, lifetime);
            return null;
        }

        return new Draft<T>(value, envelope.SavedAt);
    }

    public async Task DiscardAsync(string key, DraftLifetime lifetime)
    {
        try
        {
            await _store.DeleteAsync(key, lifetime);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            _logger.LogWarning(ex, "Couldn't delete draft {DraftKey}", key);
        }
    }

    // Failures that come from the browser storage rather than from our own code: the JavaScript
    // call failed, the connection is gone, interop isn't available yet (prerendering), the value
    // can't be decrypted, or the call was cancelled.
    private static bool IsStorageFailure(Exception ex) =>
        ex is JSException
            or JSDisconnectedException
            or InvalidOperationException
            or CryptographicException
            or TaskCanceledException;

    private sealed record DraftEnvelope(int Version, DateTimeOffset SavedAt, JsonElement Payload);
}
