# Local BenchmarkDotNet harness (Release). CI should keep compile-only checks; full runs are dev-only.
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

dotnet run -c Release --project benchmarks/Agentor.Benchmarks/Agentor.Benchmarks.csproj @args
