using ShortenLink.Hosting;
using ShortenLink.Api;
using ShortenLink.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddShortenLink(builder.Configuration);
var corsOrigins = builder.Configuration
    .GetSection("ShortenLink:Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy("ShortenLinkFrontend", policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));
}
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
if (corsOrigins.Length > 0)
{
    app.UseCors("ShortenLinkFrontend");
}

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
