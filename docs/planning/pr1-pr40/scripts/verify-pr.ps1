$ErrorActionPreference = "Stop"

dotnet restore
dotnet build --no-restore
dotnet test --no-build

Write-Host "Agentor PR verification passed."
