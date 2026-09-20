FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY FromTheFarm.Api.csproj .
RUN dotnet restore FromTheFarm.Api.csproj

COPY . .
RUN dotnet publish FromTheFarm.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render assigns the listen port via $PORT at runtime; Program.cs reads it
# and binds to 0.0.0.0:$PORT. No EXPOSE/port here is authoritative for Render.
ENTRYPOINT ["dotnet", "FromTheFarm.Api.dll"]
