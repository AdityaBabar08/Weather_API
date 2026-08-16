FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY WeatherApi/WeatherApi.csproj WeatherApi/
RUN dotnet restore WeatherApi/WeatherApi.csproj
COPY . .
RUN dotnet publish WeatherApi/WeatherApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WeatherApi.dll"]
