using System.Security.Cryptography;
using System.Text;

namespace NN.FeatureFlags;

internal static class Rollout
{
    public static bool Includes(string flagKey, string userId, int percentage)
    {
        var input = Encoding.UTF8.GetBytes($"{flagKey}\0{userId}");
#if NET8_0_OR_GREATER
        var hash = SHA256.HashData(input);
#else
        byte[] hash;
        using (var sha256 = SHA256.Create())
        {
            hash = sha256.ComputeHash(input);
        }
#endif

        var value =
            ((uint)hash[0] << 24) |
            ((uint)hash[1] << 16) |
            ((uint)hash[2] << 8) |
            hash[3];

        var bucket = (uint)(((ulong)value * 100UL) >> 32);

        return bucket < (uint)percentage;
    }
}
