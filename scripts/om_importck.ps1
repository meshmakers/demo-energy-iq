# Imports the Construction Kit models for the energyiq tenant.
#
# Runs against whatever tenant context is currently active (the orchestrator
# om_initialize_tenant.ps1 switches to local_energyiq and logs in first).
#
# Basic / Basic.Energy come from the LocalFileSystemCatalog with pinned versions
# (ImportFromCatalog requires an exact Name-Version; a bare name defaults to 1.0.0).
# EnergyIQ is imported from the local build output of src/EnergyIqCkModel.
#
# Paths are resolved relative to this script ($PSScriptRoot) so it runs from any
# working directory.
param (
    [string]$configuration = "DebugL"
)

$ckModel = Join-Path $PSScriptRoot "../src/EnergyIqCkModel/bin/$configuration/net10.0/octo-ck-libraries/EnergyIqCkModel/out/ck-energyiq-2.yaml"

octo-cli -c ImportFromCatalog -cn LocalFileSystemCatalog -m Basic-2.0.2 -w
octo-cli -c ImportFromCatalog -cn LocalFileSystemCatalog -m Basic.Energy-1.1.3 -w
octo-cli -c ImportCk -f $ckModel -w
