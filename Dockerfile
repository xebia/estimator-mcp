FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (layer cache)
COPY src/EstimatorMcp.Models/EstimatorMcp.Models.csproj src/EstimatorMcp.Models/
COPY src/EstimatorMcp.Web/EstimatorMcp.Web.csproj src/EstimatorMcp.Web/
RUN dotnet restore src/EstimatorMcp.Web/EstimatorMcp.Web.csproj

# Build & publish
COPY src/EstimatorMcp.Models/ src/EstimatorMcp.Models/
COPY src/EstimatorMcp.Web/ src/EstimatorMcp.Web/
RUN dotnet publish src/EstimatorMcp.Web/EstimatorMcp.Web.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Include catalog seed data for first-startup seeding
# DbSeeder looks in data/catalogs/ relative to AppContext.BaseDirectory by default
COPY --chown=app:app \
    src/CatalogEditor/CatalogEditor/CatalogEditor/data/catalogs/ \
    ./data/catalogs/

# Create writable data dir under the app user's home (always writable)
RUN mkdir -p /home/app/data/logs && chown -R app:app /home/app/data

USER app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV DatabasePath=/home/app/data/estimator.db
ENV ESTIMATOR_LOGS_PATH=/home/app/data/logs

ENTRYPOINT ["dotnet", "EstimatorMcp.Web.dll"]
