FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/MotoCare.Api/MotoCare.Api.csproj src/MotoCare.Api/
RUN dotnet restore src/MotoCare.Api/MotoCare.Api.csproj

COPY src/MotoCare.Api/ src/MotoCare.Api/
RUN dotnet publish src/MotoCare.Api/MotoCare.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /out \
    -p:UseAppHost=false \
    -p:DebugType=None \
    -p:DebugSymbols=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build --chown=app:app /out/ ./
RUN mkdir -p /app/wwwroot/uploads /app/data-protection-keys \
    && chown -R app:app /app/wwwroot /app/data-protection-keys
USER app
EXPOSE 5112
ENTRYPOINT ["dotnet", "MotoCare.Api.dll"]

