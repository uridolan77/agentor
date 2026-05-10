# Package Notes

This package is meant to be extracted into `uridolan77/agentor`.

## Recommended workflow

1. Create a branch:
   ```powershell
   git checkout -b pr1-agentor-runtime-foundation
   ```

2. Replace the existing malformed README stub with this package.

3. Run:
   ```powershell
   dotnet restore
   dotnet build
   dotnet test
   ```

4. Ask Cursor to fix compile issues only.

## Intentional omissions

This package intentionally does not include:
- real Athanor client
- real Conexus client
- MCP
- LLM calls
- EF Core
- PostgreSQL
- background jobs
- dashboard
- memory system

The goal is to create a stable runtime kernel first.
