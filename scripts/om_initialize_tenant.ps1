param (
    # Tenant to (re)create as a child of octosystem.
    [string]$tenantId = "energyiq",
    [string]$database = "energyiq",

    # octo-cli contexts: the system context owns child tenants, the tenant
    # context is created/used for the new tenant itself.
    [string]$systemContext = "local_octosystem",
    [string]$tenantContext = "local_energyiq",

    # Build configuration of the local CK build outputs (EnergyIQ + Loxone adapter).
    [string]$configuration = "DebugL",

    # Service URLs for the tenant context (local dev defaults).
    [string]$identityServicesUri      = "https://localhost:5003/",
    [string]$assetServicesUri         = "https://localhost:5001/",
    [string]$botServicesUri           = "https://localhost:5009/",
    [string]$communicationServicesUri = "https://localhost:5015/"
)

Write-Host "=== Initializing $tenantId Tenant ===" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# Step 1: Operate from the octosystem context. Child tenants are created and
# deleted through octosystem, so we switch to it and authenticate first.
# ---------------------------------------------------------------------------
Write-Host "Switching to system context '$systemContext' and authenticating..." -ForegroundColor Yellow
octo-cli -c UseContext -n $systemContext
# --if-needed: reuse the valid token or refresh it silently; only open a browser
# when neither works, so re-running this script is not constantly interrupted.
octo-cli -c LogIn -i --if-needed

# ---------------------------------------------------------------------------
# Step 2: (Re)create the tenant as a child of octosystem. Delete is ignored if
# the tenant does not exist yet; Create provisions the current user as admin.
# ---------------------------------------------------------------------------
Write-Host "Deleting existing tenant '$tenantId' (if any)..." -ForegroundColor Yellow
octo-cli -c Delete -tid $tenantId -y

Write-Host "Creating child tenant '$tenantId'..." -ForegroundColor Yellow
octo-cli -c Create -tid $tenantId -db $database

# ---------------------------------------------------------------------------
# Step 3: Register a context for the new tenant and switch to it.
# ---------------------------------------------------------------------------
Write-Host "Registering context '$tenantContext' for tenant '$tenantId'..." -ForegroundColor Yellow
octo-cli -c AddContext -n $tenantContext `
    -isu $identityServicesUri `
    -asu $assetServicesUri `
    -bsu $botServicesUri `
    -csu $communicationServicesUri `
    -tid $tenantId
octo-cli -c UseContext -n $tenantContext

# ---------------------------------------------------------------------------
# Step 4: Authenticate against the new tenant. The fresh token carries the new
# tenant in its allowed_tenants claim, which is required to use it.
# ---------------------------------------------------------------------------
Write-Host "Authenticating against tenant '$tenantId'..." -ForegroundColor Yellow
# Same non-disruptive login on the new tenant context. On a freshly created tenant
# there is no stored token yet, so this performs a real device log-in the first time.
octo-cli -c LogIn -i --if-needed

# ---------------------------------------------------------------------------
# Step 5: Import Construction Kit (Basic + Basic.Energy from the catalog,
# EnergyIQ from the local build output).
# ---------------------------------------------------------------------------
Write-Host "Importing Construction Kit..." -ForegroundColor Yellow
& "$PSScriptRoot/om_importck.ps1" -configuration $configuration

# ---------------------------------------------------------------------------
# Step 6: Import general Runtime data (enables communication/stream data, then
# imports the energyiq archives, simulation pipelines, queries and BIM sample).
# ---------------------------------------------------------------------------
Write-Host "Importing Runtime data (EnergyIQ)..." -ForegroundColor Yellow
& "$PSScriptRoot/om_importrt.ps1"

# ---------------------------------------------------------------------------
# Step 7: Import the Loxone smart-home sample (CK model from the sibling
# octo-adapter-loxone repo + adapter / configuration / pipelines).
# ---------------------------------------------------------------------------
Write-Host "Importing Loxone sample..." -ForegroundColor Yellow
& "$PSScriptRoot/om_importrt_sample_general.ps1" -configuration $configuration

Write-Host "=== Tenant initialization complete ===" -ForegroundColor Green

# ---------------------------------------------------------------------------
# Manually created DataPointMappings are wiped by the re-initialisation, but a
# versioned backup can restore them (data/mappings/datapoint-mappings.json,
# created via scripts/om_export_mappings.ps1). The restore needs running
# services, the deployed "Mapping Backup" data flow and one Loxone browse run
# (so the controls exist again) — it cannot run inside this script.
# ---------------------------------------------------------------------------
if (Test-Path (Join-Path $PSScriptRoot "../data/mappings/datapoint-mappings.json")) {
    Write-Host ""
    Write-Host "A DataPointMapping backup exists. To restore it:" -ForegroundColor Cyan
    Write-Host "  1. Start the services and deploy the 'Mapping Backup' data flow (Studio -> Communication -> Data Flows)." -ForegroundColor Cyan
    Write-Host "  2. Run the Loxone browse pipeline once (controls must exist again)." -ForegroundColor Cyan
    Write-Host "  3. Run scripts/om_import_mappings.ps1" -ForegroundColor Cyan
}
