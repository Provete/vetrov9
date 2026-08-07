FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Soviet.Vetrov9.Console/Soviet.Vetrov9.Console.csproj", "src/Soviet.Vetrov9.Console/"]
RUN dotnet restore "src/Soviet.Vetrov9.Console/Soviet.Vetrov9.Console.csproj"
COPY . .
WORKDIR "/src/src/Soviet.Vetrov9.Console"
RUN dotnet build "./Soviet.Vetrov9.Console.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Soviet.Vetrov9.Console.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Soviet.Vetrov9.Console.dll"]
