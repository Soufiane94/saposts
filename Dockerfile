# syntax=docker/dockerfile:1

# Build on the host's platform, publish for the target one (amd64 or arm64).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Restore first so the package layer is reused when only source files change.
# Restoring for the target architecture keeps only its native libvips binaries.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/SaPosts.Web/SaPosts.Web.csproj src/SaPosts.Web/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/SaPosts.Web/SaPosts.Web.csproj -a $TARGETARCH

COPY src/ src/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/SaPosts.Web/SaPosts.Web.csproj -a $TARGETARCH --configuration Release --no-restore --output /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Mount points for the named volumes. Created here so the volumes inherit the non-root owner.
RUN mkdir -p /data /media && chown "$APP_UID:$APP_UID" /data /media
ENV Storage__DataPath=/data \
    Storage__MediaPath=/media

COPY --from=build /app .

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SaPosts.Web.dll"]
