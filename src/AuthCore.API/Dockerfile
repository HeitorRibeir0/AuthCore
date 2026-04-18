FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY AuthCore.sln ./
COPY src/AuthCore.API/AuthCore.API.csproj src/AuthCore.API/
RUN dotnet restore src/AuthCore.API/AuthCore.API.csproj

COPY src/AuthCore.API/ src/AuthCore.API/
RUN dotnet publish src/AuthCore.API/AuthCore.API.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "AuthCore.API.dll"]
