# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET 8 Web API for a municipal citizen reporting system that allows residents to submit reports about municipal issues (potholes, broken streetlights, graffiti, etc.). The API supports both authenticated users and anonymous reporting with JWT authentication, image uploads, and role-based access control.

## Development Commands

### Building and Running
```bash
# Build the project
dotnet build

# Run the application (development)
dotnet run

# Run with hot reload
dotnet watch run

# Run in production mode
dotnet run --environment Production
```

### Database Operations
```bash
# Add new migration
dotnet ef migrations add <MigrationName>

# Update database with latest migrations
dotnet ef database update

# Remove last migration (if not applied to database)
dotnet ef migrations remove

# Generate SQL script for migrations
dotnet ef migrations script
```

### Package Management
```bash
# Restore packages
dotnet restore

# Add package
dotnet add package <PackageName>

# Update packages
dotnet list package --outdated
```

## Architecture Overview

### Core Structure
- **Program.cs**: Application entry point with comprehensive service configuration including JWT authentication, Identity, CORS, file upload settings, and middleware pipeline
- **Data/ApplicationDbContext.cs**: EF Core context extending IdentityDbContext with seeded categories and roles (Admin, MunicipalStaff, Citizen)
- **Models/**: Core entities (Report, Category, ApplicationUser) with navigation properties and Common.cs for enums
- **Controllers/**: API endpoints for Reports, Categories, Identity (auth), Images (file upload), and Dashboard
- **Services/**: JWT token generation service with interface
- **Middleware/**: Global exception handling with environment-specific error details

### Database Schema
- Uses Entity Framework Core with SQL Server
- Identity tables for users and roles
- Reports table with foreign keys to Categories and Users (nullable for anonymous reports)
- Categories are pre-seeded with 8 municipal issue types
- Configured with performance indexes on Status, CreatedAt, and UserId

### Authentication & Authorization
- JWT Bearer token authentication
- ASP.NET Core Identity for user management
- Three roles: Admin, MunicipalStaff, Citizen
- Anonymous reporting supported (reports have nullable UserId)
- CORS configured for Vue.js frontend (ports 8080, 3000)

### File Upload System
- Images stored in wwwroot/uploads directory
- 10MB file size limit configured
- Security headers applied to uploaded files
- Static file serving with content type validation

### Key Configuration
- Connection string points to local SQL Server Express: `MOHAMMEDPC\\SQLEXPRESS`
- JWT configuration in appsettings.json (SecretKey, Issuer, Audience)
- Swagger documentation enabled with JWT authentication support
- Global exception middleware provides structured error responses

### Report Status Workflow
Reports progress through: Submitted → InReview → InProgress → Resolved → Closed

### Development Notes
- Nullable reference types are disabled in .csproj
- Uses NetTopologySuite for geographic data support
- Comprehensive error handling with environment-specific detail levels
- File uploads require absolute paths and proper security validation