FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/ShortenLink.Api/ShortenLink.Api.csproj", "src/ShortenLink.Api/"]
COPY ["shared/ShortenLink.Mediator/ShortenLink.Mediator.csproj", "shared/ShortenLink.Mediator/"]
COPY ["shared/ShortenLink.Hosting/ShortenLink.Hosting.csproj", "shared/ShortenLink.Hosting/"]
COPY ["src/ShortenLink.Application/ShortenLink.Application.csproj", "src/ShortenLink.Application/"]
COPY ["src/ShortenLink.Core/ShortenLink.Core.csproj", "src/ShortenLink.Core/"]
COPY ["src/ShortenLink.Infrastructure/ShortenLink.Infrastructure.csproj", "src/ShortenLink.Infrastructure/"]
RUN dotnet restore "src/ShortenLink.Api/ShortenLink.Api.csproj"

COPY . .
RUN dotnet publish "src/ShortenLink.Api/ShortenLink.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ShortenLink.Api.dll"]
