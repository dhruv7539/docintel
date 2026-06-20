# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first for better layer caching.
COPY src/DocIntel.Api/DocIntel.Api.csproj src/DocIntel.Api/
RUN dotnet restore src/DocIntel.Api/DocIntel.Api.csproj

COPY src/ src/
RUN dotnet publish src/DocIntel.Api/DocIntel.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DocIntel.Api.dll"]
