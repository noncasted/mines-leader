using System.Security.Cryptography;
using Shared;

namespace Cluster.Lobbies;

public static class LobbyCodeGenerator
{
    // 32^6 codes: a miss means the registry is almost full or the source of codes is broken.
    private const int MaxAttempts = 100;

    public static string Generate()
    {
        return RandomNumberGenerator.GetString(LobbyCode.Alphabet, LobbyCode.Length);
    }

    public static string GenerateUnique(Func<string, bool> isTaken)
    {
        return GenerateUnique(isTaken, Generate);
    }

    public static string GenerateUnique(Func<string, bool> isTaken, Func<string> generate)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var code = generate();

            if (LobbyCode.IsValid(code) == false)
                throw new InvalidOperationException($"Generated lobby code '{code}' is not valid");

            if (isTaken(code) == false)
                return code;
        }

        throw new InvalidOperationException($"No free lobby code found in {MaxAttempts} attempts");
    }
}
