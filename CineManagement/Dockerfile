# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["CineManagement.csproj", "./"]
RUN dotnet restore "CineManagement.csproj"

# Copy all source files and compile Release
COPY . .
RUN dotnet build "CineManagement.csproj" -c Release -o /app/build
RUN dotnet publish "CineManagement.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Stateless Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

# Use non-root container user
USER $APP_UID

ENTRYPOINT ["dotnet", "CineManagement.dll"]
