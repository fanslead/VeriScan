FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY global.json Directory.Build.props VeriScan.slnx ./
COPY apps/api/Domain/Domain.csproj apps/api/Domain/
COPY apps/api/Application/Application.csproj apps/api/Application/
COPY apps/api/Infrastructure/Infrastructure.csproj apps/api/Infrastructure/
COPY apps/api/Api/Api.csproj apps/api/Api/
RUN dotnet restore apps/api/Api/Api.csproj

COPY apps/api/ apps/api/
RUN dotnet publish apps/api/Api/Api.csproj \
    --configuration Release \
    --no-restore \
    --output /out \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
RUN apk add --no-cache krb5-libs
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
COPY --from=build --chown=$APP_UID:$APP_UID /out/ ./
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1
ENTRYPOINT ["dotnet", "VeriScan.Api.dll"]
