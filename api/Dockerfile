FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
# curl is for the compose healthcheck (GET /health)
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# Non-root: the .NET 8 aspnet image ships a built-in "app" user (UID in $APP_UID). Port 8080 needs no root.
USER $APP_UID
ENTRYPOINT ["dotnet", "NewsCalendar.Api.dll"]
