# 1. Build the React app
FROM node:24-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# 2. Publish the API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY Booking.sln ./
COPY src/Booking.Api/Booking.Api.csproj src/Booking.Api/
COPY tests/Booking.UnitTests/Booking.UnitTests.csproj tests/Booking.UnitTests/
COPY tests/Booking.IntegrationTests/Booking.IntegrationTests.csproj tests/Booking.IntegrationTests/
RUN dotnet restore
COPY src/ src/
RUN dotnet publish src/Booking.Api -c Release -o /app --no-restore

# 3. Slim runtime image: the API plus the built React app in wwwroot
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Npgsql loads libgssapi_krb5 when it opens a connection; without it the logs fill with load errors.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
# Program.cs listens on $PORT when it is set (Render); the image default is 8080.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Booking.Api.dll"]
