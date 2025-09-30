#!/bin/bash
set -e

echo "Running database migrations..."
dotnet ef database update --no-build

echo "Starting application..."
exec dotnet MunicipalReportsAPI.dll
