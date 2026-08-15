FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/ShortenLink.Api/ShortenLink.Api.csproj", "src/ShortenLink.Api/"]
COPY ["shared/ShortenLink.Auditing/ShortenLink.Auditing.csproj", "shared/ShortenLink.Auditing/"]
COPY ["shared/ShortenLink.Mediator/ShortenLink.Mediator.csproj", "shared/ShortenLink.Mediator/"]
COPY ["shared/ShortenLink.Messaging/ShortenLink.Messaging.csproj", "shared/ShortenLink.Messaging/"]
COPY ["shared/ShortenLink.Hosting/ShortenLink.Hosting.csproj", "shared/ShortenLink.Hosting/"]
COPY ["src/ShortenLink.Application/ShortenLink.Application.csproj", "src/ShortenLink.Application/"]
COPY ["src/ShortenLink.Core/ShortenLink.Core.csproj", "src/ShortenLink.Core/"]
COPY ["src/ShortenLink.Infrastructure/ShortenLink.Infrastructure.csproj", "src/ShortenLink.Infrastructure/"]
RUN dotnet restore "src/ShortenLink.Api/ShortenLink.Api.csproj"

COPY . .
RUN dotnet publish "src/ShortenLink.Api/ShortenLink.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ShortenLink__Database__SqliteConnectionString="Data Source=/data/shorten-link.db"

RUN mkdir /data \
    && chown $APP_UID /data

COPY --from=build /app/publish .

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "ShortenLink.Api.dll"]
