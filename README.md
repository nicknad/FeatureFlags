# NN.FeatureFlags

A small, dependency-light feature-flag library for .NET Standard 2.0 and .NET 8+. It separates three concerns cleanly:

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

## Installation

```shell
dotnet add package NN.FeatureFlags
```

Targets `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0`.

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
The hosted service is registered once, so calling the method more than once does not start a second poller.

Prefer to supply the provider yourself? The same services are wired by the other overloads:

```csharp
// A provider instance.
builder.Services.AddFeatureFlags(new InMemoryFeatureFlagProvider());

// A provider factory, invoked once by the container.
builder.Services.AddFeatureFlags(sp => new SqlFlagProvider(sp.GetRequiredService<AppDb>()));

// No background polling: call IFeatureFlagRefresher.RefreshAsync yourself (tests, workers).
builder.Services.AddFeatureFlags(new InMemoryFeatureFlagProvider(), enableBackgroundRefresh: false);
```

An `InMemoryFeatureFlagProvider` ships with the package for tests and simple hosts. Replace its
`Snapshot` property to publish new definitions.

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
- Unknown keys are fail-closed. Use `IsEnabled("key", defaultValue: true, context)` when an
  unknown flag should fail open; blank keys always return `false`.

Targeting policies have factories for the common single-dimension cases:

```csharp
TargetingPolicy.ForTenants("acme", "globex"); // allow-list only
TargetingPolicy.ForUsers("user-42");          // allow-list only
TargetingPolicy.Percentage(25);               // percentage only
```

### Partial (percentage) rollout

Set `RolloutPercentage` to a value between `0` and `100` to slowly ramp a feature. Allow-lists always
take precedence, so you can keep specific tenants/users on and roll out to everyone else by percentage:

```csharp
var definition = new FeatureFlagDefinition(
    Key: "new-checkout",
    IsEnabled: true,
    Targeting: new TargetingPolicy(
        TenantAllowList: new HashSet<string> { "acme" },    // always on for the acme tenant
        UserAllowList: new HashSet<string> { "user-42" },   // always on for user-42
        RolloutPercentage: 25));                            // everyone else: 25% of users
```

Evaluate it by supplying a stable `TargetingKey` (which defaults to `UserId`); the bucket is derived from that key and the flag key:

```csharp
evaluator.IsEnabled("new-checkout", new FeatureFlagContext(UserId: "user-123")); // ~25%
evaluator.IsEnabled("new-checkout", new FeatureFlagContext(TenantId: "acme"));   // true (allow-list)
evaluator.IsEnabled("new-checkout", new FeatureFlagContext(TenantId: "other"));  // false (no targeting key)

// Bucket anonymous/device traffic explicitly:
evaluator.IsEnabled(
    "new-checkout",
    new FeatureFlagContext(TenantId: "other") { TargetingKey = "device-7" });    // ~25%
```

A context also exposes an `Attributes` bag for custom attributes and a `TargetingKey` override:

```csharp
var context = new FeatureFlagContext(UserId: "user-123", TenantId: "acme")
{
    TargetingKey = "account-42",
    Attributes = new Dictionary<string, string> { ["region"] = "eu" },
};
```

Notes:

- `0` keeps the rollout off; `100` turns it on for every subject, including anonymous contexts.
- A **partial** rollout requires a `TargetingKey` (or `UserId`); a tenant-only context without either evaluates to `false`.
- Bucketing is deterministic (SHA-256 of the flag key and user id) — stable across processes and restarts.
- It is **not** a secret or an access control; a subject that can choose its own id can influence its bucket.

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
- Refreshes are single-flight: concurrent callers await the same in-flight refresh instead of starting another.
- `RefreshTimeout` bounds the provider call even when the provider ignores cancellation. A timed-out provider is abandoned so later refreshes can proceed, and its late result is never published.
- Refresh failures are logged and the previous (last-good) snapshot is retained.
- Options are validated on host startup: a non-positive `RefreshInterval` or `RefreshTimeout` fails
  startup. As a fallback (for example when constructing the service directly), non-positive values
  are clamped to the defaults and logged; values above ~24.8 days are clamped.

### Refreshing on demand

Inject `IFeatureFlagRefresher` and call `RefreshAsync()` to publish a new snapshot immediately:

```csharp
public sealed class FlagAdmin(IFeatureFlagRefresher refresher)
{
    public async Task<bool> ReloadAsync(CancellationToken ct)
    {
        var result = await refresher.RefreshAsync(ct);
        return result.Updated;
    }
}
```

`FeatureFlagRefreshOutcome` is `Updated`, `Unchanged` (the provider reported a version that is
already published), or `Superseded` (a newer refresh took over after this one timed out).
Provider and validation failures throw; a `TimeoutException` is thrown when the provider exceeds
`RefreshTimeout`. The hosted service catches and logs them and retains the last-good snapshot.

### Inspecting the current snapshot

The evaluator also implements `IFeatureFlagSnapshotSource`:

```csharp
public interface IFeatureFlagSnapshotSource
{
    event EventHandler<FeatureFlagSnapshotChangedEventArgs>? Changed;
    bool IsLoaded { get; }
    FeatureFlagSnapshot GetSnapshot();
    Task WaitForLoadedAsync(CancellationToken cancellationToken = default);
}
```

- `GetSnapshot()` returns the last published snapshot (flags plus its `Version`), or an empty snapshot before the first successful refresh.
- `Changed` fires after a successful publication. A throwing subscriber does not fail the refresh; the failure is reported through `Trace`.
- When consecutive refreshes report the same non-empty `Version`, publication (and validation) is skipped. Providers that do not supply a version are republished on every refresh.
- `IsLoaded` is `false` until a snapshot has been published; `WaitForLoadedAsync` completes on the first successful publication and honors cancellation. Use it at startup when evaluations must be deterministic.

```csharp
public sealed class Startup(IFeatureFlagSnapshotSource source)
{
    public Task EnsureLoadedAsync(CancellationToken ct) => source.WaitForLoadedAsync(ct);
}
```

## Robustness and security

- **Atomic publication.** Snapshots are validated and frozen before an atomic swap, so readers never observe a partially updated configuration.
- **Snapshot validation.** A null snapshot or flag map, a duplicate or empty flag key, an out-of-range rollout percentage, or an empty/whitespace allow-list entry is rejected. The refresh fails and the previous last-good snapshot is retained.
- **Fail-closed evaluation.** Unknown keys, disabled flags, and unloaded configuration evaluate to `false`.
- **No I/O on the evaluation path.** `IsEnabled` touches only an immutable in-memory snapshot and never blocks.
- **Bounded timers.** Non-positive interval/timeout values fall back to the defaults; values above ~24.8 days are clamped so timers cannot throw at runtime.
- **Trust boundary.** `IFeatureFlagProvider` output is trusted input: source flag definitions and allow-lists from a controlled system, never from untrusted user input.
- **Not an authorization control.** The percentage rollout is deterministic (SHA-256) but not secret; a subject that can choose its own identifier can influence its bucket. Do not use rollout as an access-control or anti-abuse mechanism.
- **Kill switches.** A failed refresh keeps the last-good snapshot, so a flag that is `true` stays `true`. If a flag acts as a fail-safe kill switch, design the consuming code accordingly.

## License

MIT — see [LICENSE](LICENSE).
