# =============================================================================
# BACKEND DOCKERFILE - .NET 8 Web API
# =============================================================================
# Multi-stage build for optimized production image

# =============================================================================
# BUILD STAGE
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["PFE-Back.csproj", "./"]
RUN dotnet restore "PFE-Back.csproj"

# Copy source code and build
COPY . .
RUN dotnet build "PFE-Back.csproj" -c Release -o /app/build

# =============================================================================
# PUBLISH STAGE
# =============================================================================
FROM build AS publish
RUN dotnet publish "PFE-Back.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =============================================================================
# RUNTIME STAGE
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create app user for security
RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --gid 1001 appuser

WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create uploads directory and set permissions
RUN mkdir -p /app/uploads && \
    chown -R appuser:appgroup /app && \
    chmod -R 755 /app

# Switch to non-root user
USER appuser

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Extended timeouts for OCR processing
ENV ASPNETCORE_REQUESTTIMEOUT=300
ENV ASPNETCORE_SHUTDOWNTIMEOUT=30

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Start the application
ENTRYPOINT ["dotnet", "PFE-Back.dll"]
