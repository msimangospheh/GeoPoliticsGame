# ---------------------------------------------------------------
# Geopolitics: container recipe used by Render to build and run the app
# Stage 1 builds with the .NET 8 SDK, stage 2 runs on the small runtime image
# ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore packages first (this layer is cached until the .csproj changes)
COPY ["GeoPolitics Game.csproj", "./"]
RUN dotnet restore "GeoPolitics Game.csproj"

# Copy the rest of the source and publish a Release build
COPY . .
RUN dotnet publish "GeoPolitics Game.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The app listens on port 8080 inside the container; Render puts HTTPS in front of it
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "GeoPolitics Game.dll"]
