# src/Strategic.WebApi/Dockerfile

# ---- RUNTIME ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

# ---- BUILD ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /work

# Copia SOLO i csproj per massimizzare la cache della restore
COPY src/Strategic.WebApi/Strategic.WebApi.csproj src/Strategic.WebApi/
COPY src/Impostor.Plugins.SemanticAnnotator.Abstractions/Impostor.Plugins.SemanticAnnotator.Abstractions.csproj src/Impostor.Plugins.SemanticAnnotator.Abstractions/

# Restore
RUN dotnet restore src/Strategic.WebApi/Strategic.WebApi.csproj

# Copia il resto della repo
COPY . .

# Publish
WORKDIR /work/src/Strategic.WebApi
RUN dotnet publish -c Release -o /out /p:UseAppHost=false

# ---- FINAL ----
FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "Strategic.WebApi.dll"]
