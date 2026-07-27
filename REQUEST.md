# REQUEST.md — Shorten Link Library + Demo App

## 1. Mục tiêu

Xây dựng một **Shorten Link Library** dùng lại được cho nhiều source/project .NET khác, sau đó xây dựng một **Demo/Application source** sử dụng library này để tạo, quản lý và redirect short link.

Hệ thống nên đi theo hướng dễ mở rộng như kiến trúc URL Shortener: API layer, cache, database, key generation, analytics/worker optional.

## 2. Tech Stack đề xuất

### Backend

- .NET 10 hoặc .NET 8 LTS
- ASP.NET Core Web API
- Entity Framework Core
- SQLite làm database mặc định
- PostgreSQL có thể bật/tắt bằng config toggle
- Redis cache optional, có thể implement sau
- Clean Architecture / Modular style

### Frontend

- React + Vite
- TypeScript
- TailwindCSS
- Gọi API backend để tạo, xem, xoá, copy short link
- Có fallback FE routing/page khi backend redirect hoặc route không tồn tại nếu cần

## 3. Project Structure mong muốn

```txt
shorten-link-solution/
├── shared/
│   └── ShortenLink.Hosting/        # ASP.NET Core DI setup, middleware, and host integration
├── src/
│   ├── ShortenLink.Core/              # Domain models, interfaces, core logic
│   ├── ShortenLink.Infrastructure/    # EF Core, repositories, SQLite/Postgres provider
│   ├── ShortenLink.Worker/            # Optional analytics/background jobs
│   ├── ShortenLink.Api/               # Demo backend API using the library
│   └── ShortenLink.Web/               # React frontend
├── tests/
│   ├── ShortenLink.Core.Tests/
│   ├── ShortenLink.Infrastructure.Tests/
│   └── ShortenLink.Api.Tests/
├── README.md
└── REQUEST.md
```

## 4. Library requirements

### 4.1 ShortenLink.Core

Library core cần chứa logic độc lập, không phụ thuộc trực tiếp vào database cụ thể.

Cần có các model chính:

```csharp
public class ShortLink
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string OriginalUrl { get; set; } = default!;
    public string? Title { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiredAtUtc { get; set; }
    public bool IsActive { get; set; }
    public long ClickCount { get; set; }
}
```

Optional model:

```csharp
public class ShortLinkClick
{
    public Guid Id { get; set; }
    public Guid ShortLinkId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Referer { get; set; }
    public DateTime ClickedAtUtc { get; set; }
}
```

Cần có interface:

```csharp
public interface IShortLinkService
{
    Task<ShortLinkResult> CreateAsync(CreateShortLinkRequest request, CancellationToken cancellationToken = default);
    Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<string?> ResolveOriginalUrlAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string code, CancellationToken cancellationToken = default);
}
```

```csharp
public interface IShortCodeGenerator
{
    string Generate(int length = 7);
}
```

```csharp
public interface IShortLinkRepository
{
    Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddAsync(ShortLink shortLink, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

### 4.2 Short code generation

Yêu cầu:

- Generate code dạng Base62: `a-z`, `A-Z`, `0-9`
- Default length: 7 ký tự
- Có retry khi code bị trùng
- Cho phép custom alias nếu user nhập
- Validate alias chỉ gồm chữ, số, `_`, `-`
- Không cho tạo duplicate code

### 4.3 URL validation

Yêu cầu:

- Chỉ accept URL có scheme `http` hoặc `https`
- Reject empty URL
- Reject invalid URL format
- Optional: block localhost/private IP trong production config

## 5. Infrastructure requirements

### 5.1 Database provider toggle

Mặc định dùng SQLite.

Có config toggle để bật PostgreSQL:

```json
{
  "ShortenLink": {
    "Database": {
      "UsePostgres": false,
      "SqliteConnectionString": "Data Source=shorten-link.db",
      "PostgresConnectionString": "Host=localhost;Port=5432;Database=shorten_link;Username=postgres;Password=postgres"
    }
  }
}
```

Rule:

- Nếu `UsePostgres = false` → dùng SQLite
- Nếu `UsePostgres = true` → dùng PostgreSQL
- Không cần đổi code khi switch DB
- Migration phải support cả SQLite và PostgreSQL nếu khả thi

### 5.2 EF Core DbContext

Cần có:

```csharp
public class ShortenLinkDbContext : DbContext
{
    public DbSet<ShortLink> ShortLinks => Set<ShortLink>();
    public DbSet<ShortLinkClick> ShortLinkClicks => Set<ShortLinkClick>();
}
```

Indexes:

- Unique index cho `Code`
- Index cho `CreatedAtUtc`
- Index cho `ExpiredAtUtc`

### 5.3 Dependency Injection extension

Library cần expose extension để project khác dùng dễ dàng:

```csharp
builder.Services.AddShortenLink(builder.Configuration);
```

Optional endpoint mapping:

```csharp
app.MapShortenLinkEndpoints();
```

## 6. API Demo requirements

Demo backend `ShortenLink.Api` cần sử dụng library đã build.

### 6.1 Endpoints

#### Create short link

```http
POST /api/short-links
```

Request:

```json
{
  "originalUrl": "https://example.com/very/long/url",
  "customAlias": "my-link",
  "expiredAtUtc": null
}
```

Response:

```json
{
  "code": "my-link",
  "shortUrl": "https://localhost:5001/my-link",
  "originalUrl": "https://example.com/very/long/url",
  "createdAtUtc": "2026-01-01T00:00:00Z"
}
```

#### Redirect short link

```http
GET /{code}
```

Behavior:

- Nếu tìm thấy code và link active → HTTP 302 redirect tới original URL
- Nếu không tìm thấy → fallback sang FE hoặc trả 404 tuỳ config
- Nếu expired → trả 410 Gone hoặc fallback page

#### Get detail

```http
GET /api/short-links/{code}
```

#### Delete / deactivate

```http
DELETE /api/short-links/{code}
```

## 7. Frontend requirements

Frontend React cần có các màn hình cơ bản:

### 7.1 Home page

- Input long URL
- Optional custom alias
- Optional expired date
- Button tạo short link
- Hiển thị short URL sau khi tạo
- Button copy short URL

### 7.2 Detail page

- Xem original URL
- Xem short code
- Xem created date
- Xem expired date
- Xem click count

### 7.3 Not found / fallback page

Khi short code không tồn tại hoặc FE route không match:

- Hiển thị page friendly: “Short link not found”
- Có button quay về Home

## 8. Fallback FE behavior

Cần có config:

```json
{
  "ShortenLink": {
    "Redirect": {
      "EnableFrontendFallback": true,
      "FrontendFallbackPath": "/not-found"
    }
  }
}
```

Behavior:

- Nếu `EnableFrontendFallback = true`, khi code không tồn tại thì redirect/render FE fallback page
- Nếu `EnableFrontendFallback = false`, API trả HTTP 404 JSON response

## 9. Analytics / Worker optional

Có thể implement sau, nhưng design sẵn interface.

Yêu cầu optional:

- Track click count
- Track IP, user agent, referer
- Có thể xử lý async bằng background worker/channel/queue
- Không làm redirect bị chậm vì analytics

Interface gợi ý:

```csharp
public interface IClickTrackingService
{
    Task TrackAsync(ClickTrackingRequest request, CancellationToken cancellationToken = default);
}
```

## 10. Cache optional

Có thể implement sau Redis, nhưng design sẵn abstraction.

```csharp
public interface IShortLinkCache
{
    Task<string?> GetOriginalUrlAsync(string code, CancellationToken cancellationToken = default);
    Task SetOriginalUrlAsync(string code, string originalUrl, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task RemoveAsync(string code, CancellationToken cancellationToken = default);
}
```

Behavior mong muốn:

- Redirect lookup ưu tiên cache
- Cache miss thì query DB
- Sau khi query DB thành công thì set cache
- Khi delete/deactivate link thì remove cache

## 11. Configuration full sample

```json
{
  "ShortenLink": {
    "BaseUrl": "https://localhost:5001",
    "Code": {
      "DefaultLength": 7,
      "MaxRetry": 5,
      "AllowCustomAlias": true
    },
    "Database": {
      "UsePostgres": false,
      "SqliteConnectionString": "Data Source=shorten-link.db",
      "PostgresConnectionString": "Host=localhost;Port=5432;Database=shorten_link;Username=postgres;Password=postgres"
    },
    "Redirect": {
      "EnableFrontendFallback": true,
      "FrontendFallbackPath": "/not-found"
    },
    "Analytics": {
      "Enabled": true,
      "UseAsyncWorker": true
    },
    "Cache": {
      "Enabled": false,
      "Provider": "Memory",
      "RedisConnectionString": "localhost:6379"
    }
  }
}
```

## 12. NuGet/library usage expectation

Sau này source .NET khác có thể dùng như sau:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddShortenLink(builder.Configuration);

var app = builder.Build();

app.MapShortenLinkEndpoints();

app.Run();
```

Hoặc dùng service trực tiếp:

```csharp
public class MyService
{
    private readonly IShortLinkService _shortLinkService;

    public MyService(IShortLinkService shortLinkService)
    {
        _shortLinkService = shortLinkService;
    }

    public Task<ShortLinkResult> CreateAsync(string url)
    {
        return _shortLinkService.CreateAsync(new CreateShortLinkRequest
        {
            OriginalUrl = url
        });
    }
}
```

## 13. Non-functional requirements

- Code rõ ràng, dễ reuse
- Có unit test cho short code generator, URL validation, service create/resolve
- Có integration test cho SQLite
- Không hard-code database provider
- Config-driven behavior
- Có README hướng dẫn chạy SQLite và PostgreSQL
- Có migration hoặc auto-create database cho local development

## 14. Acceptance criteria

Hoàn thành khi:

- Build solution thành công
- Demo API chạy được với SQLite mặc định
- Có thể bật `UsePostgres = true` để chạy bằng PostgreSQL
- FE React tạo được short link thông qua API
- Redirect `/{code}` hoạt động đúng
- Custom alias hoạt động và check duplicate
- Link không tồn tại xử lý theo config fallback FE hoặc 404
- Library có thể được reference từ project .NET khác
- Có test tối thiểu cho core logic

## 15. Priority

### Phase 1 — MVP

- Core library
- SQLite default
- Create short link
- Redirect short link
- React form tạo link
- FE fallback page

### Phase 2 — PostgreSQL toggle

- Add PostgreSQL provider
- Config toggle `UsePostgres`
- Migration/update database guide

### Phase 3 — Improve production readiness

- Analytics click tracking
- Cache abstraction
- Redis provider
- Background worker
- Rate limiting
- Docker Compose
- GitHub Actions CI
