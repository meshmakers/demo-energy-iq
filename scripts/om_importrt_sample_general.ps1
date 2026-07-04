# Imports the Loxone smart-home sample on top of the EnergyIQ tenant.
#
# Migrated here from octo-adapter-loxone so the whole demo (EnergyIQ model +
# archives + Loxone sample wiring) lives in one repo. The only piece that stays
# in octo-adapter-loxone is the Loxone *edge adapter* itself (the .NET worker and
# its CK model) — its build output is still imported from that sibling repo below.
#
# Prerequisites (handled by om_initialize_tenant.ps1 when run as part of the full
# flow): the tenant exists, communication/stream-data are enabled, the EnergyIQ
# CK is imported, and the energyiq archives (6a0e...0001/0002/0003) are imported
# AND activated (the Loxone pipelines write into them). EnableCommunication /
# EnableStreamData are repeated here (idempotent) so the script is also usable
# standalone against an already-CK-imported tenant.
#
# Paths are resolved relative to this script ($PSScriptRoot) so it runs from any
# working directory.
param (
    # Build configuration of the Loxone adapter CK model in the sibling repo.
    [string]$configuration = "DebugL"
)

$dataPath = Join-Path $PSScriptRoot "../data"
# The Loxone edge adapter / CK model remains in the sibling octo-adapter-loxone repo.
$loxoneCkModel = Join-Path $PSScriptRoot "../../octo-adapter-loxone/src/AdapterEdgeLoxone.CkModel/bin/$configuration/net10.0/octo-ck-libraries/AdapterEdgeLoxone.CkModel/out/ck-loxone-4.yaml"

octo-cli -c EnableCommunication
octo-cli -c EnableStreamData

# Loxone adapter CK model (still built in octo-adapter-loxone).
octo-cli -c ImportCk -f $loxoneCkModel -w

# Loxone edge adapter + its connection / service-account / AI configuration.
octo-cli -c ImportRt -f (Join-Path $dataPath "_general/rt-adapters-loxone.yaml") -w
octo-cli -c ImportRt -f (Join-Path $dataPath "_general/rt-loxone-configuration.yaml") -w
octo-cli -c ImportRt -f (Join-Path $dataPath "_general/rt-ai-configuration.yaml") -w

# Loxone pipelines (browse/sync/poll). They write sensor stream data into the
# energyiq archives imported+activated by om_importrt.ps1. -r/Upsert so re-runs
# correct already-imported pipeline definitions.
octo-cli -c ImportRt -f (Join-Path $dataPath "_pipelines/rt-pipelines-loxone.yaml") -w -r

# Mapping backup pipelines (export/import DataPointMappings via natural keys).
# Restore a saved backup after init with scripts/om_import_mappings.ps1.
octo-cli -c ImportRt -f (Join-Path $dataPath "_pipelines/rt-pipelines-mapping-backup.yaml") -w -r
