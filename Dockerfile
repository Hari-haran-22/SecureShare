# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project files into their respective folders
COPY ["SecureShare.API/SecureShare.API.csproj", "SecureShare.API/"]
COPY ["SecureShare.Core/SecureShare.Core.csproj", "SecureShare.Core/"]

# Restore dependencies for the API
RUN dotnet restore "SecureShare.API/SecureShare.API.csproj"

# Copy the rest of the application code
COPY . .

# Set the working directory to the API project folder
WORKDIR "/src/SecureShare.API"

# Build and publish a release version
RUN dotnet publish "SecureShare.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Run the application
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

# Copy the published files from the build stage
COPY --from=build /app/publish .

# Define the command to start the API
ENTRYPOINT ["dotnet", "SecureShare.API.dll"]