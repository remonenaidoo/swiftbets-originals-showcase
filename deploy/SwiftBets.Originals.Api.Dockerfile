# syntax=docker/dockerfile:1.7
FROM node:22-bookworm-slim AS web
WORKDIR /web
# The workspace (games + packages/slot-kit) installs together, so the whole folder is needed before npm ci.
COPY web/ ./
RUN npm ci && npm run build

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG TARGETARCH
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./
COPY .packages/ .packages/
COPY src/ src/
RUN dotnet restore src/SwiftBets.Originals.Api/SwiftBets.Originals.Api.csproj -a $TARGETARCH
RUN dotnet publish src/SwiftBets.Originals.Api/SwiftBets.Originals.Api.csproj -c Release -a $TARGETARCH --no-restore --self-contained false -o /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY --from=build /app .
COPY --from=web /web/dist/ ./wwwroot/
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SwiftBets.Originals.Api.dll"]
