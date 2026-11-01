# Feltwell

Feltwell is an ASP.NET Core API for millinery hat blocks and studio jobs. Catalog blocks by room and head size, queue hats, and keep block status in sync as work moves from blocked to ready.

## Stack

- .NET 10
- ASP.NET Core Minimal APIs
- Swagger UI
- In-memory store with active-hat capacity checks
- xUnit + `WebApplicationFactory`

## Features

- Block catalog with room, head size, max active hats, and notes
- Status workflow for blocks: `Open` → `Busy` → `Offline`
- Hat jobs with client, material, and style
- Job workflow: `Blocked` → `Wired` → `Trimmed` → `Ready` (plus `Scrapped`)
- Active jobs cannot exceed block capacity
- Offline blocks reject new hat jobs
- Filter blocks by status, room, or search text
- Seeded demo blocks on startup in Development

## Architecture

```
Client
  │
  ▼
Minimal API endpoints
  │
  ▼
BlockService
  │
  └── InMemoryBlockStore
        ├── hat blocks
        └── hat jobs
```

## Project layout

```
feltwell/
├── Feltwell.slnx
├── LICENSE
├── README.md
├── src/Feltwell.Api/
│   ├── Program.cs
│   ├── Endpoints/
│   ├── Models/
│   ├── Repositories/
│   ├── Services/
│   └── Errors/
└── tests/Feltwell.Api.Tests/
```

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Setup

```bash
git clone https://github.com/<your-username>/feltwell.git
cd feltwell
dotnet restore
```

## Run

```bash
dotnet run --project src/Feltwell.Api
```

- API root: http://localhost:5000/
- Health: http://localhost:5000/health
- Swagger: http://localhost:5000/swagger
- Blocks: http://localhost:5000/api/v1/blocks

## API overview

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/health` | Liveness check |
| `GET` | `/api/v1/blocks` | List blocks (`status`, `room`, `q`, `limit`, `offset`) |
| `POST` | `/api/v1/blocks` | Create a block |
| `GET` | `/api/v1/blocks/{id}` | Get one block |
| `PATCH` | `/api/v1/blocks/{id}` | Update a block |
| `DELETE` | `/api/v1/blocks/{id}` | Delete a block (+ hats) |
| `GET` | `/api/v1/blocks/{id}/hats` | List hat jobs (`status`) |
| `POST` | `/api/v1/blocks/{id}/hats` | Queue a hat job |
| `GET` | `/api/v1/blocks/{id}/hats/{jobId}` | Get one hat job |
| `PATCH` | `/api/v1/blocks/{id}/hats/{jobId}` | Update job / advance status |

### Example

```bash
# Create a block
curl -X POST http://localhost:5000/api/v1/blocks \
  -H "Content-Type: application/json" \
  -d "{\"code\":\"HB-D1\",\"name\":\"Beret Block D\",\"room\":\"Front Room\",\"headSizeCm\":56,\"maxActiveHats\":2,\"notes\":\"Soft crowns\"}"

# Queue a hat
curl -X POST http://localhost:5000/api/v1/blocks/<block-id>/hats \
  -H "Content-Type: application/json" \
  -d "{\"label\":\"Harbor Cloche\",\"client\":\"Nora Ellis\",\"material\":\"Felt\",\"style\":\"Cloche\"}"

# Mark wired
curl -X PATCH http://localhost:5000/api/v1/blocks/<block-id>/hats/<job-id> \
  -H "Content-Type: application/json" \
  -d "{\"status\":\"Wired\"}"
```

## Tests

```bash
dotnet test
```

## License

MIT
