# syntax=docker/dockerfile:1
#
# The production image: the API, serving the built React app from wwwroot, with Node and the server that renders its
# pages (/app/ssr/server.mjs, outside wwwroot, so no static file path reaches it; see MedApp.API/ServerRendering).
# - Run on its own (docker run, CI's end-to-end tests), the API starts the page server inside this container.
# - In Kubernetes, the Helm chart runs it from this same image as a second container in the API's pod
#   (node /app/ssr/server.mjs), with none of the API's secrets, and the API forwards to it (Ssr:ServerUrl).
# Without Node the API would still work, rendering pages in the browser instead.
#
# Built from the repo root (it needs the API's projects and MedApp.Web):
#   docker build -f MedApp.API/Dockerfile -t medapp-api .
# The AppHost builds it this way when publishing (MedApp.AppHost/AppHost.cs), and so does deploy/kind/up.sh. It also
# holds the EF migrations bundle (/app/efbundle) that the Helm chart's migrations Job runs.

ARG NODE_VERSION=22
ARG DOTNET_VERSION=10.0

# 1. The React app, and the page server: dist/ (the app) and dist/ssr/ (the server, its bundled entry and a copy of
# the client build's manifest). Split into /out/wwwroot, what browsers may fetch, and /out/ssr, which they mustn't:
# the page server, and the manifests (dist/.vite), which only it reads.
FROM node:${NODE_VERSION}-bookworm-slim AS web
WORKDIR /web
COPY MedApp.Web/package.json MedApp.Web/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY MedApp.Web/ ./
# Everything left in /out/wwwroot is public: the guard at the end fails the build if the page server or the manifests
# ever end up there.
RUN npm run build \
    && mkdir /out \
    && mv dist/ssr /out/ssr \
    && rm -r dist/.vite \
    && mv dist /out/wwwroot \
    && test ! -e /out/wwwroot/ssr \
    && test ! -e /out/wwwroot/.vite \
    && test ! -e /out/wwwroot/server.mjs

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
# The EF migrations as a bundle (/app/efbundle), which the Helm chart's migrations Job runs from this same image, so
# the schema always matches the API. dotnet-ef's version follows the EF Core packages (MedApp.Infrastructure).
ARG DOTNET_EF_VERSION=10.0.15
RUN dotnet tool install --global dotnet-ef --version ${DOTNET_EF_VERSION}
RUN /root/.dotnet/tools/dotnet-ef migrations bundle --project MedApp.Infrastructure --startup-project MedApp.API \
    --configuration Release --output /app/efbundle

# 3. The runtime: ASP.NET Core, Node (just its binary, from the official image), the API, the app and the page server.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
COPY --from=web /usr/local/bin/node /usr/local/bin/node
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /out/wwwroot ./wwwroot
COPY --from=web /out/ssr ./ssr
# The image's own non-root user. The API listens on 8080; TLS ends in front of it (an ingress or proxy).
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MedApp.API.dll"]
