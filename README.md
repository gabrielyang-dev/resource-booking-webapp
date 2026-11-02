# Resource booking

A web app for booking shared resources such as meeting rooms, desks, vehicles and equipment, with add-ons like catering ordered alongside a booking.

Work in progress. See `GLOSSARY.md` for the terms used.

## Commands

```bash
# Local Postgres (dev database "booking"; tests use "booking_test")
docker compose up -d --wait

# Build and test (needs the local Postgres above)
dotnet build
dotnet test

# Run the API on http://localhost:5122
dotnet run --project src/Booking.Api --launch-profile http

# Run the web app (proxies /api to the API)
cd web && npm install && npm run dev

# Run everything as one container on http://localhost:8080
docker build -t resource-booking .
docker run --rm -p 8080:8080 \
  -e "ConnectionStrings__Default=Host=host.docker.internal;Database=booking;Username=postgres;Password=postgres" \
  resource-booking
```

## Configuration

| Setting | Purpose | Default |
| --- | --- | --- |
| `ConnectionStrings__Default` | Postgres connection string for the API | local Postgres in `appsettings.json` |
| `BOOKING_TEST_CONNECTION` | Connection string for the integration-test database | `booking_test` on local Postgres |
| `PORT` | Port the API listens on | 8080 in the container |

Put local secrets in `src/Booking.Api/appsettings.Development.json` (git-ignored).

## Deploying to Render

`render.yaml` points Render at the `Dockerfile`. In the Render dashboard, set `ConnectionStrings__Default` to the Neon connection string in Npgsql format, for example `Host=<neon-host>;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require`. Render supplies `PORT`.
