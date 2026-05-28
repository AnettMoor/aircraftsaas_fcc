# =====================================================================
# Users.WebHost - hardened multi-stage Dockerfile
#   * Minimal (distroless) runtime image: Ubuntu chiseled
#   * Runs as non-root (UID 1654, provided by the chiseled image)
#   * No shell, no package manager, no curl/wget in the final layer
#   * No HEALTHCHECK directive - health is enforced by Kubernetes
#     readiness/liveness probes against /health (see k8s manifests).
# =====================================================================

# --- Build stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files (restore layer is cached as long as csproj's don't change)
COPY Shared/Shared.Kernel/Shared.Kernel.csproj                       Shared/Shared.Kernel/
COPY Shared/Shared.Contracts/Shared.Contracts.csproj                 Shared/Shared.Contracts/
COPY Shared/Shared.Messaging/Shared.Messaging.csproj                 Shared/Shared.Messaging/
COPY Modules/Users/Users.Domain/Users.Domain.csproj                  Modules/Users/Users.Domain/
COPY Modules/Users/Users.Application/Users.Application.csproj        Modules/Users/Users.Application/
COPY Modules/Users/Users.Api/Users.Api.csproj                        Modules/Users/Users.Api/
COPY Modules/Users/Users.Infrastructure/Users.Infrastructure.csproj  Modules/Users/Users.Infrastructure/
COPY Services/Users.WebHost/Users.WebHost.csproj                     Services/Users.WebHost/

RUN dotnet restore Services/Users.WebHost/Users.WebHost.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish Services/Users.WebHost/Users.WebHost.csproj \
        -c Release \
        -o /app/publish \
        /p:UseAppHost=false

# --- Runtime stage (hardened, chiseled, non-root) -----------------------
# chiseled-extra includes ICU + tzdata + ca-certificates but still no shell.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final
WORKDIR /app

# Bind to an unprivileged port (matches Kubernetes Service port mapping)
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# The chiseled-extra image already ships USER $APP_UID (1654); we re-declare
# it explicitly so the intent is visible in code review and audits.
USER $APP_UID

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

# Disable Docker HEALTHCHECK - chiseled has no shell, and production health
# is enforced by Kubernetes HTTP probes against /health.
HEALTHCHECK NONE

ENTRYPOINT ["dotnet", "Users.WebHost.dll"]
