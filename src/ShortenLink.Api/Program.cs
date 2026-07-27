using ShortenLink.Hosting;
using ShortenLink.Api;
using ShortenLink.Api.Endpoints;
using ShortenLink.Application.Features.ShortLinks.Create;
using ShortenLink.Application.Features.ShortLinks;
using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddShortenLink(builder.Configuration);
builder.Services.AddApplicationMediator(typeof(CreateShortLinkCommand).Assembly);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentRequestContext, HttpCurrentRequestContext>();
builder.Services.AddScoped<ISecuritySessionService, SecuritySessionServiceAdapter>();
builder.Services.AddScoped<ShortLinkAccessGuard>();
builder.Services.AddScoped<ShortLinkAuditWriter>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

}

app.UseRateLimiter();
app.UseExceptionHandler();

app.MapShortLinkManagementEndpoints();
app.MapAuditLogEndpoints();
app.MapRedirectEndpoints();
app.MapSecuritySessionEndpoints();
app.MapSecurityApiKeyEndpoints();
app.MapSecurityRoleEndpoints();
app.MapSecurityUserEndpoints();
app.MapSecurityAssignmentEndpoints();
app.MapHealthEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapMockDataEndpoints();
}

app.Run();

public partial class Program;
