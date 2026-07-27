FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY src/Api/Api.csproj src/Api/
COPY src/Application/Application.csproj src/Application/
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY Tests/Api.Tests/Api.Tests.csproj Tests/Api.Tests/
COPY LoxxKingApi.sln .

RUN dotnet restore LoxxKingApi.sln

# Copy everything else and build
COPY . .
WORKDIR /src
RUN dotnet publish src/Api/Api.csproj -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Copy published files
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

# Run the app
ENTRYPOINT ["dotnet", "Api.dll"]
