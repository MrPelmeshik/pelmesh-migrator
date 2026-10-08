FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
ARG APP_UID=0
USER ${APP_UID}
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Migrator/Migrator.csproj", "Migrator/"]
RUN dotnet restore "Migrator/Migrator.csproj"
COPY . .
WORKDIR "/src/Migrator"
RUN dotnet build "./Migrator.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Migrator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Migrator.dll"]
