# syntax=docker/dockerfile:1
#
# The production image: the API, serving the built React app from wwwroot, with Node to run the server that renders
# its pages (wwwroot/ssr/server.mjs, which the API starts; see MedApp.API/ServerRendering). Without Node the API
# would still work, rendering pages in the browser instead.
#
# Built from the repo root (it needs the API's projects and MedApp.Web):
#   docker build -f MedApp.API/Dockerfile -t medapp-api .
# The AppHost builds it this way when publishing (MedApp.AppHost/AppHost.cs).

ARG NODE_VERSION=22
ARG DOTNET_VERSION=10.0

# 1. The React app, and the page server: dist/ (the app) and dist/ssr/ (the server and its bundled entry).
FROM node:${NODE_VERSION}-bookworm-slim AS web
WORKDIR /web
COPY MedApp.Web/package.json MedApp.Web/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY MedApp.Web/ ./
RUN npm run build

# 2. The API, published for Release. The project files come first, so the restore is cached until they change.
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS api
WORKDIR /src
COPY global.json ./
COPY MedApp.API/MedApp.API.csproj MedApp.API/
COPY MedApp.Application/MedApp.Application.csproj MedApp.Application/
COPY MedApp.Contracts/MedApp.Contracts.csproj MedApp.Contracts/
COPY MedApp.Domain/MedApp.Domain.csproj MedApp.Domain/
COPY MedApp.Infrastructure/MedApp.Infrastructure.csproj MedApp.Infrastructure/
COPY MedApp.ServiceDefaults/MedApp.ServiceDefaults.csproj MedApp.ServiceDefaults/
RUN dotnet restore MedApp.API/MedApp.API.csproj
COPY MedApp.API/ MedApp.API/
COPY MedApp.Application/ MedApp.Application/
COPY MedApp.Contracts/ MedApp.Contracts/
COPY MedApp.Domain/ MedApp.Domain/
COPY MedApp.Infrastructure/ MedApp.Infrastructure/
COPY MedApp.ServiceDefaults/ MedApp.ServiceDefaults/
RUN dotnet publish MedApp.API/MedApp.API.csproj --configuration Release --no-restore --output /app

# 3. The runtime: ASP.NET Core, Node (just its binary, from the official image), the API and the app.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
COPY --from=web /usr/local/bin/node /usr/local/bin/node
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
# The image's own non-root user. The API listens on 8080; TLS ends in front of it (an ingress or proxy).
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MedApp.API.dll"]
