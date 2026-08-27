# Backend-only image (independent-container deployment).
#
# Build context must be the repository root, not WebApi/, because this needs Application/,
# Core/, and Infrastructure/ too:
#   docker build -f WebApi/Dockerfile -t stationeryms-backend .
#
# For a single combined image with the SPA baked into wwwroot instead, use the root
# Dockerfile — that remains valid but is no longer what the Jenkins pipeline builds.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, on just the project files, so dependency layers cache across source edits.
COPY Project.slnx ./
COPY Application/Application.csproj Application/
COPY Core/Core.csproj Core/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY WebApi/WebApi.csproj WebApi/
RUN dotnet restore WebApi/WebApi.csproj

COPY Application/ Application/
COPY Core/ Core/
COPY Infrastructure/ Infrastructure/
COPY WebApi/ WebApi/
RUN dotnet publish WebApi/WebApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
# ASPNETCORE_ENVIRONMENT, ConnectionStrings__DefaultConnection, and Jwt__SigningKey are
# supplied at `docker run` time — never bake secrets or a connection string into the image.

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "WebApi.dll"]
