# FFAggerator

Select an NFL player and see cached weekly fantasy projections plus current news.

## What it does

The ASP.NET Core API pulls a player catalog and weekly projections from [Sleeper](https://docs.sleeper.com/), then matches headlines from ESPN’s NFL RSS feed and Google News. A React UI searches that cache and opens a player hub with PPR / half / standard scoring.

## Run locally

Use two terminals.

**API** (first run downloads the Sleeper player dump and can take a minute):

```powershell
dotnet run --project FFAggerator.csproj
```

The API listens on `http://localhost:5049` (see `Properties/launchSettings.json`).

**UI:**

```powershell
cd client
npm install
npm run dev
```

Open `http://localhost:5173`. Vite proxies `/api` to the backend.

## Tests

```powershell
dotnet test
```

## Data notes

- Players and projections are cached in SQLite at `Data/ffaggerator.db`.
- Players refresh daily, projections hourly, ESPN news about every 20 minutes. Opening a player page also refreshes that player’s Google News query if the cache is older than 20 minutes.
- Sleeper’s public API is unofficial and rate-limited; this app is for personal/non-commercial use.
