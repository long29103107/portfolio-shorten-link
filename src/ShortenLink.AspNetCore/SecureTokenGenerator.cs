using System.Security.Cryptography;
using ShortenLink.Core.Abstractions;

namespace ShortenLink.AspNetCore;

internal sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    public string CreateToken(int byteCount)
    {
        if (byteCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount));
        }

        return Convert.ToHexString(RandomNumberGenerator.GetBytes(byteCount)).ToLowerInvariant();
    }

    public string CreateIdentifier() => Guid.CreateVersion7().ToString("N");
}
