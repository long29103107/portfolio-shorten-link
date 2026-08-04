using ShortenLink.Hosting;
using ShortenLink.Api;
using ShortenLink.Api.Endpoints;
using ShortenLink.Application.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddShortenLink(builder.Configuration);
builder.Services.AddScoped<ISecuritySessionService, SecuritySessionServiceAdapter>();
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

app.MapShortenLinkEndpoints();
app.MapAuditLogEndpoints();
app.MapRateLimitEndpoints();
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

await app.RunAsync();

public partial class Program;
