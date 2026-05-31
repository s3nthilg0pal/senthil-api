# Multi-stage Docker build for SenthilApi (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file first to leverage Docker layer caching
COPY SenthilApi.csproj ./
RUN dotnet restore SenthilApi.csproj

# Copy the remaining source and publish
COPY . .
RUN dotnet publish SenthilApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Configure Kestrel to listen on container port 8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SenthilApi.dll"]
