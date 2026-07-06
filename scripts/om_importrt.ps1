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
# One archive per target type (13 since AB#3442: room sensors, light, shading,
# energy consumption and production — see rt-archives-energyiq.yaml).
# -r/Upsert lets us correct the Columns of an already-imported but
# not-yet-activated archive. ActivateArchive provisions each CrateDB table so it
# can accept writes (needs CrateDB reachable). The simulation and Loxone sample
# pipelines write into these archives.
octo-cli -c ImportRt -f (Join-Path $dataPath "_general/rt-archives-energyiq.yaml") -w -r
octo-cli -c ActivateArchive -id 6a0e000000000000000a0001  # TemperatureSensor
octo-cli -c ActivateArchive -id 6a0e000000000000000a0002  # HumiditySensor
octo-cli -c ActivateArchive -id 6a0e000000000000000a0003  # CO2Sensor
octo-cli -c ActivateArchive -id 6a0e000000000000000a0004  # Luminaire
octo-cli -c ActivateArchive -id 6a0e000000000000000a0005  # ShadingDevice
octo-cli -c ActivateArchive -id 6a0e000000000000000a0006  # Meter
octo-cli -c ActivateArchive -id 6a0e000000000000000a0007  # GridConnection
octo-cli -c ActivateArchive -id 6a0e000000000000000a0008  # ChargingStation
octo-cli -c ActivateArchive -id 6a0e000000000000000a0009  # Appliance
octo-cli -c ActivateArchive -id 6a0e000000000000000a000a  # PVString
octo-cli -c ActivateArchive -id 6a0e000000000000000a000b  # Inverter
octo-cli -c ActivateArchive -id 6a0e000000000000000a000c  # PhotovoltaicSystem
octo-cli -c ActivateArchive -id 6a0e000000000000000a000d  # BatteryStorage
octo-cli -c ActivateArchive -id 6a0e000000000000000a000e  # HeatPump
octo-cli -c ActivateArchive -id 6a0e000000000000000a000f  # ThermalEnergyStorage
octo-cli -c ActivateArchive -id 6a0e000000000000000a0010  # Pump
octo-cli -c ActivateArchive -id 6a0e000000000000000a0011  # Meter kWh counters

# --- Pipelines --------------------------------------------------------------
octo-cli -c ImportRt -f (Join-Path $dataPath "_pipelines/rt-simulation-adapters.yaml") -w

# --- Queries ----------------------------------------------------------------
octo-cli -c ImportRt -f (Join-Path $dataPath "_queries/_trees.yaml")

# --- UI: MeshBoards ----------------------------------------------------------
# "Raumtemperaturen" + "Energie" dashboards with their persistent queries and
# 5-minute AVG/MIN/MAX rollups (see rt-meshboards-energyiq.yaml for why the
# rollups are required by the resolution-aware line charts). -r/Upsert so
# re-runs update the boards in place. The rollups are activated below like the
# raw archives; backfill of pre-existing history is optional (BackfillRollup).
octo-cli -c ImportRt -f (Join-Path $dataPath "_general/rt-meshboards-energyiq.yaml") -w -r
octo-cli -c ActivateArchive -id 6a4a9fe67e487162fbe3860f  # TemperatureSensorRollup5m
octo-cli -c ActivateArchive -id 6a4aa4cc22664f2b26c67e65  # GridConnectionRollup5m
octo-cli -c ActivateArchive -id 6a4aa4cc22664f2b26c67e66  # PhotovoltaicSystemRollup5m
octo-cli -c ActivateArchive -id 6a4aa4cc22664f2b26c67e67  # ChargingStationRollup5m
octo-cli -c ActivateArchive -id 6a4aa4cc22664f2b26c67e68  # ApplianceRollup5m
octo-cli -c ActivateArchive -id 6a4aa4cc22664f2b26c67e69  # BatteryStorageRollup5m
octo-cli -c ActivateArchive -id 6a4b4000000000000000e001  # HeatPumpRollup5m
octo-cli -c ActivateArchive -id 6a4b4000000000000000e002  # ThermalEnergyStorageRollup5m
octo-cli -c ActivateArchive -id 6a4b4000000000000000e003  # PumpRollup5m
octo-cli -c ActivateArchive -id 6a4b4000000000000000e004  # MeterRollup5m
octo-cli -c ActivateArchive -id 6a4b4000000000000000e005  # MeterEnergyRollup5m

# --- UI: Runtime Browser tree navigation ------------------------------------
# Per-tenant tree config (System.UI/TreeNavigationConfiguration): role labels/visibility
# (AB#4262) + the switchable "Systems" perspective rooting on DistributionSystem (AB#4263).
# Requires System.UI >= 2.3.0 (auto-distributed by octo-platform-services). -r/Upsert so
# re-runs update the already-seeded singleton.
octo-cli -c ImportRt -f (Join-Path $dataPath "_general/rt-tree-navigation.yaml") -w -r

# --- BIM --------------------------------------------------------------------
octo-cli -c ImportRt -f (Join-Path $dataPath "bim/rt-firmianstrasse.yaml") -w
