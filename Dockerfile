FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/Workoutly.Api/Workoutly.Api.csproj src/Workoutly.Api/
RUN dotnet restore src/Workoutly.Api/Workoutly.Api.csproj
COPY src/Workoutly.Api/ src/Workoutly.Api/
RUN dotnet publish src/Workoutly.Api/Workoutly.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=10000
EXPOSE 10000
USER $APP_UID
ENTRYPOINT ["dotnet", "Workoutly.Api.dll"]
