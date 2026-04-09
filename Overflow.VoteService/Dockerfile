# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
# Copy solution and project files
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["Overflow.VoteService/Overflow.VoteService.csproj", "Overflow.VoteService/"]
COPY ["Overflow.Common/Overflow.Common.csproj", "Overflow.Common/"]
COPY ["Overflow.ServiceDefaults/Overflow.ServiceDefaults.csproj", "Overflow.ServiceDefaults/"]
COPY ["Overflow.Contracts/Overflow.Contracts.csproj", "Overflow.Contracts/"]
# Restore dependencies
RUN dotnet restore "Overflow.VoteService/Overflow.VoteService.csproj"
# Copy everything else
COPY . .
# Build
WORKDIR "/src/Overflow.VoteService"
RUN dotnet build "Overflow.VoteService.csproj" -c Release -o /app/build
# Publish
FROM build AS publish
RUN dotnet publish "Overflow.VoteService.csproj" -c Release -o /app/publish /p:UseAppHost=false
# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8443
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Overflow.VoteService.dll"]