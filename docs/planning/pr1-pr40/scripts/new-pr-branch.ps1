param(
  [Parameter(Mandatory=$true)]
  [string]$PrNumber,

  [Parameter(Mandatory=$true)]
  [string]$Slug
)

$branch = "pr$PrNumber-$Slug"
git checkout -b $branch
Write-Host "Created branch $branch"
