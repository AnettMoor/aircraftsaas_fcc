# Multi-stage build for AircraftSaaS
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution file
COPY AircraftSaaS/AircraftSaaS.sln .

# Copy project files only (for restore)
COPY AircraftSaaS/App.Resources/App.Resources.csproj ./App.Resources/
COPY AircraftSaaS/WebApp/WebApp.csproj ./WebApp/

# Copy project files — Shared modules
COPY AircraftSaaS/Shared/Shared.Kernel/Shared.Kernel.csproj ./Shared/Shared.Kernel/
COPY AircraftSaaS/Shaairred/Shared.Contracts/Shared.Contracts.csproj ./Shared/Shared.Contracts/

# Copy project files — Users module
COPY AircraftSaaS/Modules/Users/Users.Domain/Users.Domain.csproj ./Modules/Users/Users.Domain/
COPY AircraftSaaS/Modules/Users/Users.Application/Users.Application.csproj ./Modules/Users/Users.Application/
COPY AircraftSaaS/Modules/Users/Users.Infrastructure/Users.Infrastructure.csproj ./Modules/Users/Users.Infrastructure/
COPY AircraftSaaS/Modules/Users/Users.Resources/Users.Resources.csproj ./Modules/Users/Users.Resources/
COPY AircraftSaaS/Modules/Users/Users.Api/Users.Api.csproj ./Modules/Users/Users.Api/

# Copy project files — Fleet module
COPY AircraftSaaS/Modules/Fleet/Fleet.Domain/Fleet.Domain.csproj ./Modules/Fleet/Fleet.Domain/
COPY AircraftSaaS/Modules/Fleet/Fleet.Application/Fleet.Application.csproj ./Modules/Fleet/Fleet.Application/
COPY AircraftSaaS/Modules/Fleet/Fleet.Infrastructure/Fleet.Infrastructure.csproj ./Modules/Fleet/Fleet.Infrastructure/
COPY AircraftSaaS/Modules/Fleet/Fleet.Resources/Fleet.Resources.csproj ./Modules/Fleet/Fleet.Resources/
COPY AircraftSaaS/Modules/Fleet/Fleet.Api/Fleet.Api.csproj ./Modules/Fleet/Fleet.Api/

# Copy project files — Booking module
COPY AircraftSaaS/Modules/Booking/Booking.Domain/Booking.Domain.csproj ./Modules/Booking/Booking.Domain/
COPY AircraftSaaS/Modules/Booking/Booking.Application/Booking.Application.csproj ./Modules/Booking/Booking.Application/
COPY AircraftSaaS/Modules/Booking/Booking.Infrastructure/Booking.Infrastructure.csproj ./Modules/Booking/Booking.Infrastructure/
COPY AircraftSaaS/Modules/Booking/Booking.Resources/Booking.Resources.csproj ./Modules/Booking/Booking.Resources/
COPY AircraftSaaS/Modules/Booking/Booking.Api/Booking.Api.csproj ./Modules/Booking/Booking.Api/

# Copy test project files (for restore)
COPY AircraftSaaS/Tests/Fleet.Tests/Fleet.Tests.csproj ./Tests/Fleet.Tests/
COPY AircraftSaaS/Tests/Booking.Tests/Booking.Tests.csproj ./Tests/Booking.Tests/
COPY AircraftSaaS/Tests/Users.Tests/Users.Tests.csproj ./Tests/Users.Tests/
COPY AircraftSaaS/Tests/Integration.Tests/Integration.Tests.csproj ./Tests/Integration.Tests/
COPY AircraftSaaS/Tests/WebApp.Tests/WebApp.Tests.csproj ./Tests/WebApp.Tests/

# Restore NuGet packages
RUN dotnet restore -v normal

# Copy all source code
COPY AircraftSaaS/App.Resources/. ./App.Resources/
COPY AircraftSaaS/WebApp/. ./WebApp/

# Copy source code — Shared modules
COPY AircraftSaaS/Shared/Shared.Kernel/. ./Shared/Shared.Kernel/
COPY AircraftSaaS/Shared/Shared.Contracts/. ./Shared/Shared.Contracts/

# Copy source code — Users module
COPY AircraftSaaS/Modules/Users/Users.Domain/. ./Modules/Users/Users.Domain/
COPY AircraftSaaS/Modules/Users/Users.Application/. ./Modules/Users/Users.Application/
COPY AircraftSaaS/Modules/Users/Users.Infrastructure/. ./Modules/Users/Users.Infrastructure/
COPY AircraftSaaS/Modules/Users/Users.Resources/. ./Modules/Users/Users.Resources/
COPY AircraftSaaS/Modules/Users/Users.Api/. ./Modules/Users/Users.Api/

# Copy source code — Fleet module
COPY AircraftSaaS/Modules/Fleet/Fleet.Domain/. ./Modules/Fleet/Fleet.Domain/
COPY AircraftSaaS/Modules/Fleet/Fleet.Application/. ./Modules/Fleet/Fleet.Application/
COPY AircraftSaaS/Modules/Fleet/Fleet.Infrastructure/. ./Modules/Fleet/Fleet.Infrastructure/
COPY AircraftSaaS/Modules/Fleet/Fleet.Resources/. ./Modules/Fleet/Fleet.Resources/
COPY AircraftSaaS/Modules/Fleet/Fleet.Api/. ./Modules/Fleet/Fleet.Api/

# Copy source code — Booking module
COPY AircraftSaaS/Modules/Booking/Booking.Domain/. ./Modules/Booking/Booking.Domain/
COPY AircraftSaaS/Modules/Booking/Booking.Application/. ./Modules/Booking/Booking.Application/
COPY AircraftSaaS/Modules/Booking/Booking.Infrastructure/. ./Modules/Booking/Booking.Infrastructure/
COPY AircraftSaaS/Modules/Booking/Booking.Resources/. ./Modules/Booking/Booking.Resources/
COPY AircraftSaaS/Modules/Booking/Booking.Api/. ./Modules/Booking/Booking.Api/

# Copy test source code
COPY AircraftSaaS/Tests/Fleet.Tests/. ./Tests/Fleet.Tests/
COPY AircraftSaaS/Tests/Booking.Tests/. ./Tests/Booking.Tests/
COPY AircraftSaaS/Tests/Users.Tests/. ./Tests/Users.Tests/
COPY AircraftSaaS/Tests/Integration.Tests/. ./Tests/Integration.Tests/
COPY AircraftSaaS/Tests/WebApp.Tests/. ./Tests/WebApp.Tests/

# Run unit tests (exclude Integration.Tests which requires Testcontainers/Docker)
FROM build AS test
RUN dotnet test Tests/Fleet.Tests/ --configuration Release --no-restore --logger "console;verbosity=normal" && \
    dotnet test Tests/Booking.Tests/ --configuration Release --no-restore --logger "console;verbosity=normal" && \
    dotnet test Tests/Users.Tests/ --configuration Release --no-restore --logger "console;verbosity=normal"

# Build and publish
FROM build AS publish
RUN dotnet publish WebApp/WebApp.csproj -c Release -o out

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Allow ASPNETCORE_URLS to be overridden (defaults to port 8080)
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# Connection strings pointing to postgres service in docker-compose (one per module)
ENV ConnectionStrings__UsersConnection="Host=postgres-aircraft-modular;Port=5432;Database=aircraft-users;Username=postgres;Password=postgres;Timeout=30;Command Timeout=60"
ENV ConnectionStrings__FleetConnection="Host=postgres-aircraft-modular;Port=5432;Database=aircraft-fleet;Username=postgres;Password=postgres;Timeout=30;Command Timeout=60"
ENV ConnectionStrings__BookingConnection="Host=postgres-aircraft-modular;Port=5432;Database=aircraft-booking;Username=postgres;Password=postgres;Timeout=30;Command Timeout=60"

COPY --from=publish /app/out ./

ENTRYPOINT ["dotnet", "WebApp.dll"]

# Health check for the webapp (wget is available in aspnet image; curl is not)
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=5 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/Health || exit 1
