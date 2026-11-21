FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/TournamentServices.Domain/*.csproj src/TournamentServices.Domain/
COPY src/TournamentServices.Repositories/*.csproj src/TournamentServices.Repositories/
COPY src/TournamentServices.Delegates/*.csproj src/TournamentServices.Delegates/
COPY src/TournamentServices.Api/*.csproj src/TournamentServices.Api/
RUN dotnet restore src/TournamentServices.Api/TournamentServices.Api.csproj
COPY src/ src/
RUN dotnet publish src/TournamentServices.Api/TournamentServices.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN mkdir -p /app/data && chown app:app /app/data
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TournamentServices.Api.dll"]
