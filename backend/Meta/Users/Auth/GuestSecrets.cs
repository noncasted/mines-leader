using System.Buffers.Text;
using System.Security.Cryptography;

namespace Meta.Users;

/// <summary>
/// S3: the secret of a guest - 32 random bytes, base64url on the wire and in the client storage. The server keeps
/// only SHA-256 of the bytes (base64) and compares hashes in constant time. It is not the user id: the id is
/// public enough (logs, the console), the secret is known only to the client it was issued to.
/// </summary>
public static class GuestSecrets
{
    public const int SecretSize = 32;

    public static (string Secret, string Hash) Generate()
    {
        Span<byte> secret = stackalloc byte[SecretSize];
        RandomNumberGenerator.Fill(secret);

        return (Base64Url.EncodeToString(secret), Hash(secret));
    }

    // False for anything that is not exactly SecretSize bytes of base64url: such a secret was never issued.
    public static bool TryHash(string secret, out string hash)
    {
        hash = string.Empty;

        if (string.IsNullOrEmpty(secret) == true)
            return false;

        if (Base64Url.IsValid(secret, out var length) == false || length != SecretSize)
            return false;

        Span<byte> bytes = stackalloc byte[SecretSize];

        if (Base64Url.TryDecodeFromChars(secret, bytes, out var written) == false || written != SecretSize)
            return false;

        hash = Hash(bytes);
        return true;
    }

    public static bool Matches(string storedHash, string hash)
    {
        if (storedHash.Length == 0 || hash.Length == 0)
            return false;

        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(storedHash), Convert.FromBase64String(hash));
    }

    private static string Hash(ReadOnlySpan<byte> secret)
    {
        return Convert.ToBase64String(SHA256.HashData(secret));
    }
}
