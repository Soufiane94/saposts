# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is reused when only source files change.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/SaPosts.Web/SaPosts.Web.csproj src/SaPosts.Web/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/SaPosts.Web/SaPosts.Web.csproj

COPY src/ src/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/SaPosts.Web/SaPosts.Web.csproj --configuration Release --no-restore --output /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Mount points for the named volumes. Created here so the volumes inherit the non-root owner.
RUN mkdir -p /data /media && chown "$APP_UID:$APP_UID" /data /media
ENV Storage__DataPath=/data

COPY --from=build /app .

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SaPosts.Web.dll"]
