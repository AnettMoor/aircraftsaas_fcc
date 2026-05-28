# WebApp/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore
COPY Shared/Shared.Kernel/Shared.Kernel.csproj Shared/Shared.Kernel/
COPY Shared/Shared.Contracts/Shared.Contracts.csproj Shared/Shared.Contracts/
COPY Shared/Shared.Messaging/Shared.Messaging.csproj Shared/Shared.Messaging/
COPY Modules/Users/Users.Domain/Users.Domain.csproj Modules/Users/Users.Domain/
COPY Modules/Users/Users.Application/Users.Application.csproj Modules/Users/Users.Application/
COPY Modules/Users/Users.Api/Users.Api.csproj Modules/Users/Users.Api/
COPY Modules/Users/Users.Infrastructure/Users.Infrastructure.csproj Modules/Users/Users.Infrastructure/
COPY Modules/Fleet/Fleet.Domain/Fleet.Domain.csproj Modules/Fleet/Fleet.Domain/
COPY Modules/Fleet/Fleet.Application/Fleet.Application.csproj Modules/Fleet/Fleet.Application/
COPY Modules/Fleet/Fleet.Api/Fleet.Api.csproj Modules/Fleet/Fleet.Api/
COPY Modules/Fleet/Fleet.Infrastructure/Fleet.Infrastructure.csproj Modules/Fleet/Fleet.Infrastructure/
COPY Modules/Booking/Booking.Domain/Booking.Domain.csproj Modules/Booking/Booking.Domain/
COPY Modules/Booking/Booking.Application/Booking.Application.csproj Modules/Booking/Booking.Application/
COPY Modules/Booking/Booking.Api/Booking.Api.csproj Modules/Booking/Booking.Api/
COPY Modules/Booking/Booking.Infrastructure/Booking.Infrastructure.csproj Modules/Booking/Booking.Infrastructure/
COPY App.Resources/App.Resources.csproj App.Resources/
COPY WebApp/WebApp.csproj WebApp/

RUN dotnet restore WebApp/WebApp.csproj

# Copy everything and build
COPY . .
RUN dotnet publish WebApp/WebApp.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WebApp.dll"]
