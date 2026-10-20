using System.Collections.Immutable;

namespace NN.FeatureFlags;

public sealed class InMemoryFeatureFlagProvider : IFeatureFlagProvider
{
    private static readonly FeatureFlagSnapshot EmptySnapshot = new FeatureFlagSnapshot(
        string.Empty,
        ImmutableDictionary.Create<string, FeatureFlagDefinition>(StringComparer.Ordinal));

    private FeatureFlagSnapshot _snapshot;

    public InMemoryFeatureFlagProvider()
        : this(EmptySnapshot)
    {
    }

    public InMemoryFeatureFlagProvider(FeatureFlagSnapshot snapshot)
    {
        Guard.NotNull(snapshot, nameof(snapshot));
        _snapshot = snapshot;
    }

    public FeatureFlagSnapshot Snapshot
    {
        get => Volatile.Read(ref _snapshot);
        set
        {
            Guard.NotNull(value, nameof(value));
            Interlocked.Exchange(ref _snapshot, value);
        }
    }

    public Task<FeatureFlagSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(Volatile.Read(ref _snapshot));
    }
}
