FROM mcr.microsoft.com/dotnet/sdk@sha256:9c7b6b263d9d4c2e4718e16dae6d75cd8f4b5ccf206f1e7d48a98eb429981b03 AS build
WORKDIR /src
COPY infra/sprint3/docker/global.json ./global.json

COPY src/services/AURA.Identity.Api/AURA.Identity.Api.csproj \
     src/services/AURA.Identity.Api/

RUN dotnet restore \
    src/services/AURA.Identity.Api/AURA.Identity.Api.csproj

COPY src/services/AURA.Identity.Api/ \
     src/services/AURA.Identity.Api/

RUN dotnet publish \
    src/services/AURA.Identity.Api/AURA.Identity.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet@sha256:43cbcf11fd6f88c0375c9c0f7fe59af619abeb323befe5f57dc1502b5a05d8c3 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .

USER 1654:1654

EXPOSE 8080

ENTRYPOINT ["dotnet", "AURA.Identity.Api.dll"]
