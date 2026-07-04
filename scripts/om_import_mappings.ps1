# Restores DataPointMappings from the versioned backup file
# data/mappings/datapoint-mappings.json (created by om_export_mappings.ps1).
#
# Talks to the "Import DataPoint Mappings" pipeline (data flow "Mapping
# Backup", rt-pipelines-mapping-backup.yaml) via its HTTP trigger on the mesh
# adapter. Endpoints are resolved RtId → identity attribute (LoxoneUuid /
# GlobalId) → unique name; the response lists everything that could not be
# resolved for manual follow-up in the Studio's Mapping Coverage page.
#
# Typical flow after om_initialize_tenant:
#   1. Start the services, deploy the "Mapping Backup" data flow
#      (Studio → Communication → Data Flows → Deploy).
#   2. Run the Loxone browse pipeline once so the controls exist again.
#   3. Run this script.
param (
    [string]$tenant = "energyiq",
    [string]$baseUrl = "https://localhost:5020",
    [string]$inFile = (Join-Path $PSScriptRoot "../data/mappings/datapoint-mappings.json")
)

if (-not (Test-Path $inFile)) {
    Write-Host "No mapping backup found at $inFile — nothing to import." -ForegroundColor Yellow
    exit 0
}

$uri = "$baseUrl/$tenant/mappings/import"
Write-Host "Importing DataPointMappings from $inFile to $uri ..." -ForegroundColor Yellow

try {
    $response = Invoke-WebRequest -Uri $uri -Method Post `
        -Body (Get-Content $inFile -Raw) `
        -ContentType "application/json" `
        -SkipCertificateCheck
}
catch {
    Write-Host "Import failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Is the mesh adapter running and the 'Mapping Backup' data flow deployed?" -ForegroundColor Red
    exit 1
}

$statistics = $response.Content | ConvertFrom-Json
Write-Host "Import finished: $($statistics.resolved) of $($statistics.total) mapping(s) resolved." -ForegroundColor Green
if ($statistics.unresolved -gt 0) {
    Write-Host "$($statistics.unresolved) mapping(s) could not be resolved:" -ForegroundColor Yellow
    foreach ($entry in $statistics.unresolvedEntries) {
        Write-Host "  - $($entry.name): $($entry.reason)" -ForegroundColor Yellow
    }
    Write-Host "Fix these manually in the Studio's Mapping Coverage page (Orphan Sources tab)." -ForegroundColor Cyan
}
