using System.Security.Cryptography;
using System.Text;

namespace NN.FeatureFlags;

internal static class Rollout
{
    public static bool Includes(string flagKey, string userId, int percentage)
    {
        var input = Encoding.UTF8.GetBytes($"{flagKey}\0{userId}");
        var hash = SHA256.HashData(input);

        var value =
            ((uint)hash[0] << 24) |
            ((uint)hash[1] << 16) |
            ((uint)hash[2] << 8) |
            hash[3];

        var bucket = (uint)(((ulong)value * 100UL) >> 32);

        return bucket < (uint)percentage;
    }
}
