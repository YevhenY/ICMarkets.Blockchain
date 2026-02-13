FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ICMarkets.Blockchain.Gateway/ICMarkets.Blockchain.Gateway.csproj", "ICMarkets.Blockchain.Gateway/"]
RUN dotnet restore "./ICMarkets.Blockchain.Gateway/ICMarkets.Blockchain.Gateway.csproj"
COPY . .
WORKDIR "/src/ICMarkets.Blockchain.Gateway"
RUN dotnet build "./ICMarkets.Blockchain.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ICMarkets.Blockchain.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ICMarkets.Blockchain.Gateway.dll"]