# Exports the tenant's manually created DataPointMappings into the versioned
# backup file data/mappings/datapoint-mappings.json.
#
# Talks to the "Export DataPoint Mappings" pipeline (data flow "Mapping
# Backup", rt-pipelines-mapping-backup.yaml) via its HTTP trigger on the mesh
# adapter. The pipeline exports natural identities (LoxoneUuid / GlobalId), so
# the file can be re-imported after om_initialize_tenant even though all RtIds
# changed. Rule-generated mappings ("ruleId|rtId|state") are excluded — they
# are reproduced by the Rules-based Auto-Map pipeline.
#
# Prerequisite: the "Mapping Backup" data flow is DEPLOYED on the mesh adapter
# (Studio → Communication → Data Flows → Deploy).
param (
    [string]$tenant = "energyiq",
    [string]$baseUrl = "https://localhost:5020",
    [string]$outFile = (Join-Path $PSScriptRoot "../data/mappings/datapoint-mappings.json")
)

$uri = "$baseUrl/$tenant/mappings/export"
Write-Host "Exporting DataPointMappings from $uri ..." -ForegroundColor Yellow

try {
    $response = Invoke-WebRequest -Uri $uri -Method Get -SkipCertificateCheck
}
catch {
    Write-Host "Export failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Is the mesh adapter running and the 'Mapping Backup' data flow deployed?" -ForegroundColor Red
    exit 1
}

$outDir = Split-Path -Parent $outFile
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

# Pretty-print so the Git diff of the backup stays reviewable.
$response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 16 | Set-Content -Path $outFile -Encoding utf8

$document = Get-Content $outFile -Raw | ConvertFrom-Json
Write-Host "Exported $($document.mappings.Count) mapping(s) to $outFile" -ForegroundColor Green
Write-Host "Commit the file to keep the backup versioned." -ForegroundColor Cyan
