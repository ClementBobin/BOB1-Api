# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG VERSION=0.0.0

COPY . .
RUN dotnet restore src/Bob1.Api/Bob1.Api.csproj
RUN dotnet publish src/Bob1.Api/Bob1.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:Version=${VERSION} \
    -p:InformationalVersion=${VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The database is PostgreSQL: set ConnectionStrings__Default (see docker-compose.yml / README).
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Bob1.Api.dll"]