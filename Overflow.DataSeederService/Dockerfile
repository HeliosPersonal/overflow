# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
# Copy solution and project files
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["Overflow.DataSeederService/Overflow.DataSeederService.csproj", "Overflow.DataSeederService/"]
COPY ["Overflow.Common/Overflow.Common.csproj", "Overflow.Common/"]
COPY ["Overflow.Contracts/Overflow.Contracts.csproj", "Overflow.Contracts/"]
COPY ["Overflow.ServiceDefaults/Overflow.ServiceDefaults.csproj", "Overflow.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "Overflow.DataSeederService/Overflow.DataSeederService.csproj"

# Copy everything else
COPY . .

# Build
WORKDIR "/src/Overflow.DataSeederService"
RUN dotnet build "Overflow.DataSeederService.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Overflow.DataSeederService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app

# Copy published files
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Overflow.DataSeederService.dll"]
