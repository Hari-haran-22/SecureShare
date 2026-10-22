FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY SecureShare.API/SecureShare.API.csproj SecureShare.API/packages.lock.json SecureShare.API/
COPY SecureShare.Core/SecureShare.Core.csproj SecureShare.Core/packages.lock.json SecureShare.Core/
RUN dotnet restore SecureShare.API/SecureShare.API.csproj --locked-mode
COPY SecureShare.API/ SecureShare.API/
COPY SecureShare.Core/ SecureShare.Core/
RUN dotnet publish SecureShare.API/SecureShare.API.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/SecureUploads /app/DataProtectionKeys && chown -R $APP_UID:$APP_UID /app/SecureUploads /app/DataProtectionKeys
USER $APP_UID
LABEL org.secureshare.encryption-version="2"
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=10s --start-period=60s --retries=10 CMD ["dotnet", "SecureShare.API.dll", "--check-health"]
ENTRYPOINT ["dotnet", "SecureShare.API.dll"]
