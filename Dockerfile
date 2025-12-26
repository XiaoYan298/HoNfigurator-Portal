# HoNfigurator Management Portal Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5200

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["HoNfigurator.ManagementPortal/HoNfigurator.ManagementPortal.csproj", "HoNfigurator.ManagementPortal/"]
RUN dotnet restore "HoNfigurator.ManagementPortal/HoNfigurator.ManagementPortal.csproj"
COPY . .
WORKDIR "/src/HoNfigurator.ManagementPortal"
RUN dotnet build "HoNfigurator.ManagementPortal.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HoNfigurator.ManagementPortal.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create volume for persistent data (database)
VOLUME ["/app/data"]

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5200

ENTRYPOINT ["dotnet", "HoNfigurator.ManagementPortal.dll"]
