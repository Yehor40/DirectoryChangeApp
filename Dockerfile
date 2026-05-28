FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY DirectoryChangeApp.csproj ./
RUN dotnet restore DirectoryChangeApp.csproj
COPY . ./
RUN dotnet publish DirectoryChangeApp.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish ./
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "DirectoryChangeApp.dll"]
