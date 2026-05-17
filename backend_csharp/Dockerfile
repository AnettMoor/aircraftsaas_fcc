# Multi-stage build for AircraftSaaS
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution file
COPY AircraftSaaS/AircraftSaaS.sln .

# Copy project files only (for restore)
COPY AircraftSaaS/Base.Contracts.Domain/Base.Contracts.Domain.csproj ./Base.Contracts.Domain/
COPY AircraftSaaS/Base.Domain/Base.Domain.csproj ./Base.Domain/
COPY AircraftSaaS/Base.DAL.Contracts/Base.DAL.Contracts.csproj ./Base.DAL.Contracts/
COPY AircraftSaaS/Base.DAL.EF/Base.DAL.EF.csproj ./Base.DAL.EF/
COPY AircraftSaaS/Base.Helpers/Base.Helpers.csproj ./Base.Helpers/
COPY AircraftSaaS/App.Domain/App.Domain.csproj ./App.Domain/
COPY AircraftSaaS/App.Application/App.Application.csproj ./App.Application/
COPY AircraftSaaS/App.Infrastructure/App.Infrastructure.csproj ./App.Infrastructure/
COPY AircraftSaaS/App.Resources/App.Resources.csproj ./App.Resources/
COPY AircraftSaaS/WebApp/WebApp.csproj ./WebApp/

# Copy test project files (for restore)
COPY AircraftSaaS/Tests/Domain.Tests/Domain.Tests.csproj ./Tests/Domain.Tests/
COPY AircraftSaaS/Tests/Application.Tests/Application.Tests.csproj ./Tests/Application.Tests/
COPY AircraftSaaS/Tests/Infrastructure.Tests/Infrastructure.Tests.csproj ./Tests/Infrastructure.Tests/
COPY AircraftSaaS/Tests/WebApp.Tests/WebApp.Tests.csproj ./Tests/WebApp.Tests/

# Restore NuGet packages
RUN dotnet restore

# Copy all source code
COPY AircraftSaaS/Base.Contracts.Domain/. ./Base.Contracts.Domain/
COPY AircraftSaaS/Base.Domain/. ./Base.Domain/
COPY AircraftSaaS/Base.DAL.Contracts/. ./Base.DAL.Contracts/
COPY AircraftSaaS/Base.DAL.EF/. ./Base.DAL.EF/
COPY AircraftSaaS/Base.Helpers/. ./Base.Helpers/
COPY AircraftSaaS/App.Domain/. ./App.Domain/
COPY AircraftSaaS/App.Application/. ./App.Application/
COPY AircraftSaaS/App.Infrastructure/. ./App.Infrastructure/
COPY AircraftSaaS/App.Resources/. ./App.Resources/
COPY AircraftSaaS/WebApp/. ./WebApp/

# Copy test source code
COPY AircraftSaaS/Tests/Domain.Tests/. ./Tests/Domain.Tests/
COPY AircraftSaaS/Tests/Application.Tests/. ./Tests/Application.Tests/
COPY AircraftSaaS/Tests/Infrastructure.Tests/. ./Tests/Infrastructure.Tests/
COPY AircraftSaaS/Tests/WebApp.Tests/. ./Tests/WebApp.Tests/

# Run unit tests (exclude Infrastructure.Tests which requires Testcontainers/Docker)
FROM build AS test
RUN dotnet test Tests/Domain.Tests/ --configuration Release --no-restore --logger "console;verbosity=normal" && \
    dotnet test Tests/Application.Tests/ --configuration Release --no-restore --logger "console;verbosity=normal"

# Build and publish
FROM build AS publish
RUN dotnet publish WebApp/WebApp.csproj -c Release -o out

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Allow ASPNETCORE_URLS to be overridden (defaults to port 8080)
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# Connection string pointing to postgres service in docker-compose
ENV ConnectionStrings:DefaultConnection="Host=postgres-dev;Port=5432;Database=aircraft;Username=postgres;Password=postgres;Timeout=30;Command Timeout=60"

COPY --from=publish /app/out ./

ENTRYPOINT ["dotnet", "WebApp.dll"]

# Health check for the webapp
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:8080/Health || exit 1
