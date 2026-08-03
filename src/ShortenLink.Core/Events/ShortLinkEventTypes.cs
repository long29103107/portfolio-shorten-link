namespace ShortenLink.Core.Events;

public static class ShortLinkEventTypes
{
    public const string Created = "short_link.created";
    public const string Updated = "short_link.updated";
    public const string Activated = "short_link.activated";
    public const string Deactivated = "short_link.deactivated";
    public const string Deleted = "short_link.deleted";
    public const string Redirected = "short_link.redirected";
    public const string Expired = "short_link.expired";
}
