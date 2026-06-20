# CLAUDE.md

Guidance for working in this repository. See `README.md` for the full product
overview and architecture diagram.

## What this is

DocIntel is a multi-tenant RAG SaaS: teams upload documents and ask
natural-language questions answered over their own content. Backend is
ASP.NET Core (.NET 8), frontend is Angular (standalone components). The AI layer
is interface-based and runs fully offline with deterministic stub clients by
default — no API keys are needed for dev or tests.

## Common commands

Backend (run from repo root):

```bash
dotnet build DocIntel.sln           # build API + tests
dotnet test DocIntel.sln            # xUnit + Moq, fully offline via stub AI clients
dotnet test --logger "console;verbosity=detailed"   # also prints the retrieval eval (Recall@k, MRR)
cd backend/src/DocIntel.Api && dotnet run --urls http://localhost:5080
                                    # in-memory DB + stub AI by default; Swagger at /swagger
```

Frontend (run from `frontend/`):

```bash
npm install
npm start                                              # ng serve on :4200
npm test -- --watch=false --browsers=ChromeHeadless   # karma/jasmine (needs Chrome; set CHROME_BIN if not auto-detected)
npm run build
```

Full stack: `docker compose up --build` (web :4200, api :5080, Postgres :5432).

## Layout

- `backend/src/DocIntel.Api/`
  - `Controllers/` — Health (`/health`), Auth, Documents, Query. Note: the health
    route is `/health`, **not** `/api/health`; everything else is under `/api`.
  - `Services/` — `AuthService`, `DocumentService` (ingestion pipeline), `RagService`
    (embed question → cosine rank → LLM answer). All work is tenant-scoped by workspace id.
  - `Repositories/` — EF Core, interface-based.
  - `Ai/` — `ILlmClient` / `IEmbeddingClient` abstractions, `StubClients` (offline,
    deterministic), `OpenAiClients` (Azure OpenAI / OpenAI), `VectorMath`, `TextChunker`.
  - `Ingestion/` — `DocumentTextExtractor`: PDF (PdfPig) and `.docx` (Open XML SDK)
    text extraction; everything else read as UTF-8 text.
  - `Auth/`, `Data/`, `Models/`, `Dtos/`, `Validation/`, `Middleware/`, `Migrations/`.
- `backend/tests/DocIntel.Tests/` — xUnit + Moq.
- `frontend/src/app/{core,auth,workspace}` — services/interceptor, auth screen, workspace UI.
- `infra/terraform/` — Azure Container Apps + PostgreSQL + ACR.
- `.github/workflows/ci.yml` — backend build/test, frontend test+build, docker build/push.

## Conventions & gotchas

- The stub embedding is exact-token-hash based. Its tokenizer splits on whitespace,
  punctuation, and intra-word separators (`-`, `_`, `/`) so compound terms like
  `auto-rollback` match `rollback`. Cosine similarity is 0 when two texts share no
  tokens — that is correct behavior, not a bug.
- Swap to a real model with config alone: set `Ai__Provider=AzureOpenAI` (or `OpenAI`)
  plus the endpoint/key/deployment env vars. Same code path, no code changes.
- Persistence is swappable: in-memory by default (`Database__Provider`), Postgres in compose/cloud.
- Tests must stay offline — never introduce a test that needs real network or API keys.
- After backend changes run `dotnet test DocIntel.sln`; after frontend changes run the
  headless karma command above. Both should be green before committing.
