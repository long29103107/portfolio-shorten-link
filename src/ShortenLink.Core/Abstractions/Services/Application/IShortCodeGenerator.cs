using ShortenLink.Core.Generation;

namespace ShortenLink.Core.Abstractions;

public interface IShortCodeGenerator
{
    string Generate(int length = Base62ShortCodeGenerator.DefaultCodeLength);
}
