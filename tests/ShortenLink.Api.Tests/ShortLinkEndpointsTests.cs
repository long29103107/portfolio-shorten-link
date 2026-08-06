using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ShortenLink.Api;
using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Contracts.Responses;
using ShortenLink.Hosting;
using ShortenLink.Core.Domain;
using ShortenLink.Infrastructure.Persistence.Entities;
using ShortenLink.Core.Services;
using ShortenLink.Core.Contracts.Responses;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Infrastructure.Repositories;
using ShortenLink.Messaging;
using Xunit;

namespace ShortenLink.Api.Tests;

public sealed class ShortLinkEndpointsTests
{
    [Fact]
    public async Task ExpirationExecution_RequiresExplicitTriggerAndPersistsCheckpoint()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/short-links/expiration/execute",
            new
            {
                evaluatedAtUtc = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
                limit = 10,
                resumeFromCheckpoint = false
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.GetProperty("checkpointAdvanced").GetBoolean());
        Assert.Equal(0, payload.RootElement.GetProperty("cacheInvalidationHandoffs").GetInt32());
    }

    [Fact]
    public async Task AuditLogs_RecordEverySuccessfulShortLinkMutationExactlyOnce()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();
        await factory.UpsertSecurityUserAsync(
            "shared-user",
            "shared@example.com",
            "Shared User",
            "password",
            [ShortenLinkRoles.User],
            isEnabled: true);

        var created = await CreateShortLinkAsync(client, "https://example.com/audit-secret-url");
        using var updateResponse = await client.PutAsJsonAsync($"/api/short-links/{created.Code}", new
        {
            originalUrl = "https://example.com/updated-secret-url",
            expiredAtUtc = new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero)
        });
        using var deactivateResponse = await client.PostAsync(
            $"/api/short-links/{created.Code}/deactivate",
            null);
        using var activateResponse = await client.PostAsync(
            $"/api/short-links/{created.Code}/activate",
            null);
        using var grantResponse = await client.PutAsJsonAsync($"/api/short-links/{created.Code}/shares", new
        {
            username = "shared@example.com",
            access = "View"
        });
        using var updateShareResponse = await client.PutAsJsonAsync($"/api/short-links/{created.Code}/shares", new
        {
            username = "shared@example.com",
            access = "Edit"
        });
        using var revokeResponse = await client.DeleteAsync(
            $"/api/short-links/{created.Code}/shares/shared-user");
        using var deleteResponse = await client.DeleteAsync($"/api/short-links/{created.Code}");

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updateShareResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var auditFilter = Uri.EscapeDataString($"(TargetId eq `{created.Code}`)");
        var payload = await WaitForAuditPayloadAsync(
            client,
            $"/api/audit-logs?limit=20&fe={auditFilter}",
            audit => audit.Items.Count == 8);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(8, payload.Items.Count);
        Assert.All(
            new[]
            {
                ShortLinkAuditActions.Created,
                ShortLinkAuditActions.Updated,
                ShortLinkAuditActions.Deactivated,
                ShortLinkAuditActions.Activated,
                ShortLinkAuditActions.ShareGranted,
                ShortLinkAuditActions.ShareUpdated,
                ShortLinkAuditActions.ShareRevoked,
                ShortLinkAuditActions.Deleted
            },
            action => Assert.Single(payload.Items, item => item.Action == action));
        Assert.DoesNotContain("audit-secret-url", json, StringComparison.Ordinal);
        Assert.DoesNotContain("updated-secret-url", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditLogs_SupportDeterministicCursorAndFilters()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();
        var first = await CreateShortLinkAsync(client, "https://example.com/first");
        var second = await CreateShortLinkAsync(client, "https://example.com/second");

        var auditFilter = Uri.EscapeDataString(
            "(Action eq `short_link.created`) & (ActorId eq `system:admin`)");
        _ = await WaitForAuditPayloadAsync(
            client,
            $"/api/audit-logs?limit=200&fe={auditFilter}",
            audit => audit.Items.Count == 2);
        using var firstPageResponse = await client.GetAsync(
            $"/api/audit-logs?limit=1&fe={auditFilter}");
        var firstPage = await firstPageResponse.Content
            .ReadFromJsonAsync<ShortLinkAuditEventsResponse>();
        Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
        Assert.NotNull(firstPage);
        Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);

        using var secondPageResponse = await client.GetAsync(
            $"/api/audit-logs?limit=1&cursor={Uri.EscapeDataString(firstPage.NextCursor)}&fe={Uri.EscapeDataString("(Action eq `short_link.created`)")}");
        var secondPage = await secondPageResponse.Content
            .ReadFromJsonAsync<ShortLinkAuditEventsResponse>();

        Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
        Assert.NotNull(secondPage);
        Assert.Single(secondPage.Items);
        Assert.NotEqual(firstPage.Items[0].Id, secondPage.Items[0].Id);
        Assert.Equal(
            new[] { first.Code, second.Code }.OrderBy(code => code),
            new[] { firstPage.Items[0].TargetId, secondPage.Items[0].TargetId }.OrderBy(code => code));
    }

    [Fact]
    public async Task AuditLogs_ReturnUnauthorizedAndForbiddenUsingExistingErrorContract()
    {
        await using var unauthorizedFactory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var unauthorizedClient = unauthorizedFactory.CreateClient();
        using var unauthorizedResponse = await unauthorizedClient.GetAsync("/api/audit-logs");
        var unauthorized = await unauthorizedResponse.Content
            .ReadFromJsonAsync<ShortLinkErrorResponse>();

        await using var forbiddenFactory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true,
            securityRoles: Array.Empty<string>(),
            securityPermissions: Array.Empty<string>());
        using var forbiddenClient = forbiddenFactory.CreateClient();
        forbiddenClient.DefaultRequestHeaders.Add(
            "X-ShortenLink-Api-Key",
            "test-admin-key");
        using var forbiddenResponse = await forbiddenClient.GetAsync("/api/audit-logs");
        var forbidden = await forbiddenResponse.Content
            .ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);
        Assert.Equal(ErrorCodes.Unauthorized, unauthorized?.ErrorCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal(ErrorCodes.Forbidden, forbidden?.ErrorCode);
    }

    [Fact]
    public async Task AuditLogs_RecordIdentityAndSecurityProducersWithoutSecretsAndEnforceUserScope()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();
        var adminToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        using var createUserResponse = await client.PutAsJsonAsync("/api/security/users", new
        {
            id = "audit-user",
            username = "audit-user",
            displayName = "Audit User",
            password = "audit-password",
            roleIds = new[] { ShortenLinkRoles.User },
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, createUserResponse.StatusCode);

        using var createRoleResponse = await client.PutAsJsonAsync("/api/security/roles/custom", new
        {
            id = "audit-role",
            name = "Audit Role",
            permissions = new[] { ShortenLinkPermissions.ShortLinksRead },
            isEnabled = true
        });
        using var updateRoleResponse = await client.PutAsJsonAsync("/api/security/roles/custom", new
        {
            id = "audit-role",
            name = "Updated Audit Role",
            permissions = new[]
            {
                ShortenLinkPermissions.ShortLinksRead,
                ShortenLinkPermissions.AnalyticsRead
            },
            isEnabled = true
        });
        using var overrideRoleResponse = await client.PutAsJsonAsync(
            "/api/security/roles/audit-role/permission-overrides",
            new
            {
                overrides = new[]
                {
                    new
                    {
                        permission = ShortenLinkPermissions.ShortLinksRead,
                        isAllowed = false
                    }
                }
            });
        using var deleteRoleResponse = await client.DeleteAsync(
            "/api/security/roles/custom/audit-role");
        Assert.Equal(HttpStatusCode.OK, createRoleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updateRoleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, overrideRoleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deleteRoleResponse.StatusCode);

        const string credentialKey = "audit-assignment-secret";
        using var createAssignmentResponse = await client.PutAsJsonAsync(
            "/api/security/assignments",
            new
            {
                name = "Audit Assignment",
                credentialKey,
                roles = new[] { ShortenLinkRoles.User },
                permissions = Array.Empty<string>(),
                isEnabled = true
            });
        var assignment = await createAssignmentResponse.Content
            .ReadFromJsonAsync<SecurityAssignmentResponse>();
        Assert.Equal(HttpStatusCode.OK, createAssignmentResponse.StatusCode);
        Assert.NotNull(assignment);

        using var updateAssignmentResponse = await client.PutAsJsonAsync(
            "/api/security/assignments",
            new
            {
                name = "Updated Audit Assignment",
                credentialKey,
                roles = new[] { ShortenLinkRoles.User },
                permissions = new[] { ShortenLinkPermissions.AuditLogsRead },
                isEnabled = true
            });
        using var disableAssignmentResponse = await client.PostAsync(
            $"/api/security/assignments/{assignment.CredentialKeyHash}/disable",
            null);
        Assert.Equal(HttpStatusCode.OK, updateAssignmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, disableAssignmentResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        using var failedLoginResponse = await client.PostAsJsonAsync("/api/security/login", new
        {
            username = "audit-user",
            password = "wrong-password"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, failedLoginResponse.StatusCode);

        using var loginResponse = await client.PostAsJsonAsync("/api/security/login", new
        {
            username = "audit-user",
            password = "audit-password"
        });
        var userLogin = await loginResponse.Content.ReadFromJsonAsync<SecurityLoginResponse>();
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(userLogin);

        using var refreshResponse = await client.PostAsJsonAsync("/api/security/refresh", new
        {
            refreshToken = userLogin.RefreshToken
        });
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<SecurityLoginResponse>();
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotNull(refreshed);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                refreshed.AccessToken);
        using var createApiKeyResponse = await client.PostAsJsonAsync(
            "/api/security/api-keys",
            new { displayName = "Audit automation" });
        var apiKey = await createApiKeyResponse.Content
            .ReadFromJsonAsync<SecurityUserApiKeyCreatedResponse>();
        Assert.Equal(HttpStatusCode.OK, createApiKeyResponse.StatusCode);
        Assert.NotNull(apiKey);

        using var renameApiKeyResponse = await client.PutAsJsonAsync(
            $"/api/security/api-keys/{apiKey.ApiKey.Id}",
            new { displayName = "Updated audit automation" });
        using var disableApiKeyResponse = await client.PostAsync(
            $"/api/security/api-keys/{apiKey.ApiKey.Id}/disable",
            null);
        Assert.Equal(HttpStatusCode.OK, renameApiKeyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, disableApiKeyResponse.StatusCode);

        var userAudit = await WaitForAuditPayloadAsync(
            client,
            "/api/audit-logs?limit=200",
            audit => audit.Items.Count == 5);
        var userAuditJson = JsonSerializer.Serialize(userAudit, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(5, userAudit.Items.Count);
        Assert.All(
            userAudit.Items,
            item =>
            {
                Assert.Equal("audit-user", item.OwnerUserId);
                Assert.Contains(
                    item.TargetType,
                    new[]
                    {
                        ShortLinkAuditTargetTypes.Authentication,
                        ShortLinkAuditTargetTypes.UserApiKey
                    });
            });
        Assert.Single(
            userAudit.Items,
            item => item.Action == ShortLinkAuditActions.AuthenticationLogin);
        Assert.Single(
            userAudit.Items,
            item => item.Action == ShortLinkAuditActions.AuthenticationRefresh);
        Assert.DoesNotContain(
            userAudit.Items,
            item => item.TargetType == ShortLinkAuditTargetTypes.SecurityUser);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        using var updateUserResponse = await client.PutAsJsonAsync("/api/security/users", new
        {
            id = "audit-user",
            username = "audit-user",
            displayName = "Updated Audit User",
            password = (string?)null,
            roleIds = new[] { ShortenLinkRoles.User },
            isEnabled = true
        });
        using var disableUserResponse = await client.PostAsync(
            "/api/security/users/audit-user/disable",
            null);
        Assert.Equal(HttpStatusCode.OK, updateUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, disableUserResponse.StatusCode);

        var adminAudit = await WaitForAuditPayloadAsync(
            client,
            "/api/audit-logs?limit=200",
            audit => audit.Items.Count == 16);
        var adminAuditJson = JsonSerializer.Serialize(adminAudit, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(16, adminAudit.Items.Count);
        Assert.Equal(
            2,
            adminAudit.Items.Count(
                item => item.Action == ShortLinkAuditActions.AuthenticationLogin));
        Assert.All(
            new[]
            {
                ShortLinkAuditActions.AuthenticationRefresh,
                ShortLinkAuditActions.UserApiKeyCreated,
                ShortLinkAuditActions.UserApiKeyRenamed,
                ShortLinkAuditActions.UserApiKeyDisabled,
                ShortLinkAuditActions.SecurityUserCreated,
                ShortLinkAuditActions.SecurityUserUpdated,
                ShortLinkAuditActions.SecurityUserDisabled,
                ShortLinkAuditActions.SecurityRoleCreated,
                ShortLinkAuditActions.SecurityRoleUpdated,
                ShortLinkAuditActions.SecurityRolePermissionsReplaced,
                ShortLinkAuditActions.SecurityRoleDeleted,
                ShortLinkAuditActions.SecurityAssignmentCreated,
                ShortLinkAuditActions.SecurityAssignmentUpdated,
                ShortLinkAuditActions.SecurityAssignmentDisabled
            },
            action => Assert.Single(adminAudit.Items, item => item.Action == action));

        var assignmentEvents = adminAudit.Items
            .Where(item => item.TargetType == ShortLinkAuditTargetTypes.SecurityAssignment)
            .ToList();
        Assert.Equal(3, assignmentEvents.Count);
        Assert.Single(assignmentEvents.Select(item => item.TargetId).Distinct());
        Assert.True(Guid.TryParse(assignmentEvents[0].TargetId, out _));

        Assert.DoesNotContain("audit-password", adminAuditJson, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong-password", adminAuditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(userLogin.AccessToken, adminAuditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(userLogin.RefreshToken, adminAuditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(refreshed.AccessToken, adminAuditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(refreshed.RefreshToken, adminAuditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey.RawApiKey, adminAuditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(credentialKey, adminAuditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(
            assignment.CredentialKeyHash,
            adminAuditJson,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostCreate_ReturnsCreatedShortLink()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.PostAsJsonAsync("/api/short-links", new
        {
            originalUrl = "https://example.com/docs",
            expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
        });

        var payload = await response.Content.ReadFromJsonAsync<ShortLinkCreatedResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.Code));
        Assert.Equal(7, payload.Code.Length);
        Assert.Equal($"https://sho.rt/{payload.Code}", payload.ShortUrl);
        Assert.Equal("https://example.com/docs", payload.OriginalUrl);
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero), payload.CreatedAtUtc);
    }

    [Fact]
    public async Task PostCreate_GeneratesRandomCodesForRepeatedCreates()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var firstResponse = await client.PostAsJsonAsync("/api/short-links", new
        {
            originalUrl = "https://example.com/one",
            expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
        });
        using var secondResponse = await client.PostAsJsonAsync("/api/short-links", new
        {
            originalUrl = "https://example.com/two",
            expiredAtUtc = new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero)
        });

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<ShortLinkCreatedResponse>();
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<ShortLinkCreatedResponse>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.NotNull(firstPayload);
        Assert.NotNull(secondPayload);
        Assert.Equal(7, firstPayload.Code.Length);
        Assert.Equal(7, secondPayload.Code.Length);
        Assert.NotEqual(firstPayload.Code, secondPayload.Code);
    }

    [Fact]
    public async Task PostCreate_IdempotencyKeyReplaysOriginalLinkWithoutDuplicateAudit()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        static HttpRequestMessage CreateRequest() =>
            new(HttpMethod.Post, "/api/short-links")
            {
                Content = JsonContent.Create(new
                {
                    originalUrl = "https://example.com/idempotent",
                    expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
                })
            };

        using var firstRequest = CreateRequest();
        firstRequest.Headers.Add("Idempotency-Key", "api-create-123");
        using var firstResponse = await client.SendAsync(firstRequest);
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<ShortLinkCreatedResponse>();

        using var replayRequest = CreateRequest();
        replayRequest.Headers.Add("Idempotency-Key", "api-create-123");
        using var replayResponse = await client.SendAsync(replayRequest);
        var replayPayload = await replayResponse.Content.ReadFromJsonAsync<ShortLinkCreatedResponse>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.NotNull(firstPayload);
        Assert.NotNull(replayPayload);
        Assert.Equal(firstPayload.Code, replayPayload.Code);

        var auditFilter = Uri.EscapeDataString($"(TargetId eq `{firstPayload.Code}`)");
        var audit = await WaitForAuditPayloadAsync(
            client,
            $"/api/audit-logs?limit=20&fe={auditFilter}",
            payload => payload.Items.Count == 1);
        Assert.Single(audit.Items);
        Assert.Equal(ShortLinkAuditActions.Created, audit.Items[0].Action);
    }

    [Fact]
    public async Task PostImportDryRun_ReturnsPerItemErrorsWithoutPersistingLinks()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/short-links/import/dry-run",
            new
            {
                items = new object[]
                {
                    new
                    {
                        originalUrl = "https://example.com/import-valid",
                        expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
                        idempotencyKey = "batch-1"
                    },
                    new
                    {
                        originalUrl = "ftp://example.com/import-invalid",
                        expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
                        idempotencyKey = "batch-2"
                    },
                    new
                    {
                        originalUrl = "https://example.com/import-duplicate",
                        expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
                        idempotencyKey = "batch-1"
                    }
                }
            });
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkImportDryRunResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(3, payload.TotalCount);
        Assert.Equal(1, payload.ValidCount);
        Assert.Equal(2, payload.InvalidCount);
        Assert.Equal("invalid_url", payload.Items[1].ErrorCode);
        Assert.Equal("duplicate_idempotency_key", payload.Items[2].ErrorCode);

        using var listResponse = await client.GetAsync("/api/short-links?limit=10");
        var listPayload = await listResponse.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listPayload);
        Assert.Empty(listPayload.Items);
    }

    [Fact]
    public async Task PostImport_PersistsValidItemsAndContinuesAfterPerItemFailures()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/short-links/import",
            new
            {
                items = new object[]
                {
                    new
                    {
                        originalUrl = "https://example.com/import-execute-one",
                        expiredAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
                        idempotencyKey = "execute-1"
                    },
                    new
                    {
                        originalUrl = "ftp://example.com/import-execute-invalid",
                        expiredAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
                        idempotencyKey = "execute-invalid"
                    },
                    new
                    {
                        originalUrl = "https://example.com/import-execute-duplicate",
                        expiredAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
                        idempotencyKey = "execute-1"
                    },
                    new
                    {
                        originalUrl = "https://example.com/import-execute-two",
                        expiredAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
                        idempotencyKey = "execute-2"
                    }
                }
            });
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkImportExecutionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(4, payload.TotalCount);
        Assert.Equal(2, payload.SucceededCount);
        Assert.Equal(2, payload.FailedCount);
        Assert.Equal(0, payload.ReplayedCount);
        Assert.False(payload.Truncated);
        Assert.True(payload.Items[0].Succeeded);
        Assert.NotNull(payload.Items[0].ShortCode);
        Assert.Equal("invalid_url", payload.Items[1].ErrorCode);
        Assert.Equal("duplicate_idempotency_key", payload.Items[2].ErrorCode);
        Assert.True(payload.Items[3].Succeeded);
        Assert.NotNull(payload.Items[3].ShortCode);

        using var replayResponse = await client.PostAsJsonAsync(
            "/api/short-links/import",
            new
            {
                items = new[]
                {
                    new
                    {
                        originalUrl = "https://example.com/import-execute-one",
                        expiredAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
                        idempotencyKey = "execute-1"
                    }
                }
            });
        var replayPayload = await replayResponse.Content.ReadFromJsonAsync<ShortLinkImportExecutionResponse>();

        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.NotNull(replayPayload);
        Assert.Equal(1, replayPayload.SucceededCount);
        Assert.Equal(1, replayPayload.ReplayedCount);
        Assert.True(replayPayload.Items[0].Replayed);
        Assert.Equal(payload.Items[0].ShortCode, replayPayload.Items[0].ShortCode);

        using var listResponse = await client.GetAsync("/api/short-links?limit=10");
        var listPayload = await listResponse.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listPayload);
        Assert.Equal(2, listPayload.Items.Count);
    }

    [Fact]
    public async Task GetList_ReturnsRecentShortLinksForAdmin()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var first = await CreateShortLinkAsync(client, "https://example.com/one");
        var second = await CreateShortLinkAsync(client, "https://example.com/two");

        using var response = await client.GetAsync("/api/short-links?limit=10");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);
        Assert.Null(payload.NextCursor);

        var firstItem = Assert.Single(payload.Items, item => item.Code == first.Code);
        Assert.Equal(first.ShortUrl, firstItem.ShortUrl);
        Assert.Equal("https://example.com/one", firstItem.OriginalUrl);
        Assert.True(firstItem.IsActive);

        var secondItem = Assert.Single(payload.Items, item => item.Code == second.Code);
        Assert.Equal(second.ShortUrl, secondItem.ShortUrl);
        Assert.Equal("https://example.com/two", secondItem.OriginalUrl);
        Assert.True(secondItem.IsActive);
    }

    [Fact]
    public async Task GetExport_StreamsBoundedRecentRecordsWithoutPrivateFields()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();

        await CreateShortLinkAsync(client, "https://example.com/export-one");
        await CreateShortLinkAsync(client, "https://example.com/export-two");
        await CreateShortLinkAsync(client, "https://example.com/export-three");

        using var response = await client.GetAsync("/api/short-links/export?limit=2");
        var json = await response.Content.ReadAsStringAsync();
        var records = JsonSerializer.Deserialize<IReadOnlyList<ShortLinkExportRecord>>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(records);
        Assert.Equal(2, records.Count);
        Assert.True(records[0].CreatedAtUtc >= records[1].CreatedAtUtc);
        Assert.All(records, record => Assert.Equal("Admin", record.AccessLevel));
        Assert.DoesNotContain("idempotencyKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("createdBy", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetExport_RequiresReadPermission()
    {
        await using var missingCredentialFactory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var missingCredentialClient = missingCredentialFactory.CreateClient();
        using var unauthorized = await missingCredentialClient.GetAsync("/api/short-links/export");

        await using var missingPermissionFactory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true,
            securityRoles: Array.Empty<string>(),
            securityPermissions: Array.Empty<string>());
        using var missingPermissionClient = missingPermissionFactory.CreateClient();
        missingPermissionClient.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", "test-admin-key");
        using var forbidden = await missingPermissionClient.GetAsync("/api/short-links/export");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task TenantContext_IsolatesCreateImportListExportAndIdempotency()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            tenantHeaderContext: true);
        using var tenantAClient = factory.CreateClient();
        tenantAClient.DefaultRequestHeaders.Add("X-Test-Tenant-Id", "tenant-a");
        using var tenantBClient = factory.CreateClient();
        tenantBClient.DefaultRequestHeaders.Add("X-Test-Tenant-Id", "tenant-b");
        using var tenantAResolveClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        tenantAResolveClient.DefaultRequestHeaders.Add("X-Test-Tenant-Id", "tenant-a");
        using var tenantBResolveClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        tenantBResolveClient.DefaultRequestHeaders.Add("X-Test-Tenant-Id", "tenant-b");

        static HttpRequestMessage CreateRequest(string destination) =>
            new(HttpMethod.Post, "/api/short-links")
            {
                Content = JsonContent.Create(new
                {
                    originalUrl = destination,
                    expiredAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero)
                })
            };

        using var tenantARequest = CreateRequest("https://example.com/tenant-a");
        tenantARequest.Headers.Add("Idempotency-Key", "shared-key");
        using var tenantAResponse = await tenantAClient.SendAsync(tenantARequest);
        var tenantALink = await tenantAResponse.Content.ReadFromJsonAsync<ShortLinkCreatedResponse>();

        using var tenantBRequest = CreateRequest("https://example.com/tenant-b");
        tenantBRequest.Headers.Add("Idempotency-Key", "shared-key");
        using var tenantBResponse = await tenantBClient.SendAsync(tenantBRequest);
        var tenantBLink = await tenantBResponse.Content.ReadFromJsonAsync<ShortLinkCreatedResponse>();

        using var tenantAReplayRequest = CreateRequest("https://example.com/tenant-a");
        tenantAReplayRequest.Headers.Add("Idempotency-Key", "shared-key");
        using var tenantAReplayResponse = await tenantAClient.SendAsync(tenantAReplayRequest);

        using var importResponse = await tenantAClient.PostAsJsonAsync(
            "/api/short-links/import",
            new
            {
                items = new[]
                {
                    new
                    {
                        originalUrl = "https://example.com/tenant-a/imported",
                        expiredAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
                        idempotencyKey = "tenant-a-import"
                    }
                }
            });
        using var tenantAListResponse = await tenantAClient.GetAsync("/api/short-links?limit=10");
        var tenantAList = await tenantAListResponse.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();
        using var tenantBExportResponse = await tenantBClient.GetAsync("/api/short-links/export?limit=10");
        var tenantBExport = await tenantBExportResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<ShortLinkExportRecord>>();
        using var crossTenantDetails = await tenantBClient.GetAsync($"/api/short-links/{tenantALink!.Code}");
        using var tenantAResolve = await tenantAResolveClient.GetAsync($"/{tenantALink.Code}");
        using var tenantBResolve = await tenantBResolveClient.GetAsync($"/{tenantALink.Code}");
        var persisted = await factory.GetShortLinksAsync();

        Assert.Equal(HttpStatusCode.Created, tenantAResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, tenantBResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, tenantAReplayResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        Assert.NotNull(tenantBLink);
        Assert.NotEqual(tenantALink.Code, tenantBLink.Code);
        Assert.NotNull(tenantAList);
        Assert.Equal(2, tenantAList.Items.Count);
        Assert.NotNull(tenantBExport);
        Assert.Equal(tenantBLink.Code, Assert.Single(tenantBExport).Code);
        Assert.Equal(HttpStatusCode.Forbidden, crossTenantDetails.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, tenantAResolve.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, tenantBResolve.StatusCode);
        Assert.Equal(2, persisted.Count(link => link.TenantId == "tenant-a"));
        Assert.Equal(1, persisted.Count(link => link.TenantId == "tenant-b"));
    }

    [Fact]
    public async Task GetList_ReturnsUnauthorizedWhenSecurityEnabledAndApiKeyMissing()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/short-links?limit=10");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("unauthorized", payload.ErrorCode);
    }

    [Fact]
    public async Task GetList_ReturnsForbiddenWhenApiKeyLacksReadPermission()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true,
            securityRoles: Array.Empty<string>(),
            securityPermissions: Array.Empty<string>());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", "test-admin-key");

        using var response = await client.GetAsync("/api/short-links?limit=10");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("forbidden", payload.ErrorCode);
    }

    [Fact]
    public async Task GetList_ReturnsOkWhenApiKeyHasViewerRole()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true,
            securityRoles: new[] { ShortenLinkRoles.Viewer });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", "test-admin-key");

        using var response = await client.GetAsync("/api/short-links?limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetList_ReturnsOkWhenPersistedAssignmentHasViewerRole()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true,
            securityApiKey: "bootstrap-key",
            securityRoles: Array.Empty<string>(),
            securityPermissions: Array.Empty<string>());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", "persisted-viewer-key");
        await factory.UpsertSecurityAssignmentAsync(
            "persisted-viewer-key",
            new[] { ShortenLinkRoles.Viewer },
            Array.Empty<string>(),
            isEnabled: true);

        using var response = await client.GetAsync("/api/short-links?limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SecurityAssignments_CanBeUpsertedListedAndDisabledByOwner()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true,
            securityRoles: new[] { ShortenLinkRoles.Owner });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", "test-admin-key");

        using var upsertResponse = await client.PutAsJsonAsync("/api/security/assignments", new
        {
            name = "Managed Owner",
            credentialKey = "test-admin-key",
            roles = new[] { ShortenLinkRoles.Owner },
            permissions = Array.Empty<string>(),
            isEnabled = true
        });
        var upsertPayload = await upsertResponse.Content.ReadFromJsonAsync<SecurityAssignmentResponse>();

        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);
        Assert.NotNull(upsertPayload);
        Assert.Equal(HashCredential("test-admin-key"), upsertPayload.CredentialKeyHash);
        Assert.Equal("Managed Owner", upsertPayload.Name);
        Assert.True(upsertPayload.IsEnabled);
        Assert.Equal(new[] { ShortenLinkRoles.Owner }, upsertPayload.Roles);

        using var listResponse = await client.GetAsync("/api/security/assignments");
        var listJson = await listResponse.Content.ReadAsStringAsync();
        var listPayload = JsonSerializer.Deserialize<SecurityAssignmentsListResponse>(
            listJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listPayload);
        var listed = Assert.Single(listPayload.Items);
        Assert.Equal(upsertPayload.CredentialKeyHash, listed.CredentialKeyHash);
        Assert.DoesNotContain("test-admin-key", listJson, StringComparison.Ordinal);

        using var disableResponse = await client.PostAsync(
            $"/api/security/assignments/{upsertPayload.CredentialKeyHash}/disable",
            null);
        var disablePayload = await disableResponse.Content.ReadFromJsonAsync<SecurityAssignmentDisabledResponse>();

        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        Assert.NotNull(disablePayload);
        Assert.False(disablePayload.IsEnabled);

        using var protectedResponse = await client.GetAsync("/api/short-links?limit=10");
        var protectedPayload = await protectedResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        Assert.NotNull(protectedPayload);
        Assert.Equal("unauthorized", protectedPayload.ErrorCode);
    }

    [Fact]
    public async Task SecurityAssignments_ReturnUnauthorizedWhenApiKeyMissing()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/security/assignments");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("unauthorized", payload.ErrorCode);
    }

    [Fact]
    public async Task SecurityAssignments_ReturnForbiddenWhenApiKeyLacksManagePermission()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true,
            securityRoles: new[] { ShortenLinkRoles.Viewer });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", "test-admin-key");

        using var response = await client.GetAsync("/api/security/assignments");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("forbidden", payload.ErrorCode);
    }

    [Fact]
    public async Task SecurityAssignments_RejectUnknownRolesAndPermissions()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true,
            securityRoles: new[] { ShortenLinkRoles.Owner });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", "test-admin-key");

        using var roleResponse = await client.PutAsJsonAsync("/api/security/assignments", new
        {
            name = "Bad Role",
            credentialKey = "bad-role-key",
            roles = new[] { "CustomRole" },
            permissions = Array.Empty<string>(),
            isEnabled = true
        });
        var rolePayload = await roleResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();
        using var permissionResponse = await client.PutAsJsonAsync("/api/security/assignments", new
        {
            name = "Bad Permission",
            credentialKey = "bad-permission-key",
            roles = Array.Empty<string>(),
            permissions = new[] { "security.magic" },
            isEnabled = true
        });
        var permissionPayload = await permissionResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, roleResponse.StatusCode);
        Assert.NotNull(rolePayload);
        Assert.Equal("invalid_role", rolePayload.ErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, permissionResponse.StatusCode);
        Assert.NotNull(permissionPayload);
        Assert.Equal("invalid_permission", permissionPayload.ErrorCode);
    }

    [Fact]
    public async Task GetList_ReturnsUnauthorizedWhenPersistedAssignmentIsDisabled()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true,
            securityRoles: new[] { ShortenLinkRoles.Owner });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", "test-admin-key");
        await factory.UpsertSecurityAssignmentAsync(
            "test-admin-key",
            new[] { ShortenLinkRoles.Owner },
            Array.Empty<string>(),
            isEnabled: false);

        using var response = await client.GetAsync("/api/short-links?limit=10");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("unauthorized", payload.ErrorCode);
    }

    [Fact]
    public void SecurityRoles_ArePermissionBundles()
    {
        var viewerPermissions = ShortenLinkRoles.PermissionBundles[ShortenLinkRoles.User];

        Assert.Contains(ShortenLinkPermissions.ShortLinksRead, viewerPermissions);
        Assert.Contains(ShortenLinkPermissions.AnalyticsRead, viewerPermissions);
        Assert.Contains(ShortenLinkPermissions.ShortLinksDelete, viewerPermissions);
        Assert.Contains(ShortenLinkPermissions.AuditLogsRead, viewerPermissions);
    }

    [Fact]
    public async Task SecurityLogin_AuthenticatesBootstrapAdminAndAuthorizesProtectedApis()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();

        using var loginResponse = await client.PostAsJsonAsync("/api/security/login", new
        {
            email = "admin@shortenlink.local",
            password = "admin"
        });
        var loginJson = await loginResponse.Content.ReadAsStringAsync();
        var loginPayload = JsonSerializer.Deserialize<SecurityLoginResponse>(
            loginJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(loginPayload);
        Assert.False(string.IsNullOrWhiteSpace(loginPayload.Token));
        Assert.Equal("admin@shortenlink.local", loginPayload.User.Username);
        Assert.Contains(ShortenLinkRoles.Admin, loginPayload.User.Roles);
        Assert.Contains(ShortenLinkPermissions.AuditLogsRead, loginPayload.User.Permissions);
        Assert.DoesNotContain("PasswordHash", loginJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("admin:", loginJson, StringComparison.OrdinalIgnoreCase);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            loginPayload.Token);

        using var listResponse = await client.GetAsync("/api/short-links?limit=10");
        using var meResponse = await client.GetAsync("/api/security/me");
        var mePayload = await meResponse.Content.ReadFromJsonAsync<SecurityCurrentUserResponse>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.NotNull(mePayload);
        Assert.Equal("admin@shortenlink.local", mePayload.Username);
        Assert.Contains(ShortenLinkPermissions.ShortLinksRead, mePayload.Permissions);
    }

    [Fact]
    public async Task SecurityRefresh_RotatesTokenPairAndRefreshTokenCannotAuthorizeApis()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();

        using var loginResponse = await client.PostAsJsonAsync("/api/security/login", new
        {
            email = "admin@shortenlink.local",
            password = "admin"
        });
        var login = await loginResponse.Content.ReadFromJsonAsync<SecurityLoginResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
        Assert.Equal(login.Token, login.AccessToken);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            login.RefreshToken);
        using var rejectedMeResponse = await client.GetAsync("/api/security/me");
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedMeResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        using var refreshResponse = await client.PostAsJsonAsync("/api/security/refresh", new
        {
            refreshToken = login.RefreshToken
        });
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<SecurityLoginResponse>();

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotNull(refreshed);
        Assert.NotEqual(login.AccessToken, refreshed.AccessToken);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.Equal("admin@shortenlink.local", refreshed.User.Username);
    }

    [Fact]
    public async Task SecurityLogin_ReturnsGenericFailureForUnknownOrBadPassword()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();

        using var unknownResponse = await client.PostAsJsonAsync("/api/security/login", new
        {
            username = "missing",
            password = "admin"
        });
        var unknownPayload = await unknownResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();
        using var badPasswordResponse = await client.PostAsJsonAsync("/api/security/login", new
        {
            email = "admin@shortenlink.local",
            password = "wrong"
        });
        var badPasswordPayload = await badPasswordResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, unknownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, badPasswordResponse.StatusCode);
        Assert.NotNull(unknownPayload);
        Assert.NotNull(badPasswordPayload);
        Assert.Equal("invalid_login", unknownPayload.ErrorCode);
        Assert.Equal(unknownPayload.ErrorCode, badPasswordPayload.ErrorCode);
        Assert.Equal(unknownPayload.Message, badPasswordPayload.Message);
        Assert.Null(unknownPayload.FieldErrors);
        Assert.Null(badPasswordPayload.FieldErrors);
    }

    [Fact]
    public async Task SecurityLogin_ReturnsMultipleFieldErrorsWhenCredentialsAreMissing()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/security/login", new
        {
            username = "",
            password = ""
        });
        var responseJson = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<ShortLinkErrorResponse>(
            responseJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("invalid_login", payload.ErrorCode);
        Assert.Equal("Email or password is invalid.", payload.Message);
        Assert.NotNull(payload.FieldErrors);
        Assert.Equal(2, payload.FieldErrors.Count);
        Assert.Contains("email", payload.FieldErrors.Keys);
        Assert.Contains("password", payload.FieldErrors.Keys);
        Assert.DoesNotContain("admin", responseJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecurityLogin_RejectsDisabledUsers()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        await factory.UpsertSecurityUserAsync(
            "disabled-user",
            "disabled",
            "Disabled User",
            "disabled-password",
            new[] { ShortenLinkRoles.Viewer },
            isEnabled: false);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/security/login", new
        {
            username = "disabled",
            password = "disabled-password"
        });
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("invalid_login", payload.ErrorCode);
    }

    [Fact]
    public async Task SecurityLogin_ResolvesPermissionsFromLoggedInUserRoles()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        await factory.UpsertSecurityUserAsync(
            "viewer-user",
            "viewer",
            "Viewer User",
            "viewer-password",
            new[] { ShortenLinkRoles.Viewer },
            isEnabled: true);
        using var client = factory.CreateClient();

        using var loginResponse = await client.PostAsJsonAsync("/api/security/login", new
        {
            username = "viewer",
            password = "viewer-password"
        });
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<SecurityLoginResponse>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(loginPayload);
        Assert.Contains(ShortenLinkPermissions.ShortLinksRead, loginPayload.User.Permissions);
        Assert.Contains(ShortenLinkPermissions.ShortLinksDelete, loginPayload.User.Permissions);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            loginPayload.Token);

        using var readResponse = await client.GetAsync("/api/short-links?limit=10");
        using var deleteResponse = await client.DeleteAsync("/api/short-links/missing");
        var deletePayload = await deleteResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        Assert.NotNull(deletePayload);
        Assert.Equal("not_found", deletePayload.ErrorCode);
    }

    [Fact]
    public async Task SecurityRoles_CanListSystemRolesAndManageCustomRoles()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var initialListResponse = await client.GetAsync("/api/security/roles");
        var initialList = await initialListResponse.Content.ReadFromJsonAsync<SecurityRolesListResponse>();

        Assert.Equal(HttpStatusCode.OK, initialListResponse.StatusCode);
        Assert.NotNull(initialList);
        var ownerRole = Assert.Single(initialList.SystemRoles, role => role.Id == ShortenLinkRoles.Owner);
        Assert.True(ownerRole.IsSystem);
        Assert.True(ownerRole.IsEnabled);
        Assert.False(ownerRole.CanDelete);
        Assert.Contains(ShortenLinkPermissions.ShortLinksImport, ownerRole.Permissions);

        using var upsertResponse = await client.PutAsJsonAsync("/api/security/roles/custom", new
        {
            id = "support",
            name = "Support",
            permissions = new[] { ShortenLinkPermissions.ShortLinksRead, ShortenLinkPermissions.AnalyticsRead },
            isEnabled = true
        });
        var upserted = await upsertResponse.Content.ReadFromJsonAsync<SecurityRoleResponse>();

        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);
        Assert.NotNull(upserted);
        Assert.Equal("support", upserted.Id);
        Assert.False(upserted.IsSystem);
        Assert.True(upserted.IsEnabled);
        Assert.Equal(
            new[] { ShortenLinkPermissions.AnalyticsRead, ShortenLinkPermissions.ShortLinksRead },
            upserted.Permissions);

        using var deleteResponse = await client.DeleteAsync("/api/security/roles/custom/support");
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<SecurityRoleDeletedResponse>();

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.NotNull(deleted);
        Assert.Equal("support", deleted.Id);
    }

    [Fact]
    public async Task SecurityRoles_RejectInvalidPermissionsAndSystemRoleMutation()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var invalidPermissionResponse = await client.PutAsJsonAsync("/api/security/roles/custom", new
        {
            id = "support",
            name = "Support",
            permissions = new[] { "security.magic" },
            isEnabled = true
        });
        var invalidPermission = await invalidPermissionResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();
        using var systemRoleResponse = await client.PutAsJsonAsync("/api/security/roles/custom", new
        {
            id = ShortenLinkRoles.Owner,
            name = ShortenLinkRoles.Owner,
            permissions = new[] { ShortenLinkPermissions.ShortLinksRead },
            isEnabled = true
        });
        var systemRolePayload = await systemRoleResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, invalidPermissionResponse.StatusCode);
        Assert.NotNull(invalidPermission);
        Assert.Equal("invalid_permission", invalidPermission.ErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, systemRoleResponse.StatusCode);
        Assert.NotNull(systemRolePayload);
        Assert.Equal("system_role_immutable", systemRolePayload.ErrorCode);
    }

    [Fact]
    public async Task SecurityRoles_PersistOverridesAndApplyThemToUserSessions()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var overrideResponse = await client.PutAsJsonAsync(
            $"/api/security/roles/{ShortenLinkRoles.Viewer}/permission-overrides",
            new
            {
                overrides = new[]
                {
                    new { permission = ShortenLinkPermissions.ShortLinksRead, isAllowed = false },
                    new { permission = ShortenLinkPermissions.ShortLinksDelete, isAllowed = false }
                }
            });
        var overriddenRole = await overrideResponse.Content.ReadFromJsonAsync<SecurityRoleResponse>();

        Assert.Equal(HttpStatusCode.OK, overrideResponse.StatusCode);
        Assert.NotNull(overriddenRole);
        Assert.Contains(ShortenLinkPermissions.ShortLinksRead, overriddenRole.DefaultPermissions);
        Assert.Contains(ShortenLinkPermissions.ShortLinksDelete, overriddenRole.DefaultPermissions);
        Assert.DoesNotContain(ShortenLinkPermissions.ShortLinksRead, overriddenRole.Permissions);
        Assert.DoesNotContain(ShortenLinkPermissions.ShortLinksDelete, overriddenRole.Permissions);
        Assert.Contains(overriddenRole.PermissionOverrides, item =>
            item.Permission == ShortenLinkPermissions.ShortLinksRead && !item.IsAllowed);
        Assert.Contains(overriddenRole.PermissionOverrides, item =>
            item.Permission == ShortenLinkPermissions.ShortLinksDelete && !item.IsAllowed);

        using var userResponse = await client.PutAsJsonAsync("/api/security/users", new
        {
            id = "override-viewer",
            username = "override-viewer",
            displayName = "Override Viewer",
            password = "override-password",
            roleIds = new[] { ShortenLinkRoles.Viewer },
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        using var loginResponse = await client.PostAsJsonAsync("/api/security/login", new
        {
            username = "override-viewer",
            password = "override-password"
        });
        var login = await loginResponse.Content.ReadFromJsonAsync<SecurityLoginResponse>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(login);
        Assert.DoesNotContain(ShortenLinkPermissions.ShortLinksRead, login.User.Permissions);
        Assert.DoesNotContain(ShortenLinkPermissions.ShortLinksDelete, login.User.Permissions);
    }

    [Fact]
    public async Task SecurityRoles_RequireUsersToBeUnassignedBeforeCustomRoleDeletion()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var roleResponse = await client.PutAsJsonAsync("/api/security/roles/custom", new
        {
            id = "support",
            name = "Support",
            permissions = new[] { ShortenLinkPermissions.ShortLinksRead },
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, roleResponse.StatusCode);

        using var userResponse = await client.PutAsJsonAsync("/api/security/users", new
        {
            id = "support-user",
            username = "support-user",
            displayName = "Support User",
            password = "support-password",
            roleIds = new[] { "support" },
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);

        using var inUseResponse = await client.DeleteAsync("/api/security/roles/custom/support");
        var inUse = await inUseResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Conflict, inUseResponse.StatusCode);
        Assert.NotNull(inUse);
        Assert.Equal("role_in_use", inUse.ErrorCode);

        using var unassignResponse = await client.PutAsJsonAsync("/api/security/users", new
        {
            id = "support-user",
            username = "support-user",
            displayName = "Support User",
            password = (string?)null,
            roleIds = Array.Empty<string>(),
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, unassignResponse.StatusCode);

        using var deleteResponse = await client.DeleteAsync("/api/security/roles/custom/support");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task SecurityUsers_CanCreateListUpdateAndDisableNormalUsers()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var roleResponse = await client.PutAsJsonAsync("/api/security/roles/custom", new
        {
            id = "support",
            name = "Support",
            permissions = new[] { ShortenLinkPermissions.ShortLinksRead },
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, roleResponse.StatusCode);

        using var createResponse = await client.PutAsJsonAsync("/api/security/users", new
        {
            id = "user-1",
            username = "editor",
            displayName = "Editor User",
            password = "editor-password",
            roleIds = new[] { ShortenLinkRoles.Editor, "support" },
            isEnabled = true
        });
        var createJson = await createResponse.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<SecurityUserResponse>(
            createJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("editor", created.Username);
        Assert.Equal(new[] { "support", ShortenLinkRoles.User }, created.RoleIds);
        Assert.DoesNotContain("password", createJson, StringComparison.OrdinalIgnoreCase);

        using var listResponse = await client.GetAsync("/api/security/users");
        var list = await listResponse.Content.ReadFromJsonAsync<SecurityUsersListResponse>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(list);
        var listed = Assert.Single(list.Items);
        Assert.Equal("user-1", listed.Id);
        Assert.DoesNotContain(list.Items, user => user.Username == "admin");

        using var updateResponse = await client.PutAsJsonAsync("/api/security/users", new
        {
            id = "user-1",
            username = "editor",
            displayName = "Updated Editor",
            password = (string?)null,
            roleIds = new[] { ShortenLinkRoles.Viewer },
            isEnabled = true
        });
        var updated = await updateResponse.Content.ReadFromJsonAsync<SecurityUserResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("Updated Editor", updated.DisplayName);
        Assert.Equal(new[] { ShortenLinkRoles.Viewer }, updated.RoleIds);

        using var disableResponse = await client.PostAsync("/api/security/users/user-1/disable", null);
        var disabled = await disableResponse.Content.ReadFromJsonAsync<SecurityUserDisabledResponse>();

        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        Assert.NotNull(disabled);
        Assert.False(disabled.IsEnabled);
    }

    [Fact]
    public async Task SecurityUsers_RejectUnknownRolesAndRequireManagementPermission()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        await factory.UpsertSecurityUserAsync(
            "viewer-user",
            "viewer",
            "Viewer User",
            "viewer-password",
            new[] { ShortenLinkRoles.Viewer },
            isEnabled: true);
        using var client = factory.CreateClient();

        using var viewerLoginResponse = await client.PostAsJsonAsync("/api/security/login", new
        {
            username = "viewer",
            password = "viewer-password"
        });
        var viewerLogin = await viewerLoginResponse.Content.ReadFromJsonAsync<SecurityLoginResponse>();
        Assert.NotNull(viewerLogin);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", viewerLogin.Token);

        using var forbiddenResponse = await client.GetAsync("/api/security/users");
        var forbidden = await forbiddenResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.NotNull(forbidden);
        Assert.Equal("forbidden", forbidden.ErrorCode);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            await LoginAsAdminAsync(client));
        using var unknownRoleResponse = await client.PutAsJsonAsync("/api/security/users", new
        {
            id = "user-2",
            username = "badrole",
            displayName = "Bad Role",
            password = "bad-role-password",
            roleIds = new[] { "missing-role" },
            isEnabled = true
        });
        var unknownRole = await unknownRoleResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, unknownRoleResponse.StatusCode);
        Assert.NotNull(unknownRole);
        Assert.Equal("invalid_role", unknownRole.ErrorCode);
    }

    [Fact]
    public async Task SecurityApiKeys_CanBeCreatedListedRenamedAndDisabledByOwnerOnly()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var ownerClient = factory.CreateClient();
        var ownerToken = await LoginAsAdminAsync(ownerClient);
        ownerClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);

        using var createResponse = await ownerClient.PostAsJsonAsync("/api/security/api-keys", new
        {
            displayName = "Local automation"
        });
        var createJson = await createResponse.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<SecurityUserApiKeyCreatedResponse>(
            createJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created.RawApiKey));
        Assert.StartsWith("slk_", created.RawApiKey, StringComparison.Ordinal);
        Assert.Equal("Local automation", created.ApiKey.DisplayName);
        Assert.DoesNotContain("keyHash", createJson, StringComparison.OrdinalIgnoreCase);

        var stored = await factory.GetUserApiKeyRecordsAsync();
        var storedRecord = Assert.Single(stored);
        Assert.Equal(ShortenLinkSecurityCredentialHasher.HashApiKey(created.RawApiKey), storedRecord.KeyHash);
        Assert.DoesNotContain(created.RawApiKey, storedRecord.KeyHash, StringComparison.Ordinal);

        using var listResponse = await ownerClient.GetAsync("/api/security/api-keys");
        var listJson = await listResponse.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<SecurityUserApiKeysListResponse>(
            listJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(list);
        var listed = Assert.Single(list.Items);
        Assert.Equal(created.ApiKey.Id, listed.Id);
        Assert.DoesNotContain(created.RawApiKey, listJson, StringComparison.Ordinal);
        Assert.DoesNotContain("keyHash", listJson, StringComparison.OrdinalIgnoreCase);

        using var renameResponse = await ownerClient.PutAsJsonAsync($"/api/security/api-keys/{created.ApiKey.Id}", new
        {
            displayName = "Renamed automation"
        });
        var renamed = await renameResponse.Content.ReadFromJsonAsync<SecurityUserApiKeyResponse>();

        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);
        Assert.NotNull(renamed);
        Assert.Equal("Renamed automation", renamed.DisplayName);

        await factory.UpsertSecurityUserAsync(
            "other-user",
            "other",
            "Other User",
            "other-password",
            new[] { ShortenLinkRoles.Owner },
            isEnabled: true);
        using var otherClient = factory.CreateClient();
        using var otherLoginResponse = await otherClient.PostAsJsonAsync("/api/security/login", new
        {
            username = "other",
            password = "other-password"
        });
        var otherLogin = await otherLoginResponse.Content.ReadFromJsonAsync<SecurityLoginResponse>();
        Assert.NotNull(otherLogin);
        otherClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", otherLogin.Token);

        using var otherListResponse = await otherClient.GetAsync("/api/security/api-keys");
        var otherList = await otherListResponse.Content.ReadFromJsonAsync<SecurityUserApiKeysListResponse>();
        using var otherRenameResponse = await otherClient.PutAsJsonAsync($"/api/security/api-keys/{created.ApiKey.Id}", new
        {
            displayName = "Should not work"
        });

        Assert.Equal(HttpStatusCode.OK, otherListResponse.StatusCode);
        Assert.NotNull(otherList);
        Assert.Empty(otherList.Items);
        Assert.Equal(HttpStatusCode.NotFound, otherRenameResponse.StatusCode);

        using var disableResponse = await ownerClient.PostAsync($"/api/security/api-keys/{created.ApiKey.Id}/disable", null);
        var disabled = await disableResponse.Content.ReadFromJsonAsync<SecurityUserApiKeyDisabledResponse>();

        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        Assert.NotNull(disabled);
        Assert.False(disabled.IsEnabled);
    }

    [Fact]
    public async Task ShortLinks_ArePrivateUntilExplicitlySharedAndRevocationRestoresIsolation()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: false);
        await factory.UpsertSecurityUserAsync(
            "owner-user", "owner", "Owner User", "owner-password",
            new[] { ShortenLinkRoles.User }, isEnabled: true);
        await factory.UpsertSecurityUserAsync(
            "shared-user", "shared", "Shared User", "shared-password",
            new[] { ShortenLinkRoles.User }, isEnabled: true);

        using var ownerClient = factory.CreateClient();
        using var ownerLoginResponse = await ownerClient.PostAsJsonAsync("/api/security/login", new
        {
            username = "owner",
            password = "owner-password"
        });
        var ownerLogin = await ownerLoginResponse.Content.ReadFromJsonAsync<SecurityLoginResponse>();
        Assert.NotNull(ownerLogin);
        ownerClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerLogin.Token);
        var created = await CreateShortLinkAsync(ownerClient, "https://example.com/private");

        using var sharedClient = factory.CreateClient();
        using var sharedLoginResponse = await sharedClient.PostAsJsonAsync("/api/security/login", new
        {
            username = "shared",
            password = "shared-password"
        });
        var sharedLogin = await sharedLoginResponse.Content.ReadFromJsonAsync<SecurityLoginResponse>();
        Assert.NotNull(sharedLogin);
        sharedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sharedLogin.Token);

        using var privateListResponse = await sharedClient.GetAsync("/api/short-links?page=1&limit=10");
        var privateList = await privateListResponse.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();
        Assert.NotNull(privateList);
        Assert.Empty(privateList.Items);

        using var privateExportResponse = await sharedClient.GetAsync("/api/short-links/export");
        var privateExport = await privateExportResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<ShortLinkExportRecord>>();
        Assert.NotNull(privateExport);
        Assert.Empty(privateExport);

        using var privateUpdateResponse = await sharedClient.PutAsJsonAsync(
            $"/api/short-links/{created.Code}",
            new
            {
                originalUrl = "https://example.com/not-shared",
                expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
            });
        Assert.Equal(HttpStatusCode.Forbidden, privateUpdateResponse.StatusCode);

        using var shareResponse = await ownerClient.PutAsJsonAsync(
            $"/api/short-links/{created.Code}/shares",
            new { username = "shared", access = "View" });
        Assert.Equal(HttpStatusCode.OK, shareResponse.StatusCode);

        using var listResponse = await sharedClient.GetAsync("/api/short-links?page=1&limit=10");
        var list = await listResponse.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();
        var sharedLink = Assert.Single(list!.Items);
        Assert.Equal(created.Code, sharedLink.Code);
        Assert.Equal("View", sharedLink.AccessLevel);

        using var exportResponse = await sharedClient.GetAsync("/api/short-links/export");
        var export = await exportResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<ShortLinkExportRecord>>();
        var sharedExport = Assert.Single(export!);
        Assert.Equal(created.Code, sharedExport.Code);
        Assert.Equal("View", sharedExport.AccessLevel);

        using var updateResponse = await sharedClient.PutAsJsonAsync(
            $"/api/short-links/{created.Code}",
            new
            {
                originalUrl = "https://example.com/forbidden-update",
                expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
            });
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);

        using var revokeResponse = await ownerClient.DeleteAsync(
            $"/api/short-links/{created.Code}/shares/shared-user");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        using var revokedListResponse = await sharedClient.GetAsync("/api/short-links?page=1&limit=10");
        var revokedList = await revokedListResponse.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();
        Assert.NotNull(revokedList);
        Assert.Empty(revokedList.Items);

        using var revokedExportResponse = await sharedClient.GetAsync("/api/short-links/export");
        var revokedExport = await revokedExportResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<ShortLinkExportRecord>>();
        Assert.NotNull(revokedExport);
        Assert.Empty(revokedExport);

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await LoginAsAdminAsync(adminClient));
        using var adminListResponse = await adminClient.GetAsync("/api/short-links?page=1&limit=10");
        var adminList = await adminListResponse.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();
        Assert.Contains(adminList!.Items, item => item.Code == created.Code && item.AccessLevel == "Admin");
    }

    [Fact]
    public async Task SecurityApiKeys_AuthorizeProtectedEndpointsThroughOwningUserRoles()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        await factory.UpsertSecurityUserAsync(
            "viewer-user",
            "viewer",
            "Viewer User",
            "viewer-password",
            new[] { ShortenLinkRoles.Viewer },
            isEnabled: true);
        using var loginClient = factory.CreateClient();
        using var loginResponse = await loginClient.PostAsJsonAsync("/api/security/login", new
        {
            username = "viewer",
            password = "viewer-password"
        });
        var login = await loginResponse.Content.ReadFromJsonAsync<SecurityLoginResponse>();
        Assert.NotNull(login);
        loginClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.Token);

        using var createResponse = await loginClient.PostAsJsonAsync("/api/security/api-keys", new
        {
            displayName = "Viewer API key"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<SecurityUserApiKeyCreatedResponse>();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);

        using var apiClient = factory.CreateClient();
        apiClient.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", created.RawApiKey);

        using var readResponse = await apiClient.GetAsync("/api/short-links?limit=10");
        using var deleteResponse = await apiClient.DeleteAsync("/api/short-links/missing");
        var deletePayload = await deleteResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        Assert.NotNull(deletePayload);
        Assert.Equal("not_found", deletePayload.ErrorCode);
    }

    [Fact]
    public async Task SecurityApiKeys_RejectDisabledKeysForAuthorization()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var ownerClient = factory.CreateClient();
        var ownerToken = await LoginAsAdminAsync(ownerClient);
        ownerClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);

        using var createResponse = await ownerClient.PostAsJsonAsync("/api/security/api-keys", new
        {
            displayName = "Temporary key"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<SecurityUserApiKeyCreatedResponse>();
        Assert.NotNull(created);

        using var disableResponse = await ownerClient.PostAsync($"/api/security/api-keys/{created.ApiKey.Id}/disable", null);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        using var apiClient = factory.CreateClient();
        apiClient.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", created.RawApiKey);

        using var response = await apiClient.GetAsync("/api/short-links?limit=10");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("unauthorized", payload.ErrorCode);
    }

    [Fact]
    public async Task AdminMutations_ReturnUnauthorizedWhenSecurityEnabledAndApiKeyMissing()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();

        foreach (var request in CreateAdminMutationRequests())
        {
            using var response = await client.SendAsync(request);
            var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotNull(payload);
            Assert.Equal("unauthorized", payload.ErrorCode);
        }
    }

    [Fact]
    public async Task AdminMutations_ReturnForbiddenWhenApiKeyLacksMutationPermissions()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true,
            securityRoles: Array.Empty<string>(),
            securityPermissions: new[] { ShortenLinkPermissions.ShortLinksRead });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", "test-admin-key");

        foreach (var request in CreateAdminMutationRequests())
        {
            using var response = await client.SendAsync(request);
            var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.NotNull(payload);
            Assert.Equal("forbidden", payload.ErrorCode);
        }
    }

    [Fact]
    public async Task GetList_ReturnsCursorForNextPage()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await CreateShortLinkAsync(client, "https://example.com/one");
        await CreateShortLinkAsync(client, "https://example.com/two");
        await CreateShortLinkAsync(client, "https://example.com/three");

        using var firstResponse = await client.GetAsync("/api/short-links?limit=2");
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.NextCursor));

        using var secondResponse = await client.GetAsync($"/api/short-links?limit=2&cursor={Uri.EscapeDataString(firstPage.NextCursor)}");
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondPage);
        Assert.Single(secondPage.Items);
        Assert.Empty(firstPage.Items.Select(item => item.Code).Intersect(secondPage.Items.Select(item => item.Code)));
        Assert.Equal(
            new[]
            {
                "https://example.com/one",
                "https://example.com/three",
                "https://example.com/two"
            },
            firstPage.Items.Concat(secondPage.Items)
                .Select(item => item.OriginalUrl)
                .OrderBy(url => url, StringComparer.Ordinal)
                .ToArray());
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task GetList_AppliesSearchSortAndFilteredPageMetadata()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        await factory.SeedShortLinkAsync(
            "beta01",
            "https://beta.example.com/docs",
            new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero));
        await factory.SeedShortLinkAsync(
            "alpha01",
            "https://alpha.example.com/docs",
            new DateTimeOffset(2026, 7, 15, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero));
        await factory.SeedShortLinkAsync(
            "archive",
            "https://archive.example.com/old",
            new DateTimeOffset(2026, 7, 15, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var filter = Uri.EscapeDataString("(OriginalUrl contains `example.com/docs`)");
        using var response = await client.GetAsync(
            $"/api/short-links?page=1&limit=10&fe={filter}&sort={Uri.EscapeDataString("+OriginalUrl")}");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(1, payload.Page);
        Assert.Equal(10, payload.PageSize);
        Assert.Equal(1, payload.TotalPages);
        Assert.Equal(
            new[] { "https://alpha.example.com/docs", "https://beta.example.com/docs" },
            payload.Items.Select(item => item.OriginalUrl).ToArray());
    }

    [Fact]
    public async Task GetList_AppliesStatusFilters()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        await factory.SeedShortLinkAsync(
            "active1",
            "https://example.com/active",
            new DateTimeOffset(2026, 7, 15, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        await factory.SeedShortLinkAsync(
            "soon001",
            "https://example.com/soon",
            new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero));
        await factory.SeedShortLinkAsync(
            "expired",
            "https://example.com/expired",
            new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 15, 11, 0, 0, TimeSpan.Zero));
        await factory.SeedShortLinkAsync(
            "off0001",
            "https://example.com/inactive",
            new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            isActive: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var expired = await GetListCodesAsync(client, "expired");
        var inactive = await GetListCodesAsync(client, "inactive");
        var expiringSoon = await GetListCodesAsync(client, "expiring-soon");
        var active = await GetListCodesAsync(client, "active");

        Assert.Equal(new[] { "expired" }, expired);
        Assert.Equal(new[] { "off0001" }, inactive);
        Assert.Equal(new[] { "soon001" }, expiringSoon);
        Assert.Equal(new[] { "active1", "soon001" }, active.OrderBy(code => code, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [InlineData("fe", "(Missing eq `value`)", "invalid_filter")]
    [InlineData("sort", "missing", "invalid_sort")]
    public async Task GetList_ReturnsBadRequestForInvalidDiscoveryQuery(
        string parameter,
        string value,
        string expectedErrorCode)
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync(
            $"/api/short-links?page=1&{parameter}={Uri.EscapeDataString(value)}");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(expectedErrorCode, payload.ErrorCode);
    }

    [Fact]
    public async Task PostMockSeedShortLinks_CreatesRequestedMockLinks()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var seedResponse = await client.PostAsync("/api/mock/seed-short-links?count=12", null);
        var seedPayload = await seedResponse.Content.ReadFromJsonAsync<MockSeedShortLinksResponse>();

        Assert.Equal(HttpStatusCode.OK, seedResponse.StatusCode);
        Assert.NotNull(seedPayload);
        Assert.Equal(12, seedPayload.RequestedCount);
        Assert.Equal(12, seedPayload.CreatedCount);
        Assert.Equal(0, seedPayload.FailedCount);
        Assert.Equal(12, seedPayload.Codes.Count);

        using var listResponse = await client.GetAsync("/api/short-links?limit=20");
        var listPayload = await listResponse.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listPayload);
        Assert.Equal(12, listPayload.Items.Count);
    }

    [Fact]
    public async Task PostCreate_ReturnsBadRequestForInvalidUrl()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/short-links", new
        {
            originalUrl = "ftp://example.com/file",
            expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
        });

        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("invalid_url", payload.ErrorCode);
        Assert.NotNull(payload.FieldErrors);
        Assert.Equal(payload.Message, Assert.Single(payload.FieldErrors["originalUrl"]));
    }

    [Fact]
    public async Task PostCreate_ReturnsBadRequestWhenDestinationUrlIsMissing()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/short-links", new
        {
            expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
        });

        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("invalid_url", payload.ErrorCode);
    }

    [Fact]
    public async Task PostCreate_ReturnsBadRequestWhenExpiryIsMissing()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/short-links", new
        {
            originalUrl = "https://example.com/docs"
        });

        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("invalid_expiration", payload.ErrorCode);
        Assert.NotNull(payload.FieldErrors);
        Assert.Equal(payload.Message, Assert.Single(payload.FieldErrors["expiredAtUtc"]));
    }

    [Fact]
    public async Task GetDetails_ReturnsStoredShortLink()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();

        var created = await CreateShortLinkAsync(client, "https://example.com/details");

        using var response = await client.GetAsync($"/api/short-links/{created.Code}");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkDetailsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(created.Code, payload.Code);
        Assert.Equal("https://example.com/details", payload.OriginalUrl);
        Assert.True(payload.IsActive);
    }

    [Fact]
    public async Task GetAnalytics_ReturnsSummaryAndRecentClicks()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();
        var created = await CreateShortLinkAsync(client, "https://example.com/analytics");
        var baseTime = new DateTimeOffset(2026, 7, 15, 13, 0, 0, TimeSpan.Zero);
        await factory.SeedClickAsync(created.Code, baseTime, "127.0.0.1", "old-agent", "https://example.com/start");
        await factory.SeedClickAsync(created.Code, baseTime.AddMinutes(10), "127.0.0.2", "new-agent", null);
        await factory.SeedClickAsync(created.Code, baseTime.AddMinutes(5), "127.0.0.3", "middle-agent", null);
        await factory.SeedClickAsync("other01", baseTime.AddHours(1), "127.0.0.4", "other-agent", null);

        using var response = await client.GetAsync($"/api/short-links/{created.Code}/analytics?limit=2");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkAnalyticsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(created.Code, payload.Code);
        Assert.Equal(3, payload.ClickCount);
        Assert.Equal(baseTime.AddMinutes(10), payload.LastClickedAtUtc);
        Assert.Collection(
            payload.RecentClicks,
            click =>
            {
                Assert.Equal(baseTime.AddMinutes(10), click.ClickedAtUtc);
                Assert.Equal("new-agent", click.UserAgent);
            },
            click =>
            {
                Assert.Equal(baseTime.AddMinutes(5), click.ClickedAtUtc);
                Assert.Equal("middle-agent", click.UserAgent);
            });
    }

    [Fact]
    public async Task GetAnalytics_ReturnsEmptyAnalyticsForLinkWithoutClicks()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();
        var created = await CreateShortLinkAsync(client, "https://example.com/no-clicks");

        using var response = await client.GetAsync($"/api/short-links/{created.Code}/analytics");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkAnalyticsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(created.Code, payload.Code);
        Assert.Equal(0, payload.ClickCount);
        Assert.Null(payload.LastClickedAtUtc);
        Assert.Empty(payload.RecentClicks);
    }

    [Fact]
    public async Task GetAnalytics_ReturnsUnauthorizedWhenSecurityEnabledAndApiKeyMissing()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/short-links/missing/analytics");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("unauthorized", payload.ErrorCode);
    }

    [Fact]
    public async Task GetAnalytics_ReturnsForbiddenWhenApiKeyLacksAnalyticsPermission()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true,
            securityRoles: Array.Empty<string>(),
            securityPermissions: new[] { ShortenLinkPermissions.ShortLinksRead });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", "test-admin-key");

        using var response = await client.GetAsync("/api/short-links/missing/analytics");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("forbidden", payload.ErrorCode);
    }

    [Fact]
    public async Task PostDeactivate_DeactivatesShortLinkAndRedirectReturnsGone()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var created = await CreateShortLinkAsync(client, "https://example.com/remove");

        using var deleteResponse = await client.PostAsync($"/api/short-links/{created.Code}/deactivate", null);
        using var redirectResponse = await client.GetAsync($"/{created.Code}");
        var payload = await redirectResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, redirectResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("inactive", payload.ErrorCode);
    }

    [Fact]
    public async Task PutUpdate_ChangesDestinationForRedirect()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var created = await CreateShortLinkAsync(client, "https://example.com/old");

        using var updateResponse = await client.PutAsJsonAsync($"/api/short-links/{created.Code}", new
        {
            originalUrl = "https://example.com/new",
            expiredAtUtc = new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero)
        });
        using var redirectResponse = await client.GetAsync($"/{created.Code}");

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, redirectResponse.StatusCode);
        Assert.Equal("https://example.com/new", redirectResponse.Headers.Location?.AbsoluteUri);
    }

    [Fact]
    public async Task PutUpdate_ReturnsBadRequestWhenDestinationUrlIsMissing()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();
        var created = await CreateShortLinkAsync(client, "https://example.com/old");

        using var response = await client.PutAsJsonAsync($"/api/short-links/{created.Code}", new
        {
            expiredAtUtc = new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero)
        });

        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("invalid_url", payload.ErrorCode);
    }

    [Fact]
    public async Task PutUpdate_ReturnsBadRequestWhenExpiryIsMissing()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient();
        var created = await CreateShortLinkAsync(client, "https://example.com/old");

        using var response = await client.PutAsJsonAsync($"/api/short-links/{created.Code}", new
        {
            originalUrl = "https://example.com/new"
        });

        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("invalid_expiration", payload.ErrorCode);
    }

    [Fact]
    public async Task Delete_RemovesShortLink()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var created = await CreateShortLinkAsync(client, "https://example.com/delete");

        using var deleteResponse = await client.DeleteAsync($"/api/short-links/{created.Code}");
        using var detailsResponse = await client.GetAsync($"/api/short-links/{created.Code}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_InvalidatesCachedRedirect_WhenMemoryCacheEnabled()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            cacheEnabled: true,
            cacheProvider: "Memory");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var created = await CreateShortLinkAsync(client, "https://example.com/cached-remove");

        using var firstRedirectResponse = await client.GetAsync($"/{created.Code}");
        using var deleteResponse = await client.PostAsync($"/api/short-links/{created.Code}/deactivate", null);
        using var secondRedirectResponse = await client.GetAsync($"/{created.Code}");
        var payload = await secondRedirectResponse.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.Redirect, firstRedirectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, secondRedirectResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("inactive", payload.ErrorCode);
    }

    [Fact]
    public async Task Redirect_ReturnsOriginalUrlForActiveShortLink()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var created = await CreateShortLinkAsync(client, "https://example.com/redirect");

        using var response = await client.GetAsync($"/{created.Code}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://example.com/redirect", response.Headers.Location?.AbsoluteUri);
    }

    [Fact]
    public async Task Redirect_RecordsClickAnalytics_WhenEnabled()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false, analyticsEnabled: true);
        Assert.Contains(
            factory.Services.GetServices<IHostedService>(),
            service => service.GetType().Name == "ClickWorker");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var created = await CreateShortLinkAsync(client, "https://example.com/redirect");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/{created.Code}");
        request.Headers.Referrer = new Uri("https://referrer.example/source");
        request.Headers.UserAgent.ParseAdd("shorten-link-tests/1.0");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await WaitForConditionAsync(async () =>
        {
            var clicks = await factory.GetRecordedClicksAsync();
            return clicks.Count == 1;
        }));

        var click = Assert.Single(await factory.GetRecordedClicksAsync());
        Assert.Equal(created.Code, click.ShortCode);
        Assert.Equal("shorten-link-tests/1.0", click.UserAgent);
        Assert.Equal("https://referrer.example/source", click.Referrer);
    }

    [Fact]
    public async Task Redirect_DoesNotRecordClickAnalytics_WhenDisabled()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false, analyticsEnabled: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var created = await CreateShortLinkAsync(client, "https://example.com/redirect");

        using var response = await client.GetAsync($"/{created.Code}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await Task.Delay(150);
        Assert.Empty(await factory.GetRecordedClicksAsync());
    }

    [Fact]
    public async Task RateLimiting_DisabledByDefault_DoesNotThrottleCreateRequests()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            rateLimitingEnabled: false,
            createPermitLimit: 1);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var firstResponse = await client.PostAsJsonAsync("/api/short-links", new
        {
            originalUrl = "https://example.com/one",
            expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
        });
        using var secondResponse = await client.PostAsJsonAsync("/api/short-links", new
        {
            originalUrl = "https://example.com/two",
            expiredAtUtc = new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero)
        });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
    }

    [Fact]
    public async Task RateLimiting_ReturnsTooManyRequestsForCreate_WhenLimitExceeded()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            rateLimitingEnabled: true,
            createPermitLimit: 1,
            redirectPermitLimit: 10);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var firstResponse = await client.PostAsJsonAsync("/api/short-links", new
        {
            originalUrl = "https://example.com/one",
            expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
        });
        using var secondResponse = await client.PostAsJsonAsync("/api/short-links", new
        {
            originalUrl = "https://example.com/two",
            expiredAtUtc = new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero)
        });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
    }

    [Fact]
    public async Task RateLimiting_ExposesSafeAdminActivityAndRecentPolicyRejections()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            rateLimitingEnabled: true,
            createPermitLimit: 1,
            redirectPermitLimit: 10);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await CreateShortLinkAsync(client, "https://example.com/one");
        using var rejected = await client.PostAsJsonAsync("/api/short-links", new
        {
            originalUrl = "https://example.com/two",
            expiredAtUtc = new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero)
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        using var response = await client.GetAsync("/api/admin/rate-limits");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var activity = await response.Content.ReadFromJsonAsync<RateLimitActivityResponse>();

        Assert.NotNull(activity);
        Assert.True(activity.Enabled);
        Assert.Equal(1, activity.Create.PermitLimit);
        Assert.Equal(1, activity.Create.RejectedCount);
        Assert.Single(activity.RecentRejections);
        Assert.Equal("create", activity.RecentRejections[0].Policy);

        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("remoteIp", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shortCode", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("originalUrl", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RateLimiting_RejectsNonAdminActivityQueries()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            securityEnabled: true,
            securityRoles: [ShortenLinkRoles.User]);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ShortenLink-Api-Key", "test-admin-key");

        using var response = await client.GetAsync("/api/admin/rate-limits");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RateLimiting_ReturnsTooManyRequestsForRedirect_BeforeSecondAnalyticsRecord()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: false,
            analyticsEnabled: true,
            rateLimitingEnabled: true,
            createPermitLimit: 10,
            redirectPermitLimit: 1);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var created = await CreateShortLinkAsync(client, "https://example.com/redirect");

        using var firstResponse = await client.GetAsync($"/{created.Code}");
        using var secondResponse = await client.GetAsync($"/{created.Code}");

        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
        Assert.True(await WaitForConditionAsync(async () =>
        {
            var clicks = await factory.GetRecordedClicksAsync();
            return clicks.Count == 1;
        }));
        Assert.Single(await factory.GetRecordedClicksAsync());
    }

    [Fact]
    public async Task UnknownCode_RedirectsToFrontendFallbackWhenEnabled()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/missing");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/not-found", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UnknownCode_RedirectsToAbsoluteFrontendFallbackWhenConfigured()
    {
        await using var factory = new ShortLinkApiFactory(
            enableFrontendFallback: true,
            frontendFallbackPath: "http://localhost:5173/not-found");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/missing");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("http://localhost:5173/not-found", response.Headers.Location?.AbsoluteUri);
    }

    [Fact]
    public async Task UnknownCode_ReturnsJson404WhenFrontendFallbackDisabled()
    {
        await using var factory = new ShortLinkApiFactory(enableFrontendFallback: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/missing");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkErrorResponse>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("not_found", payload.ErrorCode);
    }

    [Fact]
    public void AddShortenLink_UsesSqliteProviderByDefault()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:Database:UsePostgres"] = "false",
            ["ShortenLink:Database:SqliteConnectionString"] = "Data Source=provider-sqlite.db"
        });

        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value;
        var dbContext = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();

        Assert.False(options.Database.UsePostgres);
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", dbContext.Database.ProviderName);
    }

    [Fact]
    public void AddShortenLink_UsesPostgresProviderWhenEnabled()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:Database:UsePostgres"] = "true",
            ["ShortenLink:Database:PostgresConnectionString"] = "Host=localhost;Port=5432;Database=shorten_link_tests;Username=postgres;Password=postgres"
        });

        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value;
        var dbContext = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();

        Assert.True(options.Database.UsePostgres);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Fact]
    public void AddShortenLink_RejectsMissingPostgresConnectionStringWhenEnabled()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:Database:UsePostgres"] = "true",
            ["ShortenLink:Database:PostgresConnectionString"] = ""
        });

        using var scope = services.CreateScope();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            _ = scope.ServiceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value);

        Assert.Contains("PostgresConnectionString", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddShortenLink_UsesDisabledCacheByDefault()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>());

        var cache = services.GetRequiredService<IShortLinkCache>();

        Assert.IsType<DisabledShortLinkCache>(cache);
    }

    [Fact]
    public async Task AddShortenLink_DoesNotRegisterAnalyticsWorkerWhenDisabledOrSynchronous()
    {
        await using var disabledServices = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:Analytics:Enabled"] = "false"
        });
        Assert.DoesNotContain(
            disabledServices.GetServices<IHostedService>(),
            service => service.GetType().Name == "ClickWorker");

        await using var synchronousServices = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:Analytics:Enabled"] = "true",
            ["ShortenLink:Analytics:UseAsyncWorker"] = "false"
        });
        Assert.DoesNotContain(
            synchronousServices.GetServices<IHostedService>(),
            service => service.GetType().Name == "ClickWorker");
        Assert.Equal(
            "SyncClickRecorder",
            synchronousServices.GetRequiredService<IShortLinkClickRecorder>().GetType().Name);
    }

    [Fact]
    public async Task AddShortenLink_ActivatesAnalyticsQueueWorkerForEnabledAsyncMode()
    {
        await using var services = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:Analytics:Enabled"] = "true",
            ["ShortenLink:Analytics:UseAsyncWorker"] = "true"
        });

        Assert.NotNull(services.GetService<IMessageQueue<RecordShortLinkClickRequest>>());
        Assert.Equal(
            "ClickRecorder",
            services.GetRequiredService<IShortLinkClickRecorder>().GetType().Name);
    }

    [Fact]
    public void AddShortenLink_ObservabilityIsDisabledByDefault()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>());

        var options = services.GetRequiredService<IOptions<ShortenLinkOptions>>().Value;

        Assert.False(options.Observability.Enabled);
        Assert.False(options.Observability.HealthChecksEnabled);
        Assert.Null(services.GetService<HealthCheckService>());
    }

    [Fact]
    public async Task AddShortenLink_RegistersSafeHealthChecksWhenOptedIn()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:Observability:HealthChecksEnabled"] = "true",
            ["ShortenLink:Cache:Enabled"] = "false",
            ["ShortenLink:Analytics:Enabled"] = "false"
        });

        using (var scope = services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>().Database.EnsureCreatedAsync();
        }

        var healthChecks = services.GetRequiredService<HealthCheckService>();
        var report = await healthChecks.CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Equal(
            [
                ShortenLinkHealthCheckNames.Analytics,
                ShortenLinkHealthCheckNames.Cache,
                ShortenLinkHealthCheckNames.Configuration,
                ShortenLinkHealthCheckNames.Database
            ],
            report.Entries.Keys.OrderBy(static name => name).ToArray());
        Assert.All(report.Entries.Values, entry =>
            Assert.DoesNotContain("ConnectionString", entry.Description, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AddShortenLink_UsesMemoryCacheProviderWhenEnabled()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:Cache:Enabled"] = "true",
            ["ShortenLink:Cache:Provider"] = "Memory"
        });

        var distributedCache = services.GetRequiredService<IDistributedCache>();
        var shortLinkCache = services.GetRequiredService<IShortLinkCache>();

        Assert.Contains("Memory", distributedCache.GetType().Name, StringComparison.Ordinal);
        Assert.Equal("DistributedCache", shortLinkCache.GetType().Name);
    }

    [Fact]
    public async Task CacheLoader_CoalescesConcurrentNegativeMisses()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:Cache:Enabled"] = "true",
            ["ShortenLink:Cache:NegativeEntryTtlSeconds"] = "30"
        });
        var cache = Assert.IsAssignableFrom<IShortLinkCacheLoader>(
            services.GetRequiredService<IShortLinkCache>());
        var loaderCalls = 0;

        var results = await Task.WhenAll(
            cache.GetOrCreateAsync(
                "missing-cache-entry",
                async _ =>
                {
                    Interlocked.Increment(ref loaderCalls);
                    await Task.Delay(25);
                    return null;
                }),
            cache.GetOrCreateAsync(
                "missing-cache-entry",
                _ => Task.FromResult<ShortLink?>(null)));

        Assert.Equal(1, loaderCalls);
        Assert.All(results, result => Assert.Null(result));
    }

    [Fact]
    public void AddShortenLink_UsesRedisCacheProviderWhenEnabled()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:Cache:Enabled"] = "true",
            ["ShortenLink:Cache:Provider"] = "Redis",
            ["ShortenLink:Cache:RedisConnectionString"] = "localhost:6379"
        });

        var distributedCache = services.GetRequiredService<IDistributedCache>();
        var shortLinkCache = services.GetRequiredService<IShortLinkCache>();

        Assert.Contains("Redis", distributedCache.GetType().Name, StringComparison.Ordinal);
        Assert.Equal("DistributedCache", shortLinkCache.GetType().Name);
    }

    [Fact]
    public void AddShortenLink_RejectsInvalidCacheProvider()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:Cache:Enabled"] = "true",
            ["ShortenLink:Cache:Provider"] = "Disk"
        });

        using var scope = services.CreateScope();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            _ = scope.ServiceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value);

        Assert.Contains("Cache:Provider", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddShortenLink_RejectsMissingRedisConnectionStringWhenEnabled()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:Cache:Enabled"] = "true",
            ["ShortenLink:Cache:Provider"] = "Redis",
            ["ShortenLink:Cache:RedisConnectionString"] = ""
        });

        using var scope = services.CreateScope();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            _ = scope.ServiceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value);

        Assert.Contains("RedisConnectionString", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddShortenLink_BindsRateLimitingOptions()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:RateLimiting:Enabled"] = "true",
            ["ShortenLink:RateLimiting:Create:PermitLimit"] = "3",
            ["ShortenLink:RateLimiting:Create:WindowSeconds"] = "11",
            ["ShortenLink:RateLimiting:Redirect:PermitLimit"] = "7",
            ["ShortenLink:RateLimiting:Redirect:WindowSeconds"] = "13"
        });

        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value;

        Assert.True(options.RateLimiting.Enabled);
        Assert.Equal(3, options.RateLimiting.Create.PermitLimit);
        Assert.Equal(11, options.RateLimiting.Create.WindowSeconds);
        Assert.Equal(7, options.RateLimiting.Redirect.PermitLimit);
        Assert.Equal(13, options.RateLimiting.Redirect.WindowSeconds);
    }

    [Fact]
    public void AddShortenLink_RejectsInvalidRateLimitOptions()
    {
        using var services = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["ShortenLink:RateLimiting:Enabled"] = "true",
            ["ShortenLink:RateLimiting:Create:PermitLimit"] = "0"
        });

        using var scope = services.CreateScope();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            _ = scope.ServiceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value);

        Assert.Contains("RateLimiting", exception.Message, StringComparison.Ordinal);
    }

    private sealed class ShortLinkApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly string databaseDirectory = Path.Combine(Path.GetTempPath(), $"shorten-link-api-tests-{Guid.NewGuid():N}");
        private readonly bool enableFrontendFallback;
        private readonly string frontendFallbackPath;
        private readonly bool analyticsEnabled;
        private readonly bool cacheEnabled;
        private readonly string cacheProvider;
        private readonly bool rateLimitingEnabled;
        private readonly int createPermitLimit;
        private readonly int redirectPermitLimit;
        private readonly bool securityEnabled;
        private readonly string securityApiKey;
        private readonly IReadOnlyList<string> securityRoles;
        private readonly IReadOnlyList<string> securityPermissions;
        private readonly bool tenantHeaderContext;

        public ShortLinkApiFactory(
            bool enableFrontendFallback,
            string frontendFallbackPath = "/not-found",
            bool analyticsEnabled = false,
            bool cacheEnabled = false,
            string cacheProvider = "Memory",
            bool rateLimitingEnabled = false,
            int createPermitLimit = 60,
            int redirectPermitLimit = 120,
            bool securityEnabled = false,
            string securityApiKey = "test-admin-key",
            IReadOnlyList<string>? securityRoles = null,
            IReadOnlyList<string>? securityPermissions = null,
            bool tenantHeaderContext = false)
        {
            this.enableFrontendFallback = enableFrontendFallback;
            this.frontendFallbackPath = frontendFallbackPath;
            this.analyticsEnabled = analyticsEnabled;
            this.cacheEnabled = cacheEnabled;
            this.cacheProvider = cacheProvider;
            this.rateLimitingEnabled = rateLimitingEnabled;
            this.createPermitLimit = createPermitLimit;
            this.redirectPermitLimit = redirectPermitLimit;
            this.securityEnabled = securityEnabled;
            this.securityApiKey = securityApiKey;
            this.securityRoles = securityRoles ?? new[] { ShortenLinkRoles.Owner };
            this.securityPermissions = securityPermissions ?? Array.Empty<string>();
            this.tenantHeaderContext = tenantHeaderContext;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(databaseDirectory);

            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ShortenLink:BaseUrl"] = "https://sho.rt",
                    ["ShortenLink:Database:UsePostgres"] = "false",
                    ["ShortenLink:Database:SqliteConnectionString"] = $"Data Source={Path.Combine(databaseDirectory, "app.db")}",
                    ["ShortenLink:Redirect:EnableFrontendFallback"] = enableFrontendFallback.ToString(),
                    ["ShortenLink:Redirect:FrontendFallbackPath"] = frontendFallbackPath,
                    ["ShortenLink:Analytics:Enabled"] = analyticsEnabled.ToString(),
                    ["ShortenLink:Analytics:UseAsyncWorker"] = "true",
                    ["ShortenLink:Analytics:QueueCapacity"] = "32",
                    ["ShortenLink:Cache:Enabled"] = cacheEnabled.ToString(),
                    ["ShortenLink:Cache:Provider"] = cacheProvider,
                    ["ShortenLink:Cache:RedisConnectionString"] = "localhost:6379",
                    ["ShortenLink:Cache:EntryTtlSeconds"] = "300",
                    ["ShortenLink:RateLimiting:Enabled"] = rateLimitingEnabled.ToString(),
                    ["ShortenLink:RateLimiting:Create:PermitLimit"] = createPermitLimit.ToString(),
                    ["ShortenLink:RateLimiting:Create:WindowSeconds"] = "60",
                    ["ShortenLink:RateLimiting:Create:QueueLimit"] = "0",
                    ["ShortenLink:RateLimiting:Redirect:PermitLimit"] = redirectPermitLimit.ToString(),
                    ["ShortenLink:RateLimiting:Redirect:WindowSeconds"] = "60",
                    ["ShortenLink:RateLimiting:Redirect:QueueLimit"] = "0",
                    ["ShortenLink:Security:Enabled"] = securityEnabled.ToString(),
                    ["ShortenLink:Security:HeaderName"] = "X-ShortenLink-Api-Key",
                    ["ShortenLink:Security:ApiKeys:0:Name"] = "test-admin",
                    ["ShortenLink:Security:ApiKeys:0:Key"] = securityApiKey
                });
                for (var index = 0; index < securityRoles.Count; index++)
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [$"ShortenLink:Security:ApiKeys:0:Roles:{index}"] = securityRoles[index]
                    });
                }
                if (securityRoles.Count == 0)
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ShortenLink:Security:ApiKeys:0:Roles:0"] = string.Empty
                    });
                }

                for (var index = 0; index < securityPermissions.Count; index++)
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [$"ShortenLink:Security:ApiKeys:0:Permissions:{index}"] = securityPermissions[index]
                    });
                }
                if (securityPermissions.Count == 0)
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ShortenLink:Security:ApiKeys:0:Permissions:0"] = string.Empty
                    });
                }
            });
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(
                    new FixedTimeProvider(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero)));
                if (tenantHeaderContext)
                {
                    services.RemoveAll<ICurrentRequestContext>();
                    services.AddScoped<ICurrentRequestContext, TenantHeaderRequestContext>();
                }
            });
        }

        public async Task<List<ShortLinkPersistenceEntity>> GetShortLinksAsync()
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
            return await dbContext.ShortLinks
                .AsNoTracking()
                .OrderBy(link => link.Code)
                .ToListAsync();
        }

        public async Task<List<ShortLinkClickPersistenceEntity>> GetRecordedClicksAsync()
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();

            return await dbContext.ShortLinkClicks
                .AsNoTracking()
                .OrderBy(click => click.Id)
                .ToListAsync();
        }

        public async Task<List<ShortenLinkUserApiKeyPersistenceEntity>> GetUserApiKeyRecordsAsync()
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();

            return await dbContext.SecurityUserApiKeys
                .AsNoTracking()
                .OrderBy(apiKey => apiKey.Id)
                .ToListAsync();
        }

        public async Task UpsertSecurityAssignmentAsync(
            string apiKey,
            IReadOnlyList<string> roles,
            IReadOnlyList<string> permissions,
            bool isEnabled)
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var repository = new EfCoreShortenLinkSecurityAssignmentRepository(dbContext);
            await repository.AddOrUpdateAsync(new ShortenLinkSecurityAssignment(
                HashCredential(apiKey),
                "test-persisted-assignment",
                roles,
                permissions,
                isEnabled,
                new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero)));
        }

        public async Task UpsertSecurityUserAsync(
            string id,
            string username,
            string displayName,
            string password,
            IReadOnlyList<string> roles,
            bool isEnabled)
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            await dbContext.Database.EnsureCreatedAsync();

            var repository = new EfCoreShortenLinkSecurityUserRepository(dbContext);
            await repository.AddOrUpdateAsync(new ShortenLinkSecurityUser(
                id,
                username,
                displayName,
                ShortenLinkSecurityCredentialHasher.HashPassword(password),
                roles,
                isEnabled,
                isHidden: false,
                isBootstrap: false,
                new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero)));
        }

        public async Task SeedClickAsync(
            string shortCode,
            DateTimeOffset clickedAtUtc,
            string? remoteIpAddress,
            string? userAgent,
            string? referrer)
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var repository = new EfCoreShortLinkClickRepository(dbContext);
            await repository.AddAsync(new ShortLinkClick(
                shortCode,
                clickedAtUtc,
                remoteIpAddress,
                userAgent,
                referrer));
        }

        public async Task SeedShortLinkAsync(
            string code,
            string originalUrl,
            DateTimeOffset createdAt,
            DateTimeOffset expiresAt,
            bool isActive = true)
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var repository = new EfCoreShortLinkRepository(dbContext);
            await repository.AddAsync(new ShortLink(
                code,
                new Uri(originalUrl),
                createdAt,
                expiresAt,
                isActive));
        }

        public new ValueTask DisposeAsync()
        {
            base.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TenantHeaderRequestContext(IHttpContextAccessor httpContextAccessor)
        : ICurrentRequestContext
    {
        public Task EnsureAuthorizedAsync(
            string permission,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<CurrentRequestActor> AuthorizeAsync(
            string permission,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CurrentRequestActor(
                "tenant-admin",
                IsAdmin: true,
                ActorId: "tenant-admin",
                TenantId: GetTenantId()));

        public Task<CurrentUser?> GetCurrentUserAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CurrentUser?>(new CurrentUser(
                "tenant-admin",
                "tenant-admin",
                "Tenant Admin",
                [ShortenLinkRoles.Admin],
                ShortenLinkPermissionCatalog.All.ToList()));

        public Task<string?> GetCurrentTenantIdAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GetTenantId());

        private string? GetTenantId() =>
            httpContextAccessor.HttpContext?.Request.Headers["X-Test-Tenant-Id"].ToString();
    }

    private static ServiceProvider BuildServiceProvider(IDictionary<string, string?> overrides)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ShortenLink:BaseUrl"] = "https://sho.rt",
                ["ShortenLink:Database:UsePostgres"] = "false",
                ["ShortenLink:Database:SqliteConnectionString"] = "Data Source=shorten-link-provider-tests.db",
                ["ShortenLink:Redirect:EnableFrontendFallback"] = "true",
                ["ShortenLink:Redirect:FrontendFallbackPath"] = "/not-found",
                ["ShortenLink:Analytics:Enabled"] = "false",
                ["ShortenLink:Analytics:UseAsyncWorker"] = "true",
                ["ShortenLink:Analytics:QueueCapacity"] = "32",
                ["ShortenLink:Cache:Enabled"] = "false",
                ["ShortenLink:Cache:Provider"] = "Memory",
                ["ShortenLink:Cache:RedisConnectionString"] = "localhost:6379",
                ["ShortenLink:Cache:EntryTtlSeconds"] = "300",
                ["ShortenLink:RateLimiting:Enabled"] = "false",
                ["ShortenLink:RateLimiting:Create:PermitLimit"] = "60",
                ["ShortenLink:RateLimiting:Create:WindowSeconds"] = "60",
                ["ShortenLink:RateLimiting:Create:QueueLimit"] = "0",
                ["ShortenLink:RateLimiting:Redirect:PermitLimit"] = "120",
                ["ShortenLink:RateLimiting:Redirect:WindowSeconds"] = "60",
                ["ShortenLink:RateLimiting:Redirect:QueueLimit"] = "0",
                ["ShortenLink:Security:Enabled"] = "false",
                ["ShortenLink:Security:HeaderName"] = "X-ShortenLink-Api-Key",
                ["ShortenLink:Security:ApiKeys:0:Name"] = "test-admin",
                ["ShortenLink:Security:ApiKeys:0:Key"] = "test-admin-key",
                ["ShortenLink:Security:ApiKeys:0:Roles:0"] = ShortenLinkRoles.Owner
            })
            .AddInMemoryCollection(overrides)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddShortenLink(configuration);

        return services.BuildServiceProvider();
    }

    private static async Task<ShortLinkCreatedResponse> CreateShortLinkAsync(
        HttpClient client,
        string originalUrl,
        DateTimeOffset? expiredAtUtc = null)
    {
        expiredAtUtc ??= new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

        using var response = await client.PostAsJsonAsync("/api/short-links", new
        {
            originalUrl,
            expiredAtUtc
        });
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkCreatedResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(payload);

        return payload;
    }

    private static async Task<string[]> GetListCodesAsync(HttpClient client, string status)
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var filterExpression = status switch
        {
            "expired" => $"(IsActive eq `true`) & (ExpiresAt le `{now:O}`)",
            "inactive" => "(IsActive eq `false`)",
            "expiring-soon" => $"(IsActive eq `true`) & (ExpiresAt gt `{now:O}`) & (ExpiresAt le `{now.AddDays(7):O}`)",
            "active" => $"(IsActive eq `true`) & ((ExpiresAt eq `null`) | (ExpiresAt gt `{now:O}`))",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
        using var response = await client.GetAsync(
            $"/api/short-links?page=1&limit=10&fe={Uri.EscapeDataString(filterExpression)}&sort={Uri.EscapeDataString("+Code")}");
        var payload = await response.Content.ReadFromJsonAsync<ShortLinkAdminListResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);

        return payload.Items.Select(item => item.Code).ToArray();
    }

    private static IEnumerable<HttpRequestMessage> CreateAdminMutationRequests()
    {
        yield return new HttpRequestMessage(HttpMethod.Post, "/api/short-links")
        {
            Content = JsonContent.Create(new
            {
                originalUrl = "https://example.com/secure-create",
                expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
            })
        };
        yield return new HttpRequestMessage(HttpMethod.Put, "/api/short-links/missing")
        {
            Content = JsonContent.Create(new
            {
                originalUrl = "https://example.com/secure-update",
                expiredAtUtc = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
            })
        };
        yield return new HttpRequestMessage(HttpMethod.Post, "/api/short-links/missing/activate");
        yield return new HttpRequestMessage(HttpMethod.Post, "/api/short-links/missing/deactivate");
        yield return new HttpRequestMessage(HttpMethod.Delete, "/api/short-links/missing");
    }

    private static async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/security/login", new
        {
            email = "admin@shortenlink.local",
            password = "admin"
        });
        var payload = await response.Content.ReadFromJsonAsync<SecurityLoginResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));

        return payload.Token;
    }

    private static string HashCredential(string apiKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed record ShortLinkCreatedResponse(
        string Code,
        string ShortUrl,
        string OriginalUrl,
        DateTimeOffset CreatedAtUtc);

    private sealed record ShortLinkDetailsResponse(
        string Code,
        string OriginalUrl,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? ExpiredAtUtc,
        bool IsActive);

    private sealed record ShortLinkAnalyticsResponse(
        string Code,
        long ClickCount,
        DateTimeOffset? LastClickedAtUtc,
        IReadOnlyList<ShortLinkClickActivityResponse> RecentClicks);

    private sealed record ShortLinkClickActivityResponse(
        DateTimeOffset ClickedAtUtc,
        string? RemoteIpAddress,
        string? UserAgent,
        string? Referrer);

    private sealed record SecurityAssignmentsListResponse(
        IReadOnlyList<SecurityAssignmentResponse> Items);

    private sealed record SecurityLoginResponse(
        string Token,
        string AccessToken,
        string RefreshToken,
        SecurityCurrentUserResponse User);

    private sealed record SecurityCurrentUserResponse(
        string UserId,
        string Username,
        string DisplayName,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions,
        DateTimeOffset IssuedAtUtc);

    private sealed record SecurityUserApiKeysListResponse(
        IReadOnlyList<SecurityUserApiKeyResponse> Items);

    private sealed record SecurityUserApiKeyCreatedResponse(
        SecurityUserApiKeyResponse ApiKey,
        string RawApiKey);

    private sealed record SecurityUserApiKeyResponse(
        string Id,
        string DisplayName,
        bool IsEnabled,
        DateTimeOffset CreatedAtUtc);

    private sealed record SecurityUserApiKeyDisabledResponse(
        string Id,
        bool IsEnabled);

    private sealed record SecurityRolesListResponse(
        IReadOnlyList<SecurityRoleResponse> SystemRoles,
        IReadOnlyList<SecurityRoleResponse> CustomRoles);

    private sealed record SecurityRoleResponse(
        string Id,
        string Name,
        IReadOnlyList<string> Permissions,
        IReadOnlyList<string> DefaultPermissions,
        IReadOnlyList<SecurityRolePermissionOverrideResponse> PermissionOverrides,
        bool IsSystem,
        bool IsEnabled,
        bool CanDelete,
        DateTimeOffset? CreatedAtUtc);

    private sealed record SecurityRolePermissionOverrideResponse(
        string Permission,
        bool IsAllowed);

    private sealed record SecurityRoleDeletedResponse(string Id);

    private sealed record SecurityUsersListResponse(
        IReadOnlyList<SecurityUserResponse> Items);

    private sealed record SecurityUserResponse(
        string Id,
        string Username,
        string DisplayName,
        IReadOnlyList<string> RoleIds,
        bool IsEnabled,
        bool IsHidden,
        bool IsBootstrap,
        DateTimeOffset CreatedAtUtc);

    private sealed record SecurityUserDisabledResponse(
        string Id,
        bool IsEnabled);

    private sealed record SecurityAssignmentResponse(
        string CredentialKeyHash,
        string Name,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions,
        bool IsEnabled,
        DateTimeOffset CreatedAtUtc);

    private sealed record SecurityAssignmentDisabledResponse(
        string CredentialKeyHash,
        bool IsEnabled);

    private sealed record ShortLinkAdminListItemResponse(
        string Code,
        string ShortUrl,
        string OriginalUrl,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? ExpiredAtUtc,
        bool IsActive,
        string? AccessLevel = null);

    private sealed record ShortLinkAdminListResponse(
        IReadOnlyList<ShortLinkAdminListItemResponse> Items,
        string? NextCursor,
        int? TotalCount,
        int? Page,
        int? PageSize,
        int? TotalPages);

    private sealed record MockSeedShortLinksResponse(
        int RequestedCount,
        int CreatedCount,
        int FailedCount,
        IReadOnlyList<string> Codes);

    private sealed record ShortLinkErrorResponse(
        string ErrorCode,
        string Message,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? FieldErrors = null);

    private static async Task<bool> WaitForConditionAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return false;
    }

    [Fact]
    public void AddShortenLink_PreservesConsumerAuthorizationOverrides()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ShortenLink:Database:SqliteConnectionString"] = "Data Source=override-test.db"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICurrentRequestContext, ConsumerRequestContext>();
        services.AddScoped<IShortenLinkAuthorizationService, ConsumerAuthorizationService>();
        services.AddShortenLink(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.IsType<ConsumerRequestContext>(provider.GetRequiredService<ICurrentRequestContext>());
        Assert.IsType<ConsumerAuthorizationService>(provider.GetRequiredService<IShortenLinkAuthorizationService>());
        Assert.NotNull(provider.GetRequiredService<ISecuritySessionService>());
    }

    private sealed class ConsumerRequestContext : ICurrentRequestContext
    {
        public Task EnsureAuthorizedAsync(string permission, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CurrentRequestActor> AuthorizeAsync(string permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CurrentRequestActor("consumer-user", false, "consumer:user"));

        public Task<CurrentUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<CurrentUser?>(new CurrentUser("consumer-user", "consumer", "Consumer", [], []));
    }

    private sealed class ConsumerAuthorizationService : IShortenLinkAuthorizationService
    {
        public Task<ShortenLinkAuthorizationResult> AuthorizeAsync(
            HttpContext httpContext,
            string permission,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ShortenLinkAuthorizationResult.Success("consumer-user", false, "consumer:user"));
    }

    private static async Task<ShortLinkAuditEventsResponse> WaitForAuditPayloadAsync(
        HttpClient client,
        string requestUri,
        Func<ShortLinkAuditEventsResponse, bool> isReady)
    {
        ShortLinkAuditEventsResponse? lastPayload = null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            using var response = await client.GetAsync(requestUri);
            if (response.IsSuccessStatusCode)
            {
                lastPayload = await response.Content.ReadFromJsonAsync<ShortLinkAuditEventsResponse>();
                if (lastPayload is not null && isReady(lastPayload))
                {
                    return lastPayload;
                }
            }

            await Task.Delay(50);
        }

        Assert.NotNull(lastPayload);
        throw new InvalidOperationException("Audit worker did not persist the expected events in time.");
    }
}
