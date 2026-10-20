using System.Collections.Immutable;

namespace NN.FeatureFlags;

/// <summary>
/// An in-memory feature-flag provider for tests and simple hosts.
/// </summary>
public sealed class InMemoryFeatureFlagProvider : IFeatureFlagProvider
{
    private static readonly FeatureFlagSnapshot EmptySnapshot = new FeatureFlagSnapshot(
        string.Empty,
        ImmutableDictionary.Create<string, FeatureFlagDefinition>(StringComparer.Ordinal));

    private FeatureFlagSnapshot _snapshot;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryFeatureFlagProvider"/> class with an empty snapshot.
    /// </summary>
    public InMemoryFeatureFlagProvider()
        : this(EmptySnapshot)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryFeatureFlagProvider"/> class with a snapshot.
    /// </summary>
    /// <param name="snapshot">The initial snapshot.</param>
    public InMemoryFeatureFlagProvider(FeatureFlagSnapshot snapshot)
    {
        Guard.NotNull(snapshot, nameof(snapshot));
        _snapshot = snapshot;
    }

    /// <summary>
    /// Gets or sets the snapshot returned by <see cref="GetSnapshotAsync"/>.
    /// </summary>
    public FeatureFlagSnapshot Snapshot
    {
        get => Volatile.Read(ref _snapshot);
        set
        {
            Guard.NotNull(value, nameof(value));
            Interlocked.Exchange(ref _snapshot, value);
        }
    }

    /// <inheritdoc/>
    public Task<FeatureFlagSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(Volatile.Read(ref _snapshot));
    }
}
