# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["Overflow.QuestionService/Overflow.QuestionService.csproj", "Overflow.QuestionService/"]
COPY ["Overflow.Common/Overflow.Common.csproj", "Overflow.Common/"]
COPY ["Overflow.ServiceDefaults/Overflow.ServiceDefaults.csproj", "Overflow.ServiceDefaults/"]
COPY ["Overflow.Contracts/Overflow.Contracts.csproj", "Overflow.Contracts/"]

# Restore dependencies
RUN dotnet restore "Overflow.QuestionService/Overflow.QuestionService.csproj"

# Copy everything else
COPY . .

# Build
WORKDIR "/src/Overflow.QuestionService"
RUN dotnet build "Overflow.QuestionService.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Overflow.QuestionService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Overflow.QuestionService.dll"]

