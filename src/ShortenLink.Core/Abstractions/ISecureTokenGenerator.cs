namespace ShortenLink.Core.Abstractions;

public interface ISecureTokenGenerator
{
    string CreateToken(int byteCount);

    string CreateIdentifier();
}
