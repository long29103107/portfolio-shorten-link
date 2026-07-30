namespace ShortenLink.Hosting;

public sealed class ShortenLinkEndpointOptions
{
    public string ManagementRoutePrefix { get; set; } = "/api/short-links";
    public string RedirectRoutePrefix { get; set; } = string.Empty;
    public bool MapManagementEndpoints { get; set; } = true;
    public bool MapRedirectEndpoint { get; set; } = true;
    public string? AuthorizationPolicyName { get; set; }
}
