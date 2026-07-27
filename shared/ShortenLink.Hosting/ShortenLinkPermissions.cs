using ShortenLink.Core.Security;

namespace ShortenLink.Hosting;

public static class ShortenLinkPermissions
{
    public const string AdminOnly = "$admin";
    public const string ShortLinksRead = ShortenLinkPermissionCatalog.ShortLinksRead;
    public const string ShortLinksCreate = ShortenLinkPermissionCatalog.ShortLinksCreate;
    public const string ShortLinksUpdate = ShortenLinkPermissionCatalog.ShortLinksUpdate;
    public const string ShortLinksStatus = ShortenLinkPermissionCatalog.ShortLinksStatus;
    public const string ShortLinksDelete = ShortenLinkPermissionCatalog.ShortLinksDelete;
    public const string ShortLinksImport = ShortenLinkPermissionCatalog.ShortLinksImport;
    public const string AnalyticsRead = ShortenLinkPermissionCatalog.AnalyticsRead;
    public const string AuditLogsRead = ShortenLinkPermissionCatalog.AuditLogsRead;

    public static IReadOnlySet<string> All => ShortenLinkPermissionCatalog.All;
}

public static class ShortenLinkRoles
{
    public const string Admin = ShortenLinkSystemRoles.Admin;
    public const string User = ShortenLinkSystemRoles.User;
    public const string Owner = Admin;
    public const string Editor = User;
    public const string Viewer = User;

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> PermissionBundles =>
        ShortenLinkSystemRoles.PermissionBundles;
}
