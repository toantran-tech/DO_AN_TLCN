# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for caching dotnet restore layers
COPY ["WashGo.HttpApi.Host/WashGo.HttpApi.Host.csproj", "WashGo.HttpApi.Host/"]
COPY ["src/WashGo.HttpApi/WashGo.HttpApi.csproj", "src/WashGo.HttpApi/"]
COPY ["src/WashGo.Application/WashGo.Application.csproj", "src/WashGo.Application/"]
COPY ["src/WashGo.Application.Contracts/WashGo.Application.Contracts.csproj", "src/WashGo.Application.Contracts/"]
COPY ["src/WashGo.Domain/WashGo.Domain.csproj", "src/WashGo.Domain/"]
COPY ["src/WashGo.Domain.Shared/WashGo.Domain.Shared.csproj", "src/WashGo.Domain.Shared/"]
COPY ["src/WashGo.EntityFrameworkCore/WashGo.EntityFrameworkCore.csproj", "src/WashGo.EntityFrameworkCore/"]
COPY ["src/Core/WashGo.Core/WashGo.Core.csproj", "src/Core/WashGo.Core/"]
COPY ["src/Core/WashGo.Core.Domain/WashGo.Core.Domain.csproj", "src/Core/WashGo.Core.Domain/"]
COPY ["src/Core/WashGo.Core.Domain.Shared/WashGo.Core.Domain.Shared.csproj", "src/Core/WashGo.Core.Domain.Shared/"]

RUN dotnet restore "WashGo.HttpApi.Host/WashGo.HttpApi.Host.csproj"

# Copy source code and build
COPY . .
RUN dotnet publish "WashGo.HttpApi.Host/WashGo.HttpApi.Host.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Development

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "WashGo.HttpApi.Host.dll"]
