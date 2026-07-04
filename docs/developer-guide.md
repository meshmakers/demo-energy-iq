# EnergyIQ Construction Kit - Developer Guide

**Version:** EnergyIQ-2.6.0
**Standards:** ISO 16739-1:2024 (IFC 4.3), ISO 4157, VDI 3814

## Quick Start

### Prerequisites

1. **OctoMesh Platform** - Install locally following:
   [OctoMesh Getting Started](https://docs.meshmakers.cloud/docs/technologyGuide/gettingStartedLocally/prerequisites)

2. **OctoMesh CLI** (`octo-cli`) - Installed with the platform

3. **.NET SDK 10.0** - For building the Construction Kit

### Setup (5 Steps)

```powershell
# 1. Build the project
cd demo-energy-iq
dotnet build -c Release

# 2. Configure and login to OctoMesh
cd scripts
./om_login_local.ps1

# 3. Create tenant
./om_create_tenants.ps1

# 4. Import Construction Kits (Basic + EnergyIQ-2.0.0)
./om_importck.ps1

# 5. Import Runtime data (archives, adapters, pipelines, demo building)
./om_importrt.ps1
```

After setup, the simulation automatically generates data every 10 seconds, writing values onto the Sensor entities of the Firmianstrasse demo building. The three per-sensor archives (Temperature, Humidity, CO2) collect time-series data in CrateDB.

**Access GraphQL API:** `https://localhost:5001/graphql` (Tenant: `energyiq`)

---

## Introduction

EnergyIQ is an OctoMesh Construction Kit (CK) model library for intelligent building energy optimization. It provides a standards-compliant domain model combining:

- **ISO 16739-1:2024 (IFC 4.3)** - Industry Foundation Classes for spatial structure, equipment, sensors, actuators and property sets (Pset_*)
- **ISO 4157** - Construction drawings designation systems (storeys, rooms)
- **VDI 3814** - German building automation standard (Anlagenautomation / Raumautomation split)

Project Haystack compatibility is provided through a **separate projection/mapping layer** (see `docs/haystack-integration-concept.md`), not via mixin attributes on the domain types.

## Design Philosophy (v2)

### IFC-faithful entity modeling

EnergyIQ 1.x kept measurements as inline attributes on Space (`Space.Temperature`, `Space.HeatingValvePosition`, …). v2 follows IFC 4.3 and VDI 3814 strictly:

- **Measurements** live on dedicated `Sensor` entities (`TemperatureSensor`, `HumiditySensor`, `CO2Sensor`, `IlluminanceSensor`, `PresenceSensor`, `WindowContactSensor`, `GenericSensor`) linked to Space via the `SpaceSensors` association.
- **Control signals** live on `Actuator` entities (`Valve`, `Damper`, `Dimmer`, `Motor`) linked to terminals via `TerminalActuators` or to equipment via `EquipmentActuators`.
- **Terminal units** (heating/cooling/ventilation distribution at room level) are first-class entities under `RoomTerminal` — `Radiator`, `RadiantSurface`, `ChilledBeam`, `FanCoilUnit`, `AirTerminal`, `ElectricHeater`. Each terminal is linked to its space via `SpaceTerminals` and to its plant source via `TerminalServedBy`.
- **Design / operational requirements** (target temperature ranges, illuminance targets, peak occupancy) are captured in IFC-style **Pset_* records** on Space (`PsetSpaceThermalRequirements`, `PsetSpaceLightingRequirements`, `PsetSpaceOccupancyRequirements`) — clearly separated from runtime measurements.

This trades a higher entity count (a typical room has ~5-8 entities: Space + sensors + terminal + valve + damper) for a model that:
- Maps 1:1 to IFC 4.3 entity types
- Cleanly answers "does this room have heating?" via the existence of a terminal
- Supports reversible aggregates (heat pump with passive cooling) without forcing separate heating/cooling representations
- Eliminates the "attribute is optional but missing" ambiguity (does the room have no sensor, or is the sensor offline?)

### Reversible aggregates

A heat pump with passive cooling (same hydraulic loop, mode switched via changeover valve) is modeled as:

- `HeatPump` with `IsReversibleAggregate: true` and a `HeatPumpOperatingMode` enum (`Heating | ActiveCooling | PassiveCooling | Defrost | Standby | …`)
- A `Valve` actuator with `ValveType: ChangeoverHeatingCooling` linked via `EquipmentActuators` to the heat pump
- `RadiantSurface` terminals with `IsReversibleTerminal: true` — one terminal serves the room in both modes
- Per-terminal `Valve` with `ValveType: Reversible` for flow control

This is the Firmianstrasse demo setup.

### Standards Integration

```
                    BIM / Planning                    Operations / IoT
                         │                                │
              ┌──────────┴──────────┐         ┌──────────┴──────────┐
              │                     │         │                     │
         ISO 16739              VDI 3814              Project
           (IFC 4.3)              (GA)               Haystack
              │                     │                    │
         "Structure"           "Functions"          "Semantics"
         Spaces, Equip,         Anlagen vs.         How does
         Sensors, Actuators,    Raum-Automation     external tooling
         Pset_*                 Sensor/Actor split  read this?
              │                     │                    │
              └──────────┬──────────┴────────────────────┘
                         │
                         ▼
                    ┌─────────┐         Projection (not mixin)
                    │EnergyIQ │ ────────────► PH4-Lib / Renderer
                    │ v2.0.0  │
                    └─────────┘
```

## Type Hierarchy (v2)

### Base Types from OctoMesh Basic Package

EnergyIQ leverages the OctoMesh Basic package for standardized tree structures. This aligns with IFC's `IfcRelAggregates` for spatial decomposition.

```
NamedEntity (Basic)                       # Provides: Name, Description
│
├── Tree (Basic)
│   └── Site                              # ← IfcSite (root of spatial hierarchy)
│
├── TreeNode (Basic)                      # Inherits ParentChild association
│   ├── Building                          # ← IfcBuilding
│   ├── BuildingStorey                    # ← IfcBuildingStorey
│   ├── Space                             # ← IfcSpace (no sensor attrs)
│   ├── ExternalSpace                     # ← IfcExternalSpatialElement
│   └── TechnicalSystem (abstract)
│       ├── HeatPump                      # NEW v2 - reversible aggregate
│       ├── Boiler, Chiller, Pump
│       ├── AirHandlingUnit
│       └── ThermalEnergyStorage          # NEW v2 - buffer tank
│
└── NamedEntity (Basic)
    ├── BuildingElement (abstract)
    │   ├── PassiveBuildingElement (abstract)   # fabric/fixtures, target of SpaceElements
    │   │   ├── Wall, Door, Window
    │   │   ├── ShadingDevice, Luminaire
    │   ├── Meter                         # + Appliance / ChargingStation / GridConnection
    │   ├── RoomTerminal (abstract)       # NEW v2 - VDI 3814 Raumterminal
    │   │   ├── HydronicTerminal (abstract)
    │   │   │   ├── Radiator
    │   │   │   ├── RadiantSurface        # reversible H+C surface
    │   │   │   ├── ChilledBeam
    │   │   │   └── FanCoilUnit
    │   │   ├── AirTerminal
    │   │   └── ElectricHeater
    │   ├── Sensor (abstract)             # NEW v2 - IfcSensor
    │   │   ├── TemperatureSensor
    │   │   ├── HumiditySensor
    │   │   ├── CO2Sensor
    │   │   ├── IlluminanceSensor
    │   │   ├── PresenceSensor
    │   │   ├── WindowContactSensor
    │   │   └── GenericSensor
    │   └── Actuator (abstract)           # NEW v2 - IfcActuator
    │       ├── Valve
    │       ├── Damper
    │       ├── Dimmer
    │       └── Motor
    ├── Schedule                          # NEW v2 - M:N to Space
    ├── DistributionSystem                # NEW v2 - IfcDistributionSystem
    └── PhotovoltaicSystem, PVString,
        Inverter, BatteryStorage
```

### Spatial Structure (IFC 4.3)

The spatial hierarchy uses the **ParentChild** association inherited from `Basic/TreeNode`, mapping directly to IFC's `IfcRelAggregates`:

```
Site (Tree)                 ← IfcSite
  └── Building (TreeNode)   ← IfcBuilding              (ParentChild → Site)
        └── BuildingStorey  ← IfcBuildingStorey        (ParentChild → Building)
              ├── Space     ← IfcSpace                 (ParentChild → BuildingStorey)
              └── ExternalSpace ← IfcExternalSpatialElement
```

External spaces (terraces, gardens, balconies, driveways) use the separate `ExternalSpace` type to avoid carrying the indoor Pset_* records that don't apply.

### ISO 4157 Room Designation

EnergyIQ implements ISO 4157 for standardized room and storey designation (unchanged from v1).

#### ISO 4157-1: Storey Numbering
| Attribute | Description | Example |
|-----------|-------------|---------|
| `StoreyNumber` | Consecutive number from bottom, starting with 1 | 1, 2, 3 |
| `FloorDesignation` | National floor code (German convention) | EG, 1.OG, DG |

#### ISO 4157-2: Room Numbers (Daily Use)
| Attribute | Description | Example |
|-----------|-------------|---------|
| `RoomNumber` | Floor prefix + 2-digit sequential number | EG01, 1OG02, DG03 |

Format: `{FloorPrefix}{2-digit number}` — EG: `EG01`, 1.OG: `1OG01`, DG: `DG01`, Nebengebäude: `NG-EG01`, outdoor (`ExternalSpace`): `A01`.

#### ISO 4157-3: Room Identifiers (Lifecycle)
| Attribute | Description | Example |
|-----------|-------------|---------|
| `RoomIdentifier` | Immutable identifier: I# + storey + 3-digit seq | I#1001, I#2015 |

**Important:** Room identifiers are **immutable** throughout the building lifecycle and should never change, even during remodeling.

## Space: master data only (v2)

The `Space` type in v2 carries only IFC-style master data and design Pset_* records. Sensor values and control signals live on dedicated entities reached via associations.

### Master Data
```yaml
SpaceType: LivingRoom        # extended enum incl. residential types
PredefinedType: ""           # IFC USERDEFINED slot (optional free text)
NetFloorArea: 45.0           # m²
GrossFloorArea: 48.0         # m²
CeilingHeight: 2.6           # m
RoomNumber: "EG01"
RoomIdentifier: "I#1001"
OperatingMode: Comfort       # VDI 3814 - room-level mode IS valid here
```

### Design Requirements (Pset_* records)

Three IFC-style property sets capture design/operational targets, clearly separated from runtime values:

```yaml
ThermalRequirements:
  SpaceTemperature: 21.0         # design target (°C)
  SpaceTemperatureMin: 21.0      # heating limit (°C)
  SpaceTemperatureMax: 25.0      # cooling limit (°C)
  CO2SetpointMax: 1000.0         # max acceptable (ppm)
  SpaceHumidityMin: 35.0
  SpaceHumidityMax: 65.0

LightingRequirements:
  IlluminanceTarget: 300.0       # design lux
  IlluminanceMin: 200.0
  ArtificialLighting: true
  NaturalLighting: true

OccupancyRequirements:
  OccupancyType: living          # free text (living/sleeping/office/transient)
  OccupancyNumberPeak: 6
  AreaPerOccupant: 7.5
  OccupancyTimePerDay: 14.0
```

### Associations Reaching Sensors / Actuators / Terminals

```yaml
SpaceSensors:    [TemperatureSensor, HumiditySensor, CO2Sensor, ...]
SpaceActuators:  [Valve, Damper, ...]               # room-level (rare)
SpaceTerminals:  [RadiantSurface, AirTerminal, ...] # heating/cooling/ventilation
SpaceElements:   [PassiveBuildingElement (Wall, Window, Door, ShadingDevice, Luminaire), Meter (+ Appliance/ChargingStation/GridConnection)]
SpaceSchedules:  [Schedule (M:N — shared across rooms)]
SystemSpaces:    [DistributionSystem]                # IfcRelServicesBuildings: served Space/Storey
```

## Sensor entities (v2)

All sensors share the abstract `Sensor` base (extends `BuildingElement`) and carry a typed `CurrentValue` attribute. The underlying global attribute IDs (`Temperature`, `Humidity`, `CO2Level`, `Illuminance`, `PresenceDetected`, `ContactState`) are re-exposed under the unified name `CurrentValue` per subtype:

| Sensor Subtype | CurrentValue type | Unit | IFC mapping |
|---|---|---|---|
| `TemperatureSensor` | Double | °C | IfcSensor/TEMPERATURESENSOR |
| `HumiditySensor` | Double | %RH | IfcSensor/HUMIDITYSENSOR |
| `CO2Sensor` | Double | ppm | IfcSensor/CO2SENSOR |
| `IlluminanceSensor` | Double | lux | IfcSensor/LIGHTSENSOR |
| `PresenceSensor` | Boolean | — | IfcSensor/MOVEMENTSENSOR |
| `WindowContactSensor` | Boolean | true=closed | IfcSensor/CONTACTSENSOR |
| `GenericSensor` | String | (carrier) | IfcSensor (USERDEFINED) |

Shared sensor attributes: `Manufacturer`, `Model`, `SerialNumber`, `Accuracy`, `LastUpdate`, `IsFaulty`.

A sensor is located either in a space (`SpaceSensors`) or attached to an equipment (`EquipmentSensors` — e.g. a vorlauftemp sensor on the HeatPump).

## Actuator entities (v2)

All actuators share the abstract `Actuator` base. Position/setpoint pairs follow the same naming convention.

| Actuator | Key attributes |
|---|---|
| `Valve` | `ValveType` (enum: Heating, Cooling, Reversible, ChangeoverHeatingCooling, Mixing, Bypass, Isolation), `Position`, `PositionSetpoint` |
| `Damper` | `Position`, `PositionSetpoint`, `AirflowRate`, `AirflowSetpoint` |
| `Dimmer` | `Level`, `LevelSetpoint` (standalone — for in-luminaire dimming use `Luminaire.DimmingLevel` directly) |
| `Motor` | `State`, `Speed`, `SpeedSetpoint`, `PowerConsumption` |

## Room Terminals (v2 - VDI 3814 Raumautomation)

Terminal units sit between plant equipment and the room. They are the "last meter" of the H/K/L distribution.

```
RoomTerminal (abstract)
├── HydronicTerminal (abstract, water-based)
│   ├── Radiator                — single heating valve
│   ├── RadiantSurface          — floor/ceiling/wall; IsReversibleTerminal for H+C
│   ├── ChilledBeam             — typically cooling, optionally H+C
│   └── FanCoilUnit             — forced air + 2-pipe or 4-pipe water
├── AirTerminal                 — VAV/CAV/Diffuser
└── ElectricHeater              — convector, IR panel
```

Shared attributes: `OperatingMode` (TerminalOperatingMode enum), `NominalPower`, `SupplyTemp`, `ReturnTemp`, `FlowRate`, `IsReversibleTerminal`.

Each terminal is normally linked to:
- One Space via `SpaceTerminals`
- One or more `Actuator`s (the valve/damper) via `TerminalActuators`
- One plant equipment via `TerminalServedBy` (e.g. `HeatPump` → many `RadiantSurface`)
- Optionally a `DistributionSystem` via the inbound `MemberOfSystem` navigation
  (the `SystemMembers` association is authored on the `DistributionSystem` side;
  member types declare nothing themselves)

## Plant Equipment (v2 - VDI 3814 Anlagenautomation)

| Type | IFC Mapping | Use case |
|------|-------------|----------|
| `HeatPump` *(NEW)* | IfcUnitaryEquipment/HEATPUMP | Heating, active cooling, passive cooling — `HeatPumpOperatingMode`, `HeatSource`, COP/SCOP/EER/SEER |
| `Boiler` | IfcBoiler | Classical fuel boiler (gas/oil/pellet) |
| `Chiller` | IfcChiller | Dedicated cooling-only chiller |
| `ThermalEnergyStorage` *(NEW)* | IfcTank/THERMALTANK | Buffer tank with stratification |
| `AirHandlingUnit` | IfcUnitaryEquipment | Central ventilation with heat recovery |
| `Pump` | IfcPump | Circulation pump |

Plant equipment carries its own `SupplyTemp`/`ReturnTemp`/`ModulationLevel` etc. directly as attributes (one main reading per equipment), and additionally supports attached `EquipmentSensors` / `EquipmentActuators` for richer modeling (e.g. dedicated supply/return temp sensors, changeover valves).

`SystemSpaces` (2.5.0) is the IFC `IfcRelServicesBuildings` analog at the **system** level: a `DistributionSystem` (heating / passive-cooling / ventilation circuit) declares the `Space`/`BuildingStorey` it serves via outbound `ServesSpaces`; a storey/space sees the systems serving it via inbound `ServedBySystem`. (It was lifted in 2.5.0 from a legacy equipment-level plant→Space assignment.) For fine-grained "this terminal is served by this plant" use `TerminalServedBy`.

## Schedules (v2)

`Schedule` is now a separate entity with M:N to Space — a single "Wohnen-Werktag" schedule can drive many rooms.

```yaml
Schedule:
  Name: Wohnen-Werktag
  ScheduleType: Occupancy        # Occupancy / Heating / Cooling / Ventilation / Lighting / Shading / Custom
  IsActive: true
  Entries:
    - DaysOfWeek: '[0, 1, 2, 3, 4]'   # Mon-Fri (Bracket-string format)
      StartTime: "06:00"
      EndTime: "22:00"
      Mode: 0                          # Comfort
```

Note the schedule entries' `DaysOfWeek` field uses the bracket-string array encoding for runtime model imports (`value: '[0, 1, 2, 3, 4]'`).

## DistributionSystems (v2)

Logical grouping per IFC `IfcDistributionSystem`. Carries optional energy aggregates.

```yaml
DistributionSystem:
  Name: "Heizkreis Hauptgebäude"
  SystemType: 0                    # Heating | Cooling | Ventilation | Electrical | Sanitary | DomesticHotWater | Solar | …
  TotalEnergyConsumed: 12450.0     # kWh (calculated/aggregated)
  TotalEnergyDelivered: 47310.0
  SystemEfficiency: 3.8            # SCOP for heating, COP for cooling, %_recovery for HRV
```

Members are linked via `SystemMembers` (N:N to `NamedEntity` — both TechnicalSystems and BuildingElements/Terminals can be members).

## Building Elements (lightly changed)

| Type | IFC Mapping | Key Attributes | New v2 associations |
|------|-------------|----------------|---------------------|
| `Wall` | IfcWall | — | — |
| `Door` | IfcDoor | overallHeight/Width, isOpen, isLocked, isExternal | `EquipmentSensors` (contact sensor) |
| `Window` | IfcWindow | overallHeight/Width, isOpen, openingPosition | `EquipmentSensors` (contact sensor) |
| `ShadingDevice` | IfcShadingDevice | shadingType, position, slatAngle, setpoints | `EquipmentActuators` (motor) |
| `Luminaire` | IfcLightFixture | luminaireType, ratedPower, isOn, dimmingLevel | `EquipmentActuators` (dimmer) |

Luminaires/ShadingDevices keep their internal state attributes (`DimmingLevel`, `Position`) — they ARE the building element, the actuator is internal. For modeling explicit separate dimmer/motor actuators (e.g. DALI ballast as its own device), use `EquipmentActuators` to attach an `Actuator` entity.

## Renewable Energy Systems (v2.6.0 — PV is a logical system)

`PhotovoltaicSystem` is a **logical `IfcSystem`**, not a spatial node. It derives from `NamedEntity` (not `TreeNode`) and lives only in the Systems view. It groups its physical components via the **outbound `SystemMembers`** association (reusing the same role every logical system uses), and is itself a member of the electrical `DistributionSystem`:

```
DistributionSystem "Elektrisch"      (Systems view root)
└── PhotovoltaicSystem   (SystemMembers)          ← logical IfcSystem, aggregate values
    ├── PVString          — RatedPowerKWp, Orientation, Tilt, ModuleCount, CurrentPower
    ├── Inverter          — RatedPowerKVA, DcPower, AcPower, Efficiency
    └── BatteryStorage    — RatedCapacityKWh, StateOfCharge, ChargingPower, CycleCount
```

The physical components stay `TreeNode`s and are anchored in the **spatial** tree via `ParentChild` where they actually sit — PV strings on the roof of their `Building` (or an `ExternalSpace` for a free-standing PV fence), inverters and battery in the `Technikraum` `Space`. This mirrors the plant-equipment convention (HeatPump/AHU under Technikraum) and IFC's split: components are *contained in spatial structure*, the system is a *logical group*. Before 2.6.0 the whole PV subtree hung under `Building` in the spatial view — including strings that physically sit on the Nebengebäude and the garden fence; 2.6.0 removes that misattribution.

`PhotovoltaicSystem` aggregates: `TotalRatedPowerKWp`, `TotalCurrentPowerKW`, `TotalEnergyProducedKWh`, `GridFeedIn`, `SelfConsumption`.

## Energy Metering (NEW in v2.1.0)

Residential energy monitoring uses a `Meter`-rooted hierarchy that complements the renewable-energy aggregates above. `Meter` is concrete and serves directly for generic sub-meters (e.g. per-floor electricity sub-metering fed via Loxone); three specialised subtypes carry shape-specific attributes for grid coupling, EV charging, and individually metered appliances.

```
Meter (BuildingElement → NamedEntity)                ← concrete; use for per-floor sub-meters
├── GridConnection      — Direction (Import/Export/Bidirectional), IsMainConnection,
│                         optional FormalMeteringPoint → Basic.Energy/MeteringPoint
├── ChargingStation     — ConnectorType, MaxChargingPower, CurrentSessionEnergy, SessionActive
└── Appliance           — Category, RatedPower (kW), Manufacturer, ModelName
```

Common readings inherited from `Meter`:
- `ImportedEnergy`, `ExportedEnergy` (kWh, cumulative)
- `ActivePower`, `ApparentPower`, `ReactivePower` (kW/kVA/kVAR)
- `Voltage`, `Ampere`, `Frequency`
- `MeterIdentifier` (local ID — e.g. Loxone control name) and optional `MeteringPointReference` (formal Zählpunktnummer)

**Catalog anchor:** `Basic.Energy-1.0.1` is imported as a dependency. `GridConnection` carries an optional `FormalMeteringPoint` association to `Basic.Energy/MeteringPoint` so a grid-coupled meter can be linked to the formal energy-industry entity (Austrian/German EDA Zählpunktnummer + State + CarrierType) without forcing that semantic on every sub-meter.

**Spatial wiring:** Room-scoped meters use `SpaceElements` (declared on `Meter`, inherited by Appliance/ChargingStation/GridConnection) to associate with the Space they physically sit in; `Space` declares `SpaceElements` against both `PassiveBuildingElement` and `Meter`. The `GridConnection` typically has no Space association (it lives at the building boundary). **Whole-floor sub-meters** (a Loxone "Verbrauch EG/OG/DG" measuring an entire storey) instead use `StoreyElements` (EnergyIQ-2.3.0) to anchor directly to their `BuildingStorey` — the floor is the meter's real scope, so anchoring it to one representative room would misattribute the consumption. `StoreyElements` is the storey-level counterpart of `SpaceElements` (IFC `IfcRelContainedInSpatialStructure` at the `IfcBuildingStorey` level).

**Enums introduced for metering:**
- `MeterCarrierType` (Electricity, Gas, Heat, Water, DistrictHeating, DistrictCooling, Other)
- `GridConnectionDirection` (Import, Export, Bidirectional)
- `ChargingConnectorType` (Schuko, Type1, Type2, CCS, CHAdeMO, Tesla, Other)
- `ApplianceCategory` (LaundryAndCleaning, KitchenMajor, …, Office, Other)

The Firmianstraße demo seed (`data/bim/rt-firmianstrasse.yaml`) ships a representative metering layout in v2.1.0: 1× `GridConnection` (bidirektional, with example Zählpunktnummer), 4× per-storey `Meter` (HG-EG/1.OG/DG + NG-EG), 2× `ChargingStation` Type 2, 11 kW — one in the Garage (`SpaceElements` → Garage `Space`), one at the outdoor parking on the driveway (`SpaceElements` → `Zufahrt` `ExternalSpace`; the `Meter` type declares `SpaceElements` against `ExternalSpace` too since 2.6.0), and 2× `Appliance` in the Waschküche (Miele washer + heat-pump dryer).

## Haystack Compatibility

**v2 removes the Haystack mixin attributes** (`HaystackTags`, `HaystackRefs`, `HaystackMeta`) that were previously on every domain type. Project Haystack 4 compatibility is now provided through a separate projection layer documented in `docs/haystack-integration-concept.md`. The rationale:

- PH4's spec system is semantically isomorphic to CK (lib ≈ model, spec ≈ type, marker tag ≈ attribute, etc.) — modeling it as a parallel CK would be a metamodel-in-metamodel duplication.
- The structural impedance (Haystack `Point` is a first-class entity, EnergyIQ measurement was a Space attribute) was the main reason for the mixin approach in v1. v2 already represents measurements as separate Sensor entities — the impedance is gone, and a 1 Sensor ≈ 1 PH Point projection becomes natural.

A declarative mapping config (Phase 1 of the haystack-integration-concept) is the next step. Until that lands, EnergyIQ exports no Haystack-specific representation.

## Enumerations

### SpaceType (extended in v2)
Commercial: `Office`, `MeetingRoom`, `Corridor`, `Toilet`, `Kitchen`, `TechnicalRoom`, `Storage`, `Parking`, `Lobby`, `Staircase`, `Elevator`, `ServerRoom`, `Laboratory`, `Workshop`, `Other`.
Residential (NEW): `LivingRoom`, `Bedroom`, `Bathroom`, `DiningRoom`, `Lounge`, `ChildrensRoom`, `GuestRoom`, `Laundry`, `Garage`, `WalkInCloset`, `Anteroom`.

Use `PredefinedType` (String, optional) to override or supplement the enum with IFC-style USERDEFINED free text.

### OperatingMode (VDI 3814, unchanged)
`Comfort`, `Economy`, `Standby`, `Protection`, `Off`, `Auto`.

### HeatPumpOperatingMode *(NEW)*
`Off`, `Standby`, `Heating`, `ActiveCooling`, `PassiveCooling`, `DomesticHotWater`, `Defrost`, `Fault`.

### HeatSource *(NEW)*
`Air`, `Ground`, `Water`, `Exhaust`, `Hybrid`.

### ValveType *(NEW)*
`Heating`, `Cooling`, `Reversible`, `ChangeoverHeatingCooling`, `Mixing`, `Bypass`, `Isolation`.

### TerminalOperatingMode *(NEW)*
`Off`, `Heating`, `Cooling`, `Ventilating`, `Standby`.

### ScheduleType *(NEW)*
`Occupancy`, `Heating`, `Cooling`, `Ventilation`, `Lighting`, `Shading`, `Custom`.

### DistributionSystemType *(NEW)*
`Heating`, `Cooling`, `Ventilation`, `Electrical`, `Sanitary`, `DomesticHotWater`, `DomesticColdWater`, `Drainage`, `Communication`, `Solar`, `Other`.

### SystemType, ShadingType, LuminaireType, DayOfWeek (unchanged)

## Records

### Address (unchanged)
```yaml
Street: "Firmianstraße 31A"
PostalCode: "5020"
City: "Salzburg"
Country: "AT"
```

### ScheduleEntry (unchanged)
```yaml
DaysOfWeek: '[0, 1, 2, 3, 4]'   # bracket-string for arrays inside RecordArray
StartTime: "06:00"
EndTime: "22:00"
Mode: 0                          # Comfort (OperatingMode key)
```

### PsetSpaceThermalRequirements *(NEW)*
```yaml
SpaceTemperature: 21.0
SpaceTemperatureMin: 21.0       # heating limit
SpaceTemperatureMax: 25.0       # cooling limit
SpaceHumidity: 50.0
SpaceHumidityMin: 35.0
SpaceHumidityMax: 65.0
CO2SetpointMax: 1000.0
```

### PsetSpaceLightingRequirements *(NEW)*
```yaml
IlluminanceTarget: 300.0
IlluminanceMin: 200.0
ArtificialLighting: true
NaturalLighting: true
```

### PsetSpaceOccupancyRequirements *(NEW)*
```yaml
OccupancyType: "office"
OccupancyNumberPeak: 1
AreaPerOccupant: 10.0
OccupancyTimePerDay: 8.0
```

## Archives (Stream Data)

Per-sensor archives provision CrateDB tables for time-series storage. Defined in `data/_general/rt-archives-energyiq.yaml`:

| Archive (rtId) | TargetCkTypeId | Path |
|---|---|---|
| TemperatureSensorArchive (`6a0e…0001`) | `EnergyIQ/TemperatureSensor` | `CurrentValue` |
| HumiditySensorArchive (`6a0e…0002`) | `EnergyIQ/HumiditySensor` | `CurrentValue` |
| CO2SensorArchive (`6a0e…0003`) | `EnergyIQ/CO2Sensor` | `CurrentValue` |

`Path` references the **display name at the target type**, not the global attribute id. All three sensor subtypes expose their measurement as `CurrentValue`, so all three archive Paths are `CurrentValue`.

Activate each archive after import via `octo-cli -c ActivateArchive -id <rtId>` (the `om_importrt.ps1` script does this automatically).

## TimeSeries Support

OctoMesh provides TimeSeries support for all numeric and boolean attributes automatically. In v2, time-series queries target the Sensor entity rather than the Space:

```graphql
query {
  temperatureSensor(id: "6789a00000000000010011a1") {
    name
    currentValue {
      current
      history(from: "2026-05-01", to: "2026-05-21") {
        timestamp
        value
      }
    }
  }
}
```

To navigate from a Space to all its sensors, follow `SpaceSensors`:

```graphql
query {
  space(id: "6789a00000000000000011d1") {
    name
    spaceSensors {
      __typename
      name
      currentValue { current }
    }
  }
}
```

## Simulation Pipelines (v2-targeted)

The simulation pipeline writes onto Sensor / Valve / Damper entities directly (not Space attributes). Configuration file: `data/_pipelines/rt-simulation-adapters.yaml`.

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ Pipeline 1: Data Generation (auto-provisioned Mesh Adapter) │
│                                                             │
│  FromPolling (10s) → Simulation@1 → SelectByPath → Project  │
│                          ↓                                  │
│        Math.Triangle generators (period in seconds)         │
│                          ↓                                  │
│                 ToPipelineDataEvent                         │
└─────────────────────────────────────────────────────────────┘
                          ↓
                    Event Hub
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ Pipeline 2: Update Entities (auto-provisioned Mesh Adapter) │
│                                                             │
│  FromPipelineDataEvent → CreateUpdateInfo@1 → ApplyChanges  │
│                               ↓                             │
│   Updates TemperatureSensor / HumiditySensor / CO2Sensor    │
│   / IlluminanceSensor / Valve / Damper / HeatPump / AHU /   │
│   PV+Inverter+Battery entities for 6 demo rooms             │
└─────────────────────────────────────────────────────────────┘
```

### Simulation Profiles

All base signals use `Math.Triangle` (amplitude 1, period in seconds), scaled with `LinearScaler@1` from `[-1, 1]` onto the range below.

| Variable | Simulator | Period | Range | Description |
|----------|-----------|--------|-------|-------------|
| temperature | Math.Triangle | 1200 s | 18-24°C | Room temperature cycle |
| humidity | Math.Triangle | 1500 s | 35-65% | Humidity cycle |
| co2Level | Math.Triangle | 400 s | 500-900 ppm | Occupancy pattern |
| illuminance | Math.Triangle | 1200 s | 100-700 lux | Daylight curve |
| valvePosition | Math.Triangle | 1000 s | 20-70% | Heating valve demand |
| damperPosition | Math.Triangle | 1000 s | 30-70% | Ventilation demand |
| pvStringPower | Math.Triangle | 1200 s | 0-6 kW | Solar curve |
| stateOfCharge | Math.Triangle | 900 s | 30-90% | Battery cycle |

### Targeted Entities (per-room)

For each of the 6 demo rooms (Wohnbereich, Büro EG, Schlafzimmer, Kinderzimmer, Aufenthaltsraum, Büro DG 1), the MeshPipeline updates:

- `TemperatureSensor.Temperature` (the attribute name is still `Temperature` at the sensor — only the display name is `CurrentValue` when looked up via the type)
- `HumiditySensor.Humidity`
- `CO2Sensor.CO2Level`
- `IlluminanceSensor.Illuminance` (where applicable)
- `Valve.ValvePosition` (the FBH valve)
- `Damper.DamperPosition` (the KWL outlet damper)

Plus plant-level: `HeatPump.SupplyTemp/ReturnTemp/ModulationLevel`, `AirHandlingUnit.SupplyAirTemp/FanSpeedSupply`, full PV chain (`PhotovoltaicSystem`, all `PVString`, both `Inverter`, `BatteryStorage`).

### Adding new simulated entities

1. Add simulation in Pipeline 1 (`Simulation@1`; note the node-level `locale`, and that
   `configuration` must be valid JSON with quoted keys — `frequency` is the period in seconds):
   ```yaml
   - type: Simulation@1
     locale: en
     simulations:
       - targetPath: $.newValueBase
         simulatorKey: Math.Triangle
         configuration: '{"amplitude": 1, "frequency": 720}'
   ```
   (Note: every `targetPath` must be a canonical path starting with `$.`.)

2. Add scaler (LinearScaler@1) — the Triangle output is `[-1, 1]`:
   ```yaml
   - path: "$.newValueBase"
     targetPath: $.newValue
     transformations:
       - type: LinearScaler@1
         scaleInputMin: -1
         scaleInputMax: 1
         scaleOutputMin: 10
         scaleOutputMax: 50
   ```

3. Add update in MeshPipeline (CreateUpdateInfo@1) targeting the **Sensor/Actuator/Terminal entity** (not Space):
   ```yaml
   - type: CreateUpdateInfo@1
     targetPath: $._updateItems
     targetValueKind: Array
     targetValueWriteMode: Append
     updateKind: UPDATE
     rtId: "your-sensor-rtid"
     ckTypeId: EnergyIQ/TemperatureSensor
     attributeUpdates:
       - attributeName: Temperature           # the attribute name at the type
         attributeValueType: Double
         valuePath: $.newValue
   ```

**Important Notes:**
- `rtId` values must be quoted strings
- `attributeName` must use PascalCase matching the CK type definition's `name` field at the type
- `attributeValueType` must match the CK attribute's `valueType`
- For arrays inside RecordArray (e.g. `DaysOfWeek` in `ScheduleEntry`) use the bracket-string format `'[0, 1, 2, 3, 4]'` — block-style YAML lists fail at the parser

## Demo Data: Firmianstraße 31A

The project includes a complete demo building located at Firmianstraße 31A, 5020 Salzburg, Austria. Defined in `data/bim/rt-firmianstrasse.yaml` — **~140 RT entities** in v2 (up from ~35 in v1).

### Building Structure

```
Site: Firmianstraße 31A (47.7833, 13.0333)
├── Hauptgebäude (Main Building, 280 m²)
│   ├── EG (Ground Floor)
│   │   ├── Wohnbereich (LivingRoom) — 45m²
│   │   ├── Büro EG (Office) — 12m²
│   │   ├── WC EG (Toilet) — 3m²
│   │   ├── Bad EG (Bathroom) — 8m²
│   │   ├── Technikraum — 10m²
│   │   └── Vorraum (Corridor) — 8m²
│   ├── OG (Upper Floor)
│   │   ├── Schlafzimmer (Bedroom) — 20m²
│   │   ├── Gästezimmer (GuestRoom) — 14m²
│   │   ├── Kinderzimmer (ChildrensRoom) — 16m²
│   │   ├── Bad OG (Bathroom) — 10m²
│   │   └── WC OG — 3m²
│   └── DG (Attic)
│       ├── Aufenthaltsraum (Lounge) — 25m²
│       ├── Büro DG 1 (Office) — 12m²
│       ├── Büro DG 2 (Office) — 10m²
│       └── Bad DG (Bathroom) — 6m²
├── Nebengebäude (Auxiliary, 60 m²)
│   └── EG
│       ├── Garage — 30m²
│       ├── Werkstatt (Workshop) — 18m²
│       └── Waschküche (Laundry) — 10m²
└── ExternalSpaces (5)
    ├── Terrasse EG, Dachterrasse OG, Balkon DG
    └── Zufahrt, Garten
```

### Plant Equipment

- `HeatPump` "Wärmepumpe" — Sole-Wasser (ground-source / Erdsonde) heat pump with **true passive cooling** via the floor loops (the low ground temperature is the cold sink, no compressor), `HeatSource: Ground`, `IsReversibleAggregate: true`, `HeatPumpOperatingMode: Heating`, COP 4.6, SCOP 5.0
- `Valve` "V-Umschalt-HzgKlt" — `ChangeoverHeatingCooling` valve attached to the heat pump
- `ThermalEnergyStorage` "Heizpufferspeicher" — 500 l stratified buffer
- `AirHandlingUnit` "KWL" — heat recovery 85%
- `Pump` "Heizkreispumpe"

### Room Terminals + Actuators

Each heated room has:
- `RadiantSurface` (Fußbodenheizung) with `IsReversibleTerminal: true` for Hauptgebäude rooms (heating + passive cooling); `Radiator` for Nebengebäude (Werkstatt, Waschküche)
- `Valve` per terminal — `Reversible` for HG, `Heating` for NG
- `AirTerminal` (KWL outlet) where ventilated
- `Damper` per air terminal

### Sensors per Room

Typical residential room: `TemperatureSensor` + `HumiditySensor` (always), `CO2Sensor` (where present), `IlluminanceSensor` + `PresenceSensor` (Wohnbereich + offices). Wet rooms get Temp + Humidity only.

Wohnbereich is the fully-instrumented reference room: Temp + Hum + CO2 + Lux + Presence sensors, RadiantSurface terminal with reversible valve, AirTerminal with damper, plus a south-facing Window with WindowContactSensor and an attached ShadingDevice (Raffstore) with motor.

### PV System (unchanged from v1)

```
PhotovoltaicSystem (18.4 kWp total)
├── PVString Hauptdach Ost — 4.8 kWp, 90°, 30° tilt
├── PVString Hauptdach Süd — 6.0 kWp, 180°, 35° tilt
├── PVString Nebengebäude — 4.0 kWp, 180°, 15° tilt
├── PVString PV-Zaun — 3.6 kWp, 180°, 90° tilt (bifaziale Module)
├── Inverter 1 — 10 kVA (Ost + Süd, hybrid for battery)
├── Inverter 2 — 8 kVA (NG + Zaun)
└── BatteryStorage — 15 kWh LiFePO4
```

### Schedules

4 shared schedules: `Wohnen-Werktag` (Mo-Fr 06-22 Comfort, applied to Wohnbereich, Aufenthalt, Kinderzimmer), `Wohnen-Wochenende` (Sa-So 08-23, Wohnbereich + Aufenthalt), `Schlafzimmer-Nacht` (22-06 Economy, Schlafzimmer), `Buero-Werktag` (Mo-Fr 08-18, all offices).

### DistributionSystems

- `Heizkreis Hauptgebäude` (`Heating`) — HeatPump + ThermalEnergyStorage (heating buffer) + Pump + the changeover valve (members)
- `Kühlkreis Hauptgebäude (Passivkühlung)` (`Cooling`) — the **same** reversible plant as the heating circuit, minus the buffer: HeatPump + Pump + the `ChangeoverHeatingCooling` valve. The heating buffer is deliberately **not** a member — it is hydraulically bypassed in cooling mode (a Heizpufferspeicher is not condensation-safe below dew point, and passive cooling wants minimal thermal mass). The changeover valve is the identifying member that turns the shared loop into the cooling circuit. This shared-plant N:N overlap is the canonical example of why system membership is not the spatial `ParentChild` tree.
- `Lüftung Hauptgebäude` (`Ventilation`) — AHU (member)
- `Elektrisch (PV + Eigenverbrauch)` (`Electrical`) — the full electrical picture as members: `PhotovoltaicSystem` (generation; its strings/inverters/battery hang one hop deeper via the PV system's own `SystemMembers`), the `GridConnection` (Hausanschluss), the 4 per-storey `Meter`s, the 2 `ChargingStation` wallboxes and the 2 `Appliance`s. Serves all 4 storeys via `SystemSpaces`.

### Key RT-IDs Reference

For pipeline development, the simulator targets these sensor/actuator rtIds (Wohnbereich shown — pattern is consistent across rooms):

| Entity | RT-ID |
|---|---|
| Wohnbereich (Space) | `6789a00000000000000011d1` |
| Wohnbereich TemperatureSensor | `6789a00000000000010011a1` |
| Wohnbereich HumiditySensor | `6789a00000000000010011a2` |
| Wohnbereich CO2Sensor | `6789a00000000000010011a3` |
| Wohnbereich IlluminanceSensor | `6789a00000000000010011a4` |
| Wohnbereich PresenceSensor | `6789a00000000000010011a5` |
| Wohnbereich RadiantSurface (FBH) | `6789a00000000000020011b1` |
| Wohnbereich Valve | `6789a00000000000020011b2` |
| Wohnbereich AirTerminal | `6789a00000000000020011c1` |
| Wohnbereich Damper | `6789a00000000000020011c2` |
| HeatPump (Wärmepumpe) | `6789a00000000000000060f1` |
| Heat pump changeover valve | `6789a00000000000000060f2` |
| ThermalEnergyStorage | `6789a00000000000000063f4` |
| AirHandlingUnit (KWL) | `6789a00000000000000061f2` |
| PhotovoltaicSystem | `6789a00000000000000080a1` |

Sensor rtIds follow the pattern `6789a00000000000010{spaceId}a{n}` for room sensors; actuators/terminals use `6789a00000000000020{spaceId}{type}{n}`.

## File Organization

```
src/EnergyIqCkModel/ConstructionKit/
├── ckModel.yaml                # EnergyIQ-2.0.0, depends on Basic-[2.0,3.0)
├── enums/                      # 12 enums (incl. HeatPumpOperatingMode, ValveType, etc.)
├── records/                    # 5 records (Address, ScheduleEntry, Pset_*×3)
├── attributes/                 # grouped by domain (heatPump, sensor, actuator, terminal, schedule, distributionSystem, pset, …)
├── associations/               # 11 association roles
└── types/                      # ~45 type files (Site / Building / Storey / Space /
                                #   ExternalSpace / Sensor+subtypes / Actuator+subtypes /
                                #   RoomTerminal+subtypes / HeatPump / ThermalEnergyStorage /
                                #   Schedule / DistributionSystem / unchanged PV chain)

data/
├── _general/
│   ├── rt-archives-energyiq.yaml      # 3 per-sensor archives
│   ├── rt-adapters-mesh.yaml
│   └── rt-autoincrement.yaml
├── _pipelines/
│   └── rt-simulation-adapters.yaml    # simulation targeting Sensor/Actuator entities
├── _queries/
│   └── _trees.yaml
└── bim/
    └── rt-firmianstrasse.yaml          # ~140 entities, the v2 demo property
```

## Future Extensions

Out-of-scope items for v2 (kept compatible for later addition — see `docs/space-restructuring-concept.md` §12):

- Lüftung mit Wärmerückgewinnung pro Raum (decentralised HRV units) — add a `RoomTerminal` subtype
- Solarthermie (`SolarThermalCollector`)
- BHKW / cogeneration (`CombinedHeatAndPower`)
- Pelletkessel, Holzvergaser (Boiler PredefinedType variants or own subtype)
- Submetering / Smart Meter (`EnergyMeter` sensor subtype + Distribution-System binding)
- Wallbox / EV charger (`ElectricVehicleCharger` TechnicalSystem subtype)
- Weather-compensated heating curve (Schedule extension or dedicated record)
- Native IFC export (separate workstream)

## Migration from v1 to v2

EnergyIQ-1.x → 2.0.0 is a **breaking change**. The major shifts:

- Space attributes removed: `Temperature`, `Humidity`, `CO2Level`, `Illuminance`, `PresenceDetected`, `WindowOpen`, `HeatingValvePosition`, `CoolingValvePosition`, `VentilationLevel`, `LightingLevel`, `ShadingPosition`, `TemperatureSetpointHeating/Cooling`, `CO2Setpoint`, `IlluminanceSetpoint`, `ScheduleEntries`, `EnergyConsumption*`
- Replaced by Sensor / Actuator / Terminal entities + Pset_* records + Schedule entities
- `HaystackTags`/`HaystackRefs`/`HaystackMeta` removed across all types
- New types: ~25 (Sensor subtypes, Actuator subtypes, Terminal subtypes, HeatPump, ThermalEnergyStorage, ExternalSpace, Schedule, DistributionSystem)
- `Boiler` is no longer the correct type for a heat pump — use `HeatPump`
- Outdoor "spaces" are now `ExternalSpace`, not `Space` with `SpaceType: Other`

There is no automatic migration tooling — runtime data must be re-modeled. See `docs/space-restructuring-concept.md` for the full reasoning and migration approach.

## References

- [ISO 16739-1:2024 (IFC 4.3)](https://www.iso.org/standard/84123.html)
- [buildingSMART IFC Documentation](https://ifc43-docs.standards.buildingsmart.org/)
- [VDI 3814 Building Automation](https://www.vdi.de/richtlinien/unsere-richtlinien-highlights/vdi-3814)
- [Project Haystack](https://project-haystack.org/) (via separate projection — see `docs/haystack-integration-concept.md`)
- [OctoMesh Construction Kit Documentation](https://docs.meshmakers.cloud/)
