# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# Copy solution and project files
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["Overflow.SearchService/Overflow.SearchService.csproj", "Overflow.SearchService/"]
COPY ["Overflow.Common/Overflow.Common.csproj", "Overflow.Common/"]
COPY ["Overflow.ServiceDefaults/Overflow.ServiceDefaults.csproj", "Overflow.ServiceDefaults/"]
COPY ["Overflow.Contracts/Overflow.Contracts.csproj", "Overflow.Contracts/"]

# Restore dependencies
RUN dotnet restore "Overflow.SearchService/Overflow.SearchService.csproj"

# Copy everything else
COPY . .

# Build
WORKDIR "/src/Overflow.SearchService"
RUN dotnet build "Overflow.SearchService.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Overflow.SearchService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Overflow.SearchService.dll"]

