# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish EXAMENPARCIAL.csproj -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 10000
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "EXAMENPARCIAL.dll"]
