namespace NN.FeatureFlags;

/// <summary>
/// Evaluates feature flags against an in-memory snapshot without performing I/O.
/// </summary>
public interface IFeatureFlagEvaluator
{
    /// <summary>
    /// Determines whether the specified flag is enabled for the given context.
    /// Unknown flags evaluate to <see langword="false"/>.
    /// </summary>
    /// <param name="key">The flag key.</param>
    /// <param name="context">The evaluation context, or <see langword="null"/> for no subject.</param>
    /// <returns><see langword="true"/> when the flag is enabled for the context.</returns>
    bool IsEnabled(string key, FeatureFlagContext? context = null);

    /// <summary>
    /// Determines whether the specified flag is enabled, returning <paramref name="defaultValue"/>
    /// when the flag is unknown.
    /// </summary>
    /// <param name="key">The flag key. Blank keys always return <see langword="false"/>.</param>
    /// <param name="defaultValue">The value to return when the flag is not present in the snapshot.</param>
    /// <param name="context">The evaluation context, or <see langword="null"/> for no subject.</param>
    /// <returns><see langword="true"/> when the flag is enabled for the context.</returns>
    bool IsEnabled(string key, bool defaultValue, FeatureFlagContext? context = null);
}
