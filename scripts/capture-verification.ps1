$ErrorActionPreference = "Continue"

New-Item -ItemType Directory -Force artifacts/verification | Out-Null

dotnet --info *> artifacts/verification/dotnet-info.txt
dotnet restore *> artifacts/verification/dotnet-restore.txt
dotnet build --no-restore *> artifacts/verification/dotnet-build.txt
dotnet test --no-build *> artifacts/verification/dotnet-test.txt

dotnet test tests/Agentor.Api.Tests/Agentor.Api.Tests.csproj --no-build *> artifacts/verification/api-smoke.txt

git status *> artifacts/verification/git-status.txt
git diff --stat *> artifacts/verification/git-diff-summary.txt

Write-Host "Verification evidence written to artifacts/verification/"
