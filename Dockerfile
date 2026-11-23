# Build from the solution root so every project reference and embedded SQL file exists.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["HamzetWasl.slnx", "./"]
COPY ["src/HamzetWasl.Api/HamzetWasl.Api.csproj", "src/HamzetWasl.Api/"]
COPY ["src/HamzetWasl.Application/HamzetWasl.Application.csproj", "src/HamzetWasl.Application/"]
COPY ["src/HamzetWasl.Domain/HamzetWasl.Domain.csproj", "src/HamzetWasl.Domain/"]
COPY ["src/HamzetWasl.Infrastructure/HamzetWasl.Infrastructure.csproj", "src/HamzetWasl.Infrastructure/"]
RUN dotnet restore "src/HamzetWasl.Api/HamzetWasl.Api.csproj"
COPY src/ src/
COPY db/ db/
RUN dotnet publish "src/HamzetWasl.Api/HamzetWasl.Api.csproj" \
    -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build /app/publish .
# Existing storage and logging use local folders. Non-root writes are needed for
# demos; these folders are EPHEMERAL on Render Free, not durable storage.
RUN mkdir -p App_Data/uploads/public logs \
    && chown -R app:app /app
USER app
ENTRYPOINT ["dotnet", "HamzetWasl.Api.dll"]
