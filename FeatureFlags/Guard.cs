namespace NN.FeatureFlags;

internal static class Guard
{
    public static void NotNull(object? value, string parameterName)
    {
#pragma warning disable CA1510
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
#pragma warning restore CA1510
    }
}
