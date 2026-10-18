# NN.FeatureFlags

A small, dependency-light feature-flag library for .NET 10. It separates three concerns cleanly:

- **Evaluation** — a synchronous, allocation-light, thread-safe evaluator that never does I/O.
- **Targeting** — typed, validated policies (tenant/user allow-lists and deterministic percentage rollout).
- **Synchronization** — a provider abstraction plus a background refresher that atomically publishes new snapshots.

## Features

- Synchronous `IsEnabled` against an immutable in-memory snapshot (no `await` on the hot path).
- Atomic snapshot publication — evaluations never observe a half-updated configuration.
- Deep immutability — nested allow-lists are frozen at publish time.
- Deterministic percentage rollout (stable across processes and restarts).
- Provider-agnostic: SQL, config files, remote services, or in-memory.
- Fail-closed: unknown flags and unloaded configuration evaluate to `false`.
- Background refresh with a configurable interval and timeout.


## Quick start

### 1. Implement a provider

An `IFeatureFlagProvider` returns a complete, internally consistent snapshot. The `Version` identifies the snapshot (a revision number, ETag, hash, …) and is used by you, not by the evaluator.

```csharp
using NN.FeatureFlags;

public sealed class InMemoryProvider : IFeatureFlagProvider
{
    private static readonly IReadOnlyDictionary<string, FeatureFlagDefinition> Flags =
        new Dictionary<string, FeatureFlagDefinition>
        {
            ["new-checkout"] = new FeatureFlagDefinition(
                Key: "new-checkout",
                IsEnabled: true,
                Targeting: new TargetingPolicy(
                    TenantAllowList: new HashSet<string> { "acme" },
                    UserAllowList: new HashSet<string> { "user-42" },
                    RolloutPercentage: 25)),
        };

    public Task<FeatureFlagSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new FeatureFlagSnapshot("v1", Flags));
}
```

### 2. Register

```csharp
builder.Services.AddFeatureFlags<InMemoryProvider>(options =>
{
    options.RefreshInterval = TimeSpan.FromSeconds(30);
    options.RefreshTimeout = TimeSpan.FromSeconds(5);
});
```

`AddFeatureFlags<TProvider>` registers the provider, the evaluator, the refresher, and a hosted
service that performs an initial refresh (off the startup thread) and then refreshes on the interval.

### 3. Evaluate

```csharp
using NN.FeatureFlags;

public sealed class CheckoutService(IFeatureFlagEvaluator evaluator)
{
    public bool UseNewCheckout(string? userId, string? tenantId)
        => evaluator.IsEnabled(
            "new-checkout",
            new FeatureFlagContext(UserId: userId, TenantId: tenantId));
}
```

## Evaluation and targeting semantics

Evaluation is synchronous and lock-free. Given a key and an optional `FeatureFlagContext`, the result is:

1. Unknown key → `false`.
2. Flag disabled (`IsEnabled == false`) → `false`.
3. No targeting policy → `true`.
4. Tenant is in `TenantAllowList` → `true`.
5. User is in `UserAllowList` → `true`.
6. `RolloutPercentage >= 100` → `true` (every subject, including anonymous).
7. Otherwise, if a user is present, run the percentage rollout.
8. Fallback → `false`.

Notes:

- Allow-lists and rollout are **OR** conditions; `IsEnabled` is a master gate.
- `null` targeting means unrestricted; an explicitly configured policy means targeted access.
- A tenant-only context cannot participate in a **partial** (0 < p < 100) rollout → `false`.

## Configuration and synchronization

```csharp
public sealed class FeatureFlagOptions
{
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(120);
    public TimeSpan RefreshTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

- Configured through the standard Options pattern (`IOptions<FeatureFlagOptions>`), so
  `services.Configure<FeatureFlagOptions>(...)` also works.
- The provider is resolved from a DI scope on every refresh, so scoped providers are supported.
- Concurrent refreshes are coalesced; an in-flight refresh makes later callers return immediately.
- Refresh failures are logged and the previous (last-good) snapshot is retained.
- Non-positive interval/timeout values are clamped to the defaults and logged.

### Refreshing on demand

Inject `IFeatureFlagRefresher` and call `RefreshAsync()` to publish a new snapshot immediately:

```csharp
public sealed class FlagAdmin(IFeatureFlagRefresher refresher)
{
    public Task ReloadAsync(CancellationToken ct) => refresher.RefreshAsync(ct);
}
```

## License

MIT — see [LICENSE](LICENSE).
