# Deploy Supabase Edge Functions for Kaplao level storage (Timeweb S3)
# Run in PowerShell from KaplaoGamesWebsite folder:
#   .\supabase\deploy-functions.ps1

$ErrorActionPreference = "Stop"
$ProjectRef = "dmazorbjzecsfrcyxdsq"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "== Kaplao: deploy Supabase functions ==" -ForegroundColor Cyan

# Install CLI locally if missing
if (-not (Test-Path "node_modules\.bin\supabase.cmd")) {
    Write-Host "Installing supabase CLI..."
    npm install supabase --save-dev
}

$Supabase = Join-Path $Root "node_modules\.bin\supabase.cmd"
if (-not (Test-Path $Supabase)) {
    throw "supabase CLI not found after npm install"
}

# Login token (optional): set env SUPABASE_ACCESS_TOKEN or paste when prompted
if (-not $env:SUPABASE_ACCESS_TOKEN) {
    Write-Host ""
    Write-Host "Need Supabase access token." -ForegroundColor Yellow
    Write-Host "1. Open https://supabase.com/dashboard/account/tokens"
    Write-Host "2. Create token -> copy"
    Write-Host "3. Paste below (input hidden):"
    $token = Read-Host "Access token"
    if ([string]::IsNullOrWhiteSpace($token)) { throw "Token required." }
    $env:SUPABASE_ACCESS_TOKEN = $token.Trim()
    & $Supabase login --token $env:SUPABASE_ACCESS_TOKEN
} else {
    & $Supabase login --token $env:SUPABASE_ACCESS_TOKEN
}

Write-Host "Linking project $ProjectRef..."
& $Supabase link --project-ref $ProjectRef

# Load secrets from .env.local if present
$EnvLocal = Join-Path $Root ".env.local"
if (Test-Path $EnvLocal) {
    Write-Host "Loading secrets from .env.local..."
    Get-Content $EnvLocal | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]+)=(.*)$') {
            $name = $matches[1].Trim()
            $value = $matches[2].Trim().Trim('"').Trim("'")
            if ($name -match '^(GAME_SERVER_SECRET|JWT_SECRET|TIMEWEB_S3_|ACCOUNTS_)') {
                & $Supabase secrets set "${name}=${value}" | Out-Null
                Write-Host "  set $name"
            }
        }
    }
} else {
    Write-Host ""
    Write-Host ".env.local not found — set secrets manually:" -ForegroundColor Yellow
    Write-Host @"
  supabase secrets set GAME_SERVER_SECRET=...
  supabase secrets set TIMEWEB_S3_ENDPOINT=https://s3.twcstorage.ru
  supabase secrets set TIMEWEB_S3_REGION=ru-1
  supabase secrets set TIMEWEB_S3_BUCKET=...
  supabase secrets set TIMEWEB_S3_ACCESS_KEY=...
  supabase secrets set TIMEWEB_S3_SECRET_KEY=...
"@
    Read-Host "Press Enter after secrets are set in another terminal"
}

Write-Host "Deploying functions..."
& $Supabase functions deploy levels-upload-url
& $Supabase functions deploy levels-download
& $Supabase functions deploy levels-delete
& $Supabase functions deploy verify-player

Write-Host ""
Write-Host "Done. Test:" -ForegroundColor Green
Write-Host @"
`$anon = "YOUR_ANON_KEY"
`$secret = "YOUR_GAME_SERVER_SECRET"
Invoke-RestMethod -Method POST `
  -Uri "https://$ProjectRef.supabase.co/functions/v1/levels-upload-url" `
  -Headers @{ apikey=`$anon; Authorization="Bearer `$anon"; "X-Kaplao-Game-Secret"=`$secret; "Content-Type"="application/json" } `
  -Body "{}"
"@
