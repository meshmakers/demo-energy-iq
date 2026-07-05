# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

EnergyIQ is an OctoMesh Construction Kit (CK) model for intelligent building energy optimization. It defines a domain model based on ISO 16739-1:2024 (IFC 4.3) for spatial structure and VDI 3814 for building automation. Project Haystack compatibility is provided via a separate projection/mapping layer (see `docs/haystack-integration-concept.md`), not via mixin attributes on the domain types.

**Key Principle:** IFC-faithful entity modeling. Sensors, actuators, and terminal units are **separate entities** (not attributes on Space). Design requirements (Pset_*) are captured as records, distinct from runtime values held by Sensor entities.

**Version:** `EnergyIQ-2.8.0` (2.0.0 was the breaking change vs 1.x — Haystack mixins removed, Space restructured per IFC/VDI; 2.1.0 added energy-metering types; 2.2.0 introduced `PassiveBuildingElement` so `SpaceElements` no longer overlaps the device associations; 2.3.0 added `StoreyElements` so whole-floor sub-meters anchor to their `BuildingStorey`; 2.4.0 fixed the `SystemMembers` nav-name inversion — outbound `SystemMembers` is now on the `DistributionSystem` side, inbound `MemberOfSystem` on the member side; 2.5.0 lifted `SystemSpaces` from the legacy equipment level to `DistributionSystem` → `Space`/`BuildingStorey` coverage, the IFC `IfcRelServicesBuildings` analog; 2.6.0 made `PhotovoltaicSystem` a pure logical `IfcSystem` (`NamedEntity`, no longer a spatial `TreeNode`) grouping its components via outbound `SystemMembers` — the physical `PVString`/`Inverter`/`BatteryStorage` now anchor spatially where they sit (roofs → Building, WR+battery → Technikraum, PV fence → `ExternalSpace`), and the electrical `DistributionSystem` now aggregates the full electrical picture: PV + GridConnection + per-storey meters + wallboxes + appliances; `Meter` also gained a `SpaceElements → ExternalSpace` target so outdoor meters/chargers can anchor to an `ExternalSpace` — the demo's second wallbox now sits at the outdoor parking (`Zufahrt`) instead of the garage; the reversible heat pump became `HeatSource: Ground` (Sole-Wasser/Erdsonde) so the passive-cooling circuit is physically real; 2.6.0 also dropped the stale pre-2.4.0 member-side `SystemMembers` declarations (AirHandlingUnit/Boiler/Chiller/HeatPump/Pump/RoomTerminal/ThermalEnergyStorage → DistributionSystem) — the association is authored on the `DistributionSystem`/`PhotovoltaicSystem` origin side only, members navigate it inbound as `MemberOfSystem`; 2.7.0 renamed the misleading `SpaceSensors` outbound name `SensorInSpace` → `LocatedInSpace` — the old name read like it yields a sensor but the outbound navigation goes Sensor → Space, AB#4323; 2.8.0 made every association declared exactly once on its ORIGIN side — removed the reversed duplicate declarations on the target types (Space/ExternalSpace `SpaceSensors → Sensor` etc., they produced phantom outbound navigations like `Space.LocatedInSpace` that never carry data and silently empty any query column built on them) and added the missing origin-side declarations `Sensor.SpaceSensors → ExternalSpace`, `Sensor/Actuator.Equipment* → TechnicalSystem`, `DistributionSystem.SystemSpaces → Space/BuildingStorey`, `Meter.StoreyElements → BuildingStorey`).

## Build Commands

```bash
# Build the project (compiles CK YAML definitions)
dotnet build

# Build release configuration
dotnet build -c Release

# Clean and rebuild
dotnet clean && dotnet build
```

The build automatically processes YAML files in `src/EnergyIqCkModel/ConstructionKit/` via OctoMesh MSBuild tasks.

## Architecture

### Project Structure

```
src/EnergyIqCkModel/
└── ConstructionKit/          # CK model definitions
    ├── ckModel.yaml          # Model metadata (modelId: EnergyIQ-2.2.0)
    ├── types/                # Entity type definitions (~35 types)
    ├── attributes/           # Attribute definitions
    ├── enums/                # Enumeration definitions (~10 enums)
    ├── records/              # Complex value type definitions (incl. Pset_* records)
    └── associations/         # Relationship definitions (~16 associations)
data/bim/                     # RT (Runtime) model examples
└── rt-firmianstrasse.yaml    # Demo property in Salzburg (~140 entities)
```

### Type Hierarchy (EnergyIQ-2.2.0)

```
NamedEntity (Basic)
├── Tree (Basic)
│   └── Site                                # Root of spatial hierarchy
├── TreeNode (Basic)
│   ├── Building → Site                     # Space + building marker (PH4)
│   ├── BuildingStorey → Building
│   ├── Space → BuildingStorey              # Master data + Pset_* records (no sensor attrs)
│   ├── ExternalSpace → Site/Building/Storey # IfcExternalSpatialElement
│   └── TechnicalSystem (abstract)
│       ├── HeatPump                        # NEW - reversible (Heating/PassiveCooling/etc.)
│       ├── Boiler                          # classical fuel boiler
│       ├── Chiller
│       ├── AirHandlingUnit
│       ├── Pump
│       └── ThermalEnergyStorage            # NEW - buffer tank
└── NamedEntity (Basic)
    ├── BuildingElement (abstract)
    │   ├── PassiveBuildingElement (abstract)  # fabric/fixtures (target of SpaceElements)
    │   │   ├── Wall, Door, Window, ShadingDevice, Luminaire
    │   ├── Meter                           # + Appliance / ChargingStation / GridConnection
    │   ├── RoomTerminal (abstract)         # NEW - VDI 3814 Raumterminal
    │   │   ├── HydronicTerminal (abstract)
    │   │   │   ├── Radiator
    │   │   │   ├── RadiantSurface          # reversible H+C (IsReversible flag)
    │   │   │   ├── ChilledBeam
    │   │   │   └── FanCoilUnit             # 2-pipe or 4-pipe
    │   │   ├── AirTerminal                 # VAV/CAV/Diffuser
    │   │   └── ElectricHeater
    │   ├── Sensor (abstract)               # NEW - IfcSensor
    │   │   ├── TemperatureSensor
    │   │   ├── HumiditySensor
    │   │   ├── CO2Sensor
    │   │   ├── IlluminanceSensor
    │   │   ├── PresenceSensor
    │   │   ├── WindowContactSensor
    │   │   └── GenericSensor               # fallback (String CurrentValue)
    │   └── Actuator (abstract)             # NEW - IfcActuator
    │       ├── Valve                       # ValveType enum incl. Reversible/Changeover
    │       ├── Damper
    │       ├── Dimmer
    │       └── Motor
    ├── Schedule                            # NEW - M:N to Space (shared schedules)
    ├── DistributionSystem                  # NEW - IfcDistributionSystem
    └── PhotovoltaicSystem, PVString, Inverter, BatteryStorage
```

### Key Relationships

| Association | Source | Target | Multiplicity |
|---|---|---|---|
| `ParentChild` (inherited Basic) | TreeNode parent | TreeNode child | 1:N |
| `SpaceElements` | PassiveBuildingElement + Meter | Space | N:ZeroOrOne |
| `StoreyElements` | Meter (whole-floor sub-meter) | BuildingStorey | N:ZeroOrOne |
| `SpaceSensors` | Sensor | Space | N:ZeroOrOne |
| `SpaceActuators` | Actuator | Space | N:ZeroOrOne |
| `SpaceTerminals` | RoomTerminal | Space | N:ZeroOrOne |
| `EquipmentSensors` | Sensor | BuildingElement (equip/terminal) | N:ZeroOrOne |
| `EquipmentActuators` | Actuator | BuildingElement (equip/terminal) | N:ZeroOrOne |
| `TerminalActuators` | Actuator | RoomTerminal | N:ZeroOrOne |
| `SystemMembers` | DistributionSystem **or** PhotovoltaicSystem (outbound `SystemMembers`) | NamedEntity member, inbound `MemberOfSystem` (HeatPump/Pump/buffer/meters/loads; for the PV system: its PVString/Inverter/BatteryStorage) | N:N |
| `SpaceSchedules` | Schedule | Space | N:N |
| `TerminalServedBy` | TechnicalSystem | RoomTerminal | N:N |
| `SystemSpaces` | DistributionSystem (out `ServesSpaces`) | Space/BuildingStorey (in `ServedBySystem`) — IfcRelServicesBuildings | N:N |

**Note:** Name and Description are inherited from Basic/NamedEntity, not defined in EnergyIQ.

### Modeling principle (IFC/VDI consistency)

- **Space carries only master data + Pset_* design requirements + OperatingMode.** Sensor measurements live on Sensor entities, terminal control signals live on Terminal/Actuator entities.
- **Pset_* records** (`PsetSpaceThermalRequirements`, `PsetSpaceLightingRequirements`, `PsetSpaceOccupancyRequirements`) carry design/operational targets — distinct from runtime values.
- **Reversible aggregates** (e.g. heat pump with passive cooling): `HeatPump.IsReversibleAggregate = true`, `HeatPumpOperatingMode` enum, single hydraulic loop modeled by `RadiantSurface.IsReversibleTerminal = true` + a `Valve` with `ValveType = Reversible` (or a `ChangeoverHeatingCooling` valve at the plant).
- **External spaces** (terrace, garden, balcony, driveway) → `ExternalSpace` type (IFC `IfcExternalSpatialElement`).
- **Schedules** are separate entities with M:N to Space — shared across rooms.

## CK YAML Conventions

### Schema Reference
All CK YAML files must include:
```yaml
$schema: https://schemas.meshmakers.cloud/construction-kit-elements.schema.json
```

Model metadata uses:
```yaml
$schema: https://schemas.meshmakers.cloud/construction-kit-meta.schema.json
```

### Reference Syntax
- `${this}` - Current model (EnergyIQ)
- `${Basic}` - Basic package (NamedEntity, Tree, TreeNode)
- `${System}` - System model (base types from OctoMesh)

### Value Types
Valid valueType values: `String`, `Boolean`, `DateTime`, `Int`, `Double`, `StringArray`, `IntArray`, `Record`, `RecordArray`, `TimeSpan`, `Enum`, `Int64`, `DateTimeOffset`, `Binary`, `BinaryLinked`, `GeospatialPoint`

### Naming Convention
- Attribute `id` and `name` are PascalCase
- Type `typeId`, Enum `enumId`, Record `recordId` are PascalCase
- Association `id` is PascalCase, `inboundName`/`outboundName` are PascalCase

### Implementation Order
Create CK elements in this order: Enums → Records → Attributes → Associations → Types

## Sample Data (rt-firmianstrasse.yaml)

The `data/bim/rt-firmianstrasse.yaml` file demonstrates the full v2 model for a residential property:

- **Site:** Firmianstraße 31A, 5020 Salzburg, Austria
- **Buildings:** Hauptgebäude (3 storeys: EG, OG, DG) + Nebengebäude (1 storey)
- **18 Internal Spaces** + **5 ExternalSpaces** (Terrasse, Dachterrasse, Balkon, Zufahrt, Garten)
- **~50 Sensors** (Temperature, Humidity, CO2, Illuminance, Presence, WindowContact per room as appropriate)
- **~20 Room Terminals** — mostly `RadiantSurface` (Fußbodenheizung, reversible) + `AirTerminal` for KWL outlets; `Radiator` for the Nebengebäude
- **~25 Actuators** (Valves, Dampers, Motors)
- **Plant equipment** (all spatially under the `Technikraum` Space, not the Building):
  - `HeatPump` (Sole-Wasser/Erdsonde, reversibel, **echte Passivkühlung** über Bodenheizkreis — Erdreich als Kältequelle, kein Kompressor)
  - `ThermalEnergyStorage` (500l Pufferspeicher)
  - `AirHandlingUnit` (KWL mit Wärmerückgewinnung)
  - `Pump` (Heizkreispumpe)
  - `PhotovoltaicSystem` mit 4 Strings, 2 Wechselrichtern, 15 kWh Batterie
- **4 Schedules** (Wohnen-Werktag, Wohnen-Wochenende, Schlafzimmer-Nacht, Buero-Werktag)
- **4 DistributionSystems** (Heizkreis · Kühlkreis/Passivkühlung · Lüftung · Elektrisch). The Heizkreis and Kühlkreis **share** the same reversible plant (HeatPump + Pump + the `ChangeoverHeatingCooling` valve) via N:N `SystemMembers` — the canonical example of why system membership is not the spatial `ParentChild` tree. The heating buffer is a member of the Heizkreis **only** (bypassed in cooling mode); the changeover valve is the identifying member that turns the shared loop into the cooling circuit. The Elektrisch system aggregates PV + GridConnection + per-storey meters + wallboxes + appliances.

Total: ~140 RT entities.

## Tenant provisioning scripts (`scripts/`, PowerShell)

Local-dev scripts that provision the `energyiq` tenant end to end via `octo-cli`.
All paths resolve relative to `$PSScriptRoot`, so they run from any working
directory.

```powershell
# Full end-to-end: (re)create the energyiq tenant + import everything
cd scripts && pwsh om_initialize_tenant.ps1
```

`om_initialize_tenant.ps1` (modeled on `voest-app/scripts/om_initialize_tenant.ps1`):

1. Switch to the **octosystem** context (`local_octosystem`) and `LogIn -i` —
   child tenants are created/deleted through octosystem.
2. `Delete` (ignored if absent) then `Create` the `energyiq` tenant as a **child
   of octosystem** (provisions the current user as admin).
3. `AddContext` + `UseContext` for the tenant context (`local_energyiq`).
4. `LogIn -i` against the new tenant — the fresh token carries it in its
   `allowed_tenants` claim.
5. `om_importck.ps1` — imports `Basic-2.0.2` / `Basic.Energy-1.1.4` from the
   `LocalFileSystemCatalog` and `EnergyIQ` (`ck-energyiq-2.yaml`) from the local
   build output (`-configuration`, default `DebugL`).
6. `om_importrt.ps1` — `EnableCommunication` (auto-applies the System.Communication
   blueprint → seeds the default Cloud pool + Mesh adapter) + `EnableStreamData`,
   then imports & **activates** the 13 per-type stream-data archives
   (`6a0e…0001`–`…000d`, AB#3442: room sensors, light, shading, energy
   consumption + production — see `docs/developer-guide.md` "Archives (Stream
   Data)"), the simulation pipelines, the `_trees` query, the two **MeshBoards**
   (`rt-meshboards-energyiq.yaml`: "Raumtemperaturen" + "Energie" — dashboards,
   widgets, persistent queries and six 5-minute AVG/MIN/MAX rollups, which are
   activated like the raw archives; the rollups are required by the
   resolution-aware line charts because raw archives declare no grain), the
   `rt-tree-navigation.yaml` Runtime Browser config (role labels/visibility +
   the switchable **Systems** perspective on `DistributionSystem`, needs
   System.UI ≥ 2.3.0) and the `rt-firmianstrasse.yaml` BIM sample.
7. `om_importrt_sample_general.ps1` — the **Loxone smart-home sample** (migrated
   from `octo-adapter-loxone` so the whole demo is in one repo). Imports the
   Loxone CK model from the **sibling `octo-adapter-loxone` build output**
   (the adapter + CK model stay there) plus the Loxone adapter, connection/
   service-account/AI configuration and pipelines from this repo's `data/`. The
   pipelines write sensor stream data into the energyiq archives from step 6.

The Loxone sample data lives in `data/_general/rt-{adapters,loxone-configuration,ai-configuration}-loxone*.yaml`
and `data/_pipelines/rt-pipelines-loxone.yaml`. Only the Loxone **edge adapter**
(the .NET worker and `AdapterEdgeLoxone.CkModel`) remains in `octo-adapter-loxone`.

### DataPointMapping backup (survives re-initialisation)

Manually created DataPointMappings are wiped by `om_initialize_tenant.ps1`. The
**Mapping Backup** data flow (`data/_pipelines/rt-pipelines-mapping-backup.yaml`,
imported in step 7) exports/imports them via NATURAL keys — `LoxoneUuid` on the
source side, `GlobalId` + the fixed seed RtIds on the EnergyIQ target side — so
a backup taken before the re-init can be restored afterwards:

- `scripts/om_export_mappings.ps1` → `GET /energyiq/mappings/export` →
  `data/mappings/datapoint-mappings.json` (commit it — the backup is versioned).
- `scripts/om_import_mappings.ps1` → `POST /energyiq/mappings/import`; the
  response lists unresolved entries for manual follow-up in the Studio.
- **Studio:** the Data Mappings page has a Backup toolbar row (Export /
  Import…) that runs the same pipelines via the Communication Controller
  (`ExecutePipelineCommand`; the import pipeline carries a second
  `FromExecutePipelineCommand@1` trigger and the Studio sends
  `{ body: <document> }` so the same `$.body` path serves both entry points).

Only the **manual delta** is exported (`excludeNameRegex` skips the
rule-generated `ruleId|rtId|state` names — the Rules-based Auto-Map pipeline
recreates those with the new RtIds). Both endpoints require the deployed
"Mapping Backup" data flow and, for import, a prior Loxone browse run so the
controls exist again. See `data/mappings/README.md`. The nodes
(`ExportDataPointMappings@1` / `ImportDataPointMappings@1`) live in
`octo-mesh-adapter`.

## Documentation

Detailed specifications are in `docs/`:
- `developer-guide.md` - English developer guide explaining design philosophy and standards integration
- `construction-kit.md` - Complete CK specification with all types, attributes, and associations
- `standards-reference.md` - IFC 4.3 and VDI 3814 mapping reference
- `haystack-integration-concept.md` - Architecture decision: Haystack as projection (not parallel CK)
- `haystack-adapter-concept.md` - Haystack REST API server concept (consumes the projection)
- `space-restructuring-concept.md` - Architecture decision: full IFC/VDI restructure (this refactoring)
- `implementation-plan.md` - Phased implementation roadmap

**IMPORTANT:** After any structural changes to the CK model (types, attributes, associations, inheritance), update `docs/developer-guide.md` to reflect the changes. Keep the documentation in sync with the actual implementation.
