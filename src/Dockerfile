# Image de l'API pour le palier 3. En local (paliers 0 à 2), `dotnet run`
# suffit ; pour une exposition publique, mieux vaut un conteneur, ne serait-ce
# que pour éviter de dépendre du réseau de la machine de développement.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TodoApi.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TodoApi.dll"]
