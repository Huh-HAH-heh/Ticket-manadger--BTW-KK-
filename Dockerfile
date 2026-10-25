FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Ticket Manager API/Ticket Manager API.csproj", "Ticket Manager API/"]
RUN dotnet restore "Ticket Manager API/Ticket Manager API.csproj"

COPY . .
RUN dotnet publish "Ticket Manager API/Ticket Manager API.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Ticket Manager API.dll"]
