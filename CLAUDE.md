# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

EnergyIQ is an OctoMesh Construction Kit (CK) model for intelligent building energy optimization. It defines a domain model based on ISO 16739-1:2024 (IFC 4.3) for spatial structure and VDI 3814 for building automation. Project Haystack compatibility is provided via a separate projection/mapping layer (see `docs/haystack-integration-concept.md`), not via mixin attributes on the domain types.

**Key Principle:** IFC-faithful entity modeling. Sensors, actuators, and terminal units are **separate entities** (not attributes on Space). Design requirements (Pset_*) are captured as records, distinct from runtime values held by Sensor entities.

**Version:** `EnergyIQ-2.4.0` (2.0.0 was the breaking change vs 1.x — Haystack mixins removed, Space restructured per IFC/VDI; 2.1.0 added energy-metering types; 2.2.0 introduced `PassiveBuildingElement` so `SpaceElements` no longer overlaps the device associations; 2.3.0 added `StoreyElements` so whole-floor sub-meters anchor to their `BuildingStorey`; 2.4.0 fixed the `SystemMembers` nav-name inversion — outbound `SystemMembers` is now on the `DistributionSystem` side, inbound `MemberOfSystem` on the member side).

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
| `SystemMembers` | DistributionSystem (outbound `SystemMembers`) | NamedEntity member, inbound `MemberOfSystem` (HeatPump/Pump/buffer/…) | N:N |
| `SpaceSchedules` | Schedule | Space | N:N |
| `TerminalServedBy` | TechnicalSystem | RoomTerminal | N:N |
| `SystemSpaces` (legacy) | TechnicalSystem | Space | N:N |

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
  - `HeatPump` (Luft-Wasser, reversibel, Passivkühlung über Bodenheizkreis)
  - `ThermalEnergyStorage` (500l Pufferspeicher)
  - `AirHandlingUnit` (KWL mit Wärmerückgewinnung)
  - `Pump` (Heizkreispumpe)
  - `PhotovoltaicSystem` mit 4 Strings, 2 Wechselrichtern, 15 kWh Batterie
- **4 Schedules** (Wohnen-Werktag, Wohnen-Wochenende, Schlafzimmer-Nacht, Buero-Werktag)
- **4 DistributionSystems** (Heizkreis · Kühlkreis/Passivkühlung · Lüftung · Elektrisch-PV). The Heizkreis and Kühlkreis **share** the same reversible plant (HeatPump/buffer/Pump) via N:N `SystemMembers` — the canonical example of why system membership is not the spatial `ParentChild` tree.

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
5. `om_importck.ps1` — imports `Basic-2.0.2` / `Basic.Energy-1.1.3` from the
   `LocalFileSystemCatalog` and `EnergyIQ` (`ck-energyiq-2.yaml`) from the local
   build output (`-configuration`, default `DebugL`).
6. `om_importrt.ps1` — `EnableCommunication` (auto-applies the System.Communication
   blueprint → seeds the default Cloud pool + Mesh adapter) + `EnableStreamData`,
   then imports & **activates** the three per-sensor archives
   (`6a0e…0001/0002/0003`), the simulation pipelines, the `_trees` query, the
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
