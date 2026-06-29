# Imports the general (EnergyIQ) runtime entities for the energyiq tenant.
#
# Order matters: EnableCommunication auto-applies the System.Communication
# blueprint, which seeds the default Cloud pool 670000000000000000000001 and the
# managed Mesh adapter 670000000000000000000002. The rt YAMLs imported below
# reference those blueprint entities through their baked-in associations, so the
# blueprint must be in place first.
#
# Paths are resolved relative to this script ($PSScriptRoot) so it runs from any
# working directory.

$dataPath = Join-Path $PSScriptRoot "../data"

octo-cli -c EnableCommunication   # auto-applies System.Communication blueprint (seeds pool + mesh adapter)
octo-cli -c EnableStreamData
#octo-cli -c EnableReporting

#octo-cli -c ImportRt -f (Join-Path $dataPath "_general/rt-autoincrement.yaml") -w

# --- Stream-data archives ---------------------------------------------------
# One archive per sensor type since v2 (TemperatureSensor / HumiditySensor /
# CO2Sensor). -r/Upsert lets us correct the Columns of an already-imported but
# not-yet-activated archive. ActivateArchive provisions each CrateDB table so it
# can accept writes (needs CrateDB reachable). The Loxone sample pipelines
# (om_importrt_sample_general.ps1) write into these archives.
octo-cli -c ImportRt -f (Join-Path $dataPath "_general/rt-archives-energyiq.yaml") -w -r
octo-cli -c ActivateArchive -id 6a0e000000000000000a0001
octo-cli -c ActivateArchive -id 6a0e000000000000000a0002
octo-cli -c ActivateArchive -id 6a0e000000000000000a0003

# --- Pipelines --------------------------------------------------------------
octo-cli -c ImportRt -f (Join-Path $dataPath "_pipelines/rt-simulation-adapters.yaml") -w

# --- Queries ----------------------------------------------------------------
octo-cli -c ImportRt -f (Join-Path $dataPath "_queries/_trees.yaml")

# --- BIM --------------------------------------------------------------------
octo-cli -c ImportRt -f (Join-Path $dataPath "bim/rt-firmianstrasse.yaml") -w
