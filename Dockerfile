# Multi-stage Dockerfile for Real Estate Backend (RealEstatePlatform)
# Production-ready ASP.NET Core 8.0 Web API with optimized image size
# Build: docker build -t realestate-backend:latest .
# Run: docker run -d -p 5202:5202 -e "ConnectionStrings__DefaultConnection=..." realestate-backend:latest

# ==================== BUILD STAGE ====================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

# Set working directory in build stage
WORKDIR /src

# Copy project files for dependency resolution
COPY ["WebApiShop/WebApiShop.csproj", "WebApiShop/"]
COPY ["DTOs/DTOs.csproj", "DTOs/"]
COPY ["Entities/Entities.csproj", "Entities/"]
COPY ["Repository/Repository.csproj", "Repository/"]
COPY ["Services/Services.csproj", "Services/"]

# Restore NuGet packages
RUN dotnet restore "WebApiShop/WebApiShop.csproj"

# Copy entire source code
COPY . .

# Publish application in Release mode for production
# --self-contained false: use shared .NET runtime (smaller image)
# --no-restore: skip restore since already done
RUN dotnet publish "WebApiShop/WebApiShop.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --self-contained false

# ==================== RUNTIME STAGE ====================
# Use minimal ASP.NET Core runtime image (no SDK needed)
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Container metadata labels
LABEL maintainer="Real Estate Backend Team"
LABEL version="1.0"
LABEL description="Production-ready ASP.NET Core 8.0 Web API for Real Estate Platform"

# Set working directory in runtime stage
WORKDIR /app

# Install curl for health check (lightweight alternative to full utilities)
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Create dedicated non-root user for security (principle of least privilege)
# UID 1001 is a standard non-system user ID
RUN useradd -m -u 1001 -s /bin/bash appuser && \
    chown -R appuser:appuser /app

# Copy published binaries from build stage, preserving permissions
COPY --from=builder --chown=appuser:appuser /app/publish .

# Set production environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5202
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose application port
EXPOSE 5202

# Health check configuration
# Checks every 30 seconds, timeout after 10 seconds
# Waits 40 seconds before first check (start-period)
# Retries up to 3 times before marking unhealthy
# Requires /health endpoint in application (add in Program.cs MapGet("/health", ...) or controller)
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5202/health || exit 1

# Switch to non-root user for all application processes
USER appuser

# Container entry point: run the ASP.NET Core application
ENTRYPOINT ["dotnet", "WebApiShop.dll"]

# ==================== BUILD & RUN COMMANDS ====================
# Build image with tag:
# docker build -t realestate-backend:latest -f Dockerfile .
#
# Tag for container registry (e.g., Docker Hub, Azure Container Registry):
# docker tag realestate-backend:latest myregistry.azurecr.io/realestate-backend:1.0.0
#
# Push to registry:
# docker push myregistry.azurecr.io/realestate-backend:1.0.0
#
# Run container with SQL Server connection string:
# docker run -d \
#   -p 5202:5202 \
#   -e "ConnectionStrings__DefaultConnection=Server=sql.example.com;Database=RealEstate;User Id=sa;Password=YourPassword123;" \
#   -e "ASPNETCORE_ENVIRONMENT=Production" \
#   --name realestate-api \
#   realestate-backend:latest
#
# Run with external SQL Server and logging:
# docker run -d \
#   -p 5202:5202 \
#   -e "ConnectionStrings__DefaultConnection=Server=db-host;Initial Catalog=RealEstate;User Id=sa;Password=Pass@123;" \
#   -e "ASPNETCORE_ENVIRONMENT=Production" \
#   -e "Logging__LogLevel__Default=Information" \
#   -e "Logging__LogLevel__Microsoft=Warning" \
#   --restart unless-stopped \
#   --name realestate-api \
#   realestate-backend:latest
#
# View container logs:
# docker logs -f realestate-api
#
# View health status:
# docker inspect --format='{{.State.Health.Status}}' realestate-api
#
# Stop container gracefully:
# docker stop realestate-api
#
# Remove container:
# docker rm realestate-api
#
# Run with volume mount for NLog logs:
# docker run -d \
#   -p 5202:5202 \
#   -v /logs/realestate:/app/logs \
#   -e "ConnectionStrings__DefaultConnection=..." \
#   --name realestate-api \
#   realestate-backend:latest
