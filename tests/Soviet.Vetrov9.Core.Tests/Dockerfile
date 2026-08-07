FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["tests/Soviet.Vetrov9.Core.Tests/Soviet.Vetrov9.Core.Tests.csproj", "tests/Soviet.Vetrov9.Core.Tests/"]
RUN dotnet restore "tests/Soviet.Vetrov9.Core.Tests/Soviet.Vetrov9.Core.Tests.csproj"
COPY . .
WORKDIR "/src/tests/Soviet.Vetrov9.Core.Tests"
RUN dotnet build "./Soviet.Vetrov9.Core.Tests.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Soviet.Vetrov9.Core.Tests.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Soviet.Vetrov9.Core.Tests.dll"]
