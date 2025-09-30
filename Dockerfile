# Build stage - Force rebuild for Railway cache
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["MunicipalReportsAPI.csproj", "./"]
RUN dotnet restore "MunicipalReportsAPI.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "MunicipalReportsAPI.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MunicipalReportsAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create uploads directory
RUN mkdir -p /app/wwwroot/uploads

# Copy published files
COPY --from=publish /app/publish .

# Set environment to Production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Expose port (Railway uses PORT environment variable)
EXPOSE 8080

ENTRYPOINT ["dotnet", "MunicipalReportsAPI.dll"]