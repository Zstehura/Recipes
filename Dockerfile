# ---------------------------------------------------------------------------------------
# Build stage: full .NET SDK, discarded after publishing
# ---------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore in its own layer, so packages are only re-downloaded when the project file changes
COPY RecipesApp.csproj .
RUN dotnet restore RecipesApp.csproj

COPY . .
RUN dotnet publish RecipesApp.csproj -c Release -o /app/publish --no-restore

# Make the published files readable (not writable) by everyone, whatever permissions the
# source files had on the build machine, so the non-root user can read them at runtime
RUN chmod -R u=rwX,go=rX /app/publish

# Empty folder for the database; copied into the final image owned by the app user
RUN mkdir -p /app/data

# ---------------------------------------------------------------------------------------
# Runtime stage: "chiseled" Ubuntu image containing only what .NET needs to run.
# No shell or package manager, and it runs as the non-root "app" user (UID 1654) by default.
# The "-extra" variant adds ICU and time zone data, which the app uses for cultures and
# local times.
# ---------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled-extra
WORKDIR /app

# The app's files stay owned by root, so the app user can read but not modify them
COPY --from=build /app/publish .

# The database folder is the only place the app user can write
COPY --from=build --chown=$APP_UID:$APP_UID /app/data ./Data

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Already the image default; stated explicitly so it's visible here
USER $APP_UID

ENTRYPOINT ["dotnet", "RecipesApp.dll"]
