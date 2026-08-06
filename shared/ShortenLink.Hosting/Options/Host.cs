namespace ShortenLink.Hosting;

public sealed class ShortenLinkHostOptions
{
    public bool RedirectOnly { get; set; }

    public bool UseExternalPersistence { get; set; }
}
