# Standards Reference

**EnergyIQ-2.0.0** mapping reference for ISO 16739-1:2024 (IFC 4.3), VDI 3814, ISO 4157, and Project Haystack 4.

## Overview: Standards Landscape

```
                    BIM / Planning                    Operations / IoT
                         │                                │
              ┌──────────┴──────────┐         ┌──────────┴──────────┐
              │                     │         │                     │
         ISO 16739              VDI 3814              Project
           (IFC 4.3)              (BA)               Haystack 4
              │                     │                    │
         "Structure"           "Functions"          "Semantics"
         Spaces, Equip,         Anlagen-/             What does
         Sensors, Actuators,    Raum-Automation,     this data
         Pset_*                 Sensor/Aktor          point mean?
              │                     │                    │
              └──────────┬──────────┴────────────────────┘
                         │
                         ▼
                    ┌─────────┐         Projection (not mixin)
                    │EnergyIQ │ ────────────► PH4-Lib / Renderer
                    │ v2.0.0  │
                    └─────────┘
```

---

## ISO 16739-1:2024 (IFC 4.3)

### Overview

**Industry Foundation Classes (IFC)** is an open international standard for Building Information Modeling (BIM). The current version IFC 4.3 (ISO 16739-1:2024) extends the scope to infrastructure (bridges, roads, railways).

- **Origin:** Architecture / Construction (buildingSMART)
- **Focus:** Planning, construction, and increasingly operations
- **Data:** Static (geometry, structure, properties) + property sets for operational requirements

### Spatial Structure

IFC defines a hierarchical spatial decomposition, established via `IfcRelAggregates`:

```
IfcProject
└── IfcSite                            → EnergyIQ/Site
    ├── IfcBuilding                    → EnergyIQ/Building
    │   └── IfcBuildingStorey          → EnergyIQ/BuildingStorey
    │       ├── IfcSpace               → EnergyIQ/Space
    │       └── IfcExternalSpatialElement → EnergyIQ/ExternalSpace
    └── IfcExternalSpatialElement      → EnergyIQ/ExternalSpace (e.g. Garten, Zufahrt)
```

The `ExternalSpace` type is new in v2 and mirrors `IfcExternalSpatialElement` (introduced in IFC 4.0) — outdoor areas don't carry indoor thermal/lighting requirements.

### Property Sets (Pset_*)

IFC defines reusable property sets for additional properties. v2 implements three Pset_* concepts as CK records on Space, clearly separating design/operational *requirements* from runtime *measurements*:

| IFC Pset | EnergyIQ Record | Carries |
|---|---|---|
| `Pset_SpaceThermalRequirements` | `PsetSpaceThermalRequirements` | SpaceTemperature, SpaceTemperatureMin/Max, SpaceHumidity[Min/Max], CO2SetpointMax |
| `Pset_SpaceLightingRequirements` | `PsetSpaceLightingRequirements` | IlluminanceTarget, IlluminanceMin, ArtificialLighting, NaturalLighting |
| `Pset_SpaceOccupancyRequirements` | `PsetSpaceOccupancyRequirements` | OccupancyType, OccupancyNumberPeak, AreaPerOccupant, OccupancyTimePerDay |

### Building Elements

| IFC Entity | EnergyIQ Type | Notes |
|---|---|---|
| IfcWall | Wall | |
| IfcDoor | Door | + WindowContactSensor via EquipmentSensors |
| IfcWindow | Window | + WindowContactSensor |
| IfcShadingDevice | ShadingDevice | + Motor actuator via EquipmentActuators |
| IfcLightFixture | Luminaire | + Dimmer actuator |

### Sensors (NEW v2)

| IFC PredefinedType | EnergyIQ Type | Measurement |
|---|---|---|
| IfcSensor / TEMPERATURESENSOR | TemperatureSensor | Double, °C |
| IfcSensor / HUMIDITYSENSOR | HumiditySensor | Double, %RH |
| IfcSensor / CO2SENSOR | CO2Sensor | Double, ppm |
| IfcSensor / LIGHTSENSOR | IlluminanceSensor | Double, lux |
| IfcSensor / MOVEMENTSENSOR | PresenceSensor | Boolean |
| IfcSensor / CONTACTSENSOR | WindowContactSensor | Boolean |
| IfcSensor / USERDEFINED | GenericSensor | String (carrier) |

### Actuators (NEW v2)

| IFC Entity | EnergyIQ Type | Notes |
|---|---|---|
| IfcValve | Valve | ValveType enum incl. Reversible/Changeover |
| IfcDamper | Damper | |
| IfcActuator / ELECTRICACTUATOR | Dimmer | Standalone DALI ballast etc. |
| IfcMotorConnection / IfcActuator | Motor | Shading drive, pump VSD, fan motor |

### Building Services Plant Equipment

| IFC Entity / PredefinedType | EnergyIQ Type |
|---|---|
| IfcUnitaryEquipment / HEATPUMP | **HeatPump** *(NEW v2)* |
| IfcBoiler | Boiler |
| IfcChiller | Chiller |
| IfcPump | Pump |
| IfcUnitaryEquipment / AIRHANDLER | AirHandlingUnit |
| IfcTank / THERMALTANK | **ThermalEnergyStorage** *(NEW v2)* |

### Room Terminals (Distribution Endpoints) — NEW v2

| IFC Entity / PredefinedType | EnergyIQ Type | Notes |
|---|---|---|
| IfcSpaceHeater / RADIATOR | Radiator | |
| IfcSpaceHeater / RADIATOR (USERDEFINED radiantFloor/Ceiling) | RadiantSurface | Reversible H+C via IsReversibleTerminal |
| IfcCooledBeam | ChilledBeam | |
| IfcUnitaryEquipment + IfcFan | FanCoilUnit | 2-pipe or 4-pipe |
| IfcAirTerminal / IfcAirTerminalBox | AirTerminal | VAV/CAV/Diffuser |
| IfcSpaceHeater / CONVECTOR (ELECTRIC USERDEFINED) | ElectricHeater | |

### Logical Systems

| IFC Entity | EnergyIQ Type |
|---|---|
| IfcSystem | TechnicalSystem (abstract) |
| IfcDistributionSystem | **DistributionSystem** *(NEW v2)* |
| IfcSolarDevice | PVString |
| IfcElectricFlowStorageDevice | BatteryStorage |

### Schedules

| IFC Entity | EnergyIQ Type |
|---|---|
| IfcWorkSchedule (analog) | **Schedule** *(NEW v2)* — M:N to Space, shared across rooms |

### Sources

- [buildingSMART IFC Specification](https://technical.buildingsmart.org/standards/ifc/)
- [IFC 4.3 Documentation](https://ifc43-docs.standards.buildingsmart.org/)
- [ISO 16739-1:2024](https://www.iso.org/standard/84123.html)

---

## ISO 4157 (Designation Systems)

EnergyIQ implements ISO 4157 for storey and room designation.

### ISO 4157-1: Storey Numbering

| EnergyIQ Attribute | Description | Example |
|---|---|---|
| `BuildingStorey.StoreyNumber` | Consecutive from bottom, 1 = ground | 1, 2, 3 |
| `BuildingStorey.FloorDesignation` | National code | EG, 1.OG, 2.OG, DG, UG |

### ISO 4157-2: Room Numbers (Daily Use)

| EnergyIQ Attribute | Description | Example |
|---|---|---|
| `Space.RoomNumber` | Floor prefix + 2-digit sequential | EG01, 1OG02, DG03 |

Pattern: `{FloorPrefix}{2-digit-sequence}` — Nebengebäude: `NG-EG01`. Outdoor (`ExternalSpace`): `A01`.

### ISO 4157-3: Room Identifiers (Lifecycle)

| EnergyIQ Attribute | Description | Example |
|---|---|---|
| `Space.RoomIdentifier` | Immutable: `I#` + storey + 3-digit | I#1001, I#2015, I#3001 |

**Important:** Room identifiers are **immutable** throughout the building lifecycle.

---

## VDI 3814 (Building Automation)

### Overview

**VDI Guideline 3814** describes the state of the art for planning and implementing building automation (BA) systems. It was fundamentally revised in 2019 and integrates the former VDI 3813.

- **Origin:** Building services planning, Germany (VDI)
- **Focus:** Planning & documentation of BA systems
- **Data:** Function descriptions, data point lists

### Guideline Series Structure

| Part | Content |
|---|---|
| 1 | Fundamentals |
| 2.1 | Requirements planning |
| 2.2 | Planning content, system integration |
| 3.1 | BA functions (automation functions) |
| 3.2 | Macros from basic functions |
| 4.1 | Identification systems |
| 4.2 | Checklists |
| 4.3 | BA automation schema, function list |

### Building Automation Classification

```
Building Automation (BA)
├── Raum-Automation (Room Automation)
│   ├── Temperature control       ← EnergyIQ: TemperatureSensor + RadiantSurface + Valve
│   ├── Lighting control          ← EnergyIQ: IlluminanceSensor + Luminaire / Dimmer
│   ├── Sun protection control    ← EnergyIQ: ShadingDevice + Motor
│   ├── Ventilation control       ← EnergyIQ: AirTerminal + Damper
│   ├── Presence detection        ← EnergyIQ: PresenceSensor
│   └── Window monitoring         ← EnergyIQ: WindowContactSensor
├── Anlagen-Automation (Plant Automation)
│   ├── HVAC systems              ← EnergyIQ: HeatPump, Boiler, Chiller, AHU, Pump
│   ├── Thermal storage           ← EnergyIQ: ThermalEnergyStorage
│   ├── Electrical & PV           ← EnergyIQ: PhotovoltaicSystem, Inverter, BatteryStorage
│   └── Distribution              ← EnergyIQ: DistributionSystem (logical grouping)
└── Management-Ebene
    ├── Schedules                 ← EnergyIQ: Schedule (M:N to Space)
    ├── Operating modes           ← EnergyIQ: OperatingMode (Comfort/Economy/…)
    └── Optimization              ← EnergyIQ: external (analytics layer)
```

### The Room / Plant Split in v2

The v2 restructuring (`docs/space-restructuring-concept.md`) explicitly mirrors VDI 3814's split:

- **Room level (Raumautomation):** sensors report room conditions, terminals deliver heating/cooling/ventilation locally, actuators control the terminals. `Space` carries master data + operating mode + schedule references; never measurements or terminal control signals.
- **Plant level (Anlagenautomation):** TechnicalSystem subtypes generate heat/cold/air at building scale; they connect to room terminals via `TerminalServedBy`.

This is the structural reason v1's "everything on Space" model was reorganized.

### BA Functions (Part 3.1)

EnergyIQ maps the VDI 3814 function types to entity attributes / associations:

| VDI Function | EnergyIQ Realization |
|---|---|
| Switching (on/off) | `Luminaire.IsOn`, `Pump.IsRunning`, `Motor.State`, etc. |
| Limit monitoring | (Out of scope — application layer / Studio rules) |
| Time schedule | `Schedule` entity + `Schedule.Entries` |
| Counter (operating hours, energy) | `DistributionSystem.TotalEnergyConsumed`, `PVString.EnergyProducedKWh`, `BatteryStorage.CycleCount` |
| PI/PID controller | (Plant-internal — not modeled at CK level) |
| Sequence control | (Plant-internal) |
| Pump control | `Pump.SpeedSetpoint` + `Pump.IsRunning` |
| Valve control | `Valve.Position` + `Valve.PositionSetpoint` |
| Damper control | `Damper.Position` + `Damper.PositionSetpoint` |

### Data Point Types (VDI vs. EnergyIQ)

VDI 3814 treats data points as standalone objects (BI/BO/AI/AO/CI). EnergyIQ v2 promotes those data points to **first-class Sensor / Actuator entities** — each VDI data point becomes its own RT entity with typed `CurrentValue` / `Position` / etc.

| VDI Data Point Type | Direction | EnergyIQ Equivalent |
|---|---|---|
| Binary input | Input | `PresenceSensor`, `WindowContactSensor` |
| Binary output | Output | `Motor.State`, `Luminaire.IsOn` |
| Analog input | Input | `TemperatureSensor`, `HumiditySensor`, `CO2Sensor`, `IlluminanceSensor` |
| Analog output | Output | `Valve.Position`, `Damper.Position`, `Luminaire.DimmingLevel` |
| Counter input | Input | (`DistributionSystem.TotalEnergyConsumed` aggregated, or via `GenericSensor`) |

### Operating Modes (VDI 3814)

EnergyIQ implements the standard VDI 3814 room operating modes via the `OperatingMode` enum on `Space`:

| Mode | Description |
|---|---|
| Comfort | Full conditioning, occupancy expected |
| Economy | Reduced setpoints, lower energy use |
| Standby | Minimal conditioning, ready for occupancy |
| Protection | Freeze / overheat protection only |
| Off | System disabled |
| Auto | Schedule-driven mode selection |

### BA Identification (Part 4.1)

VDI 3814-4.1 prescribes an identification schema. EnergyIQ does **not** generate VDI-style identifier strings automatically — the equivalent role is fulfilled by:
- `Space.RoomNumber` / `Space.RoomIdentifier` (ISO 4157)
- `TechnicalSystem.Identifier` (e.g. "HZG-01", "KWL-01", "PMP-HK01")
- Entity-level descriptive `Name`s

Generating canonical VDI identifier strings is a downstream concern (export tooling), not part of the core CK.

### Sources

- [VDI 3814 Overview](https://www.vdi.de/richtlinien/unsere-richtlinien-highlights/vdi-3814)
- [VDI 3814 Part 1 – Fundamentals](https://www.vdi.de/richtlinien/details/vdi-3814-blatt-1-gebaeudeautomation-ga-grundlagen)
- [VDI 3814 Part 3.1 – BA Functions](https://www.vdi.de/richtlinien/details/vdi-3814-blatt-31-gebaeudeautomation-ga-ga-funktionen-automationsfunktionen)

---

## Project Haystack 4

### Overview

**Project Haystack** is an open-source initiative (since 2014) for standardizing semantic tagging of IoT and building data. Version 4 introduces a formal spec system (`lib`, `spec`, Fantom-typed tags).

- **Origin:** BA operations, USA (industry consortium)
- **Focus:** Runtime data, interoperability between BA tools (SkySpark, FIN Framework, Niagara, etc.)
- **Data:** Semantic tags, typed specs in PH4

**Founding members:** Siemens, Intel, J2 Innovations, SkyFoundry, Lynxspring, Legrand.

### Core Concepts

1. **Tags** — Standardized vocabulary (`temp`, `humidity`, `ahu`, `vav`, `pump`, `sensor`, `cmd`, `sp`, …).
2. **Marker vs. Value tags** — Markers indicate presence (`{ hot, water, pump }`), value tags carry data (`{ unit: "°C", maxVal: 100 }`).
3. **Conjuncts** — Compound tags (`chilled-water`, `hot-water-plant`).
4. **Taxonomy / Specs** (PH4) — `equip` → `hvac` → `ahu`, `vav`, `fcu`, … with formal specs.
5. **References** — `equipRef`, `siteRef`, `spaceRef` link entities.

### PH4 vs. EnergyIQ CK

In v2 we explicitly chose **not** to model Haystack as a parallel CK. The reasoning (see `docs/haystack-integration-concept.md`):

PH4's spec system is semantically isomorphic to OctoMesh CK:

| OctoMesh CK | PH4 |
|---|---|
| CK Model (e.g. `EnergyIQ-2.0.0`) | `lib` |
| CK Type | `spec` |
| Attribute with `valueType` | typed tag slot (`Marker`, `Str`, `Number`, `Ref`, `Date`, …) |
| Enum | constrained `Str` |
| `derivedFromCkTypeId` | spec inheritance |
| Association | `Ref`-slot with `of:` constraint |
| RT entity | dict |
| Collection of RT entities | grid |

Modeling Haystack inside CK would be a metamodel-inside-metamodel duplication. Instead, EnergyIQ uses a **projection layer**: a declarative mapping config (see `docs/haystack-integration-concept.md`) produces PH4-compliant grids on demand. The same mapping config can also generate a PH4 `lib` definition.

### Architectural Implication of v2

The Haystack `Point` is a first-class entity with refs back to its space/equip. In v1, EnergyIQ kept measurements as inline Space attributes — that meant a Haystack export needed to "explode" each measurement attribute into a separate Point dict (structural impedance). In **v2**, measurements are already separate Sensor entities. The export becomes 1:1: one `TemperatureSensor` ≈ one PH `Point` (markers `point sensor temp air zone`, kind `Number`, unit `"°C"`). The projection layer is now simpler.

### Sources

- [Project Haystack](https://project-haystack.org/)
- [Haystack Documentation](https://project-haystack.org/doc)
- [Haystack 4 Specs](https://project-haystack.org/doc/docHaystack/Specs)
- [Xeto Schema Language](https://project-haystack.org/doc/docHaystack/Xeto)

---

## Standards Comparison

| Aspect | IFC 4.3 | VDI 3814 | Project Haystack 4 | EnergyIQ-2.0.0 |
|---|---|---|---|---|
| **Origin** | BIM / Architecture | BA planning, Germany | BA / IoT, USA | OctoMesh |
| **Focus** | Planning, Construction, Operations | Planning & Documentation | Operations & Runtime | Energy modeling & optimization |
| **Data Model** | OO (EXPRESS / IFC4.3 spec) | Function blocks | Tags + Specs | OO (CK) |
| **Structure** | Spatial + Functional | Functional | Semantic | Combined (IFC + VDI + projection to PH) |
| **Time Series** | No | Partial | External | Integrated via OctoMesh archives |
| **Relationships** | Explicit (IfcRel*) | Implicit | Reference tags | Typed CK associations |
| **Format** | STEP / XML / JSON | Proprietary docs + data point lists | JSON / Zinc / Trio | GraphQL / YAML |
| **Sensors as entities** | Yes (IfcSensor) | Yes (data points are objects) | Yes (Points) | Yes (NEW v2) |
| **Pset-style requirements** | Yes (Pset_*) | — | — | Yes (PsetSpace*Requirements records) |

---

## Integration in EnergyIQ-2.0.0

### Adopted from IFC 4.3

- Spatial hierarchy (Site → Building → Storey → Space / ExternalSpace) via `Basic/Tree` + `Basic/TreeNode`
- Building elements (Wall, Door, Window, ShadingDevice, Luminaire) as `BuildingElement` subtypes
- Sensors / Actuators / Terminals as first-class entities (IfcSensor / IfcActuator / IfcSpaceHeater / IfcAirTerminal / …)
- Property Set concept → CK records (`PsetSpaceThermalRequirements`, `PsetSpaceLightingRequirements`, `PsetSpaceOccupancyRequirements`)
- `IfcExternalSpatialElement` → `ExternalSpace` type
- `IfcDistributionSystem` → `DistributionSystem` type
- `IfcUnitaryEquipment / HEATPUMP` → `HeatPump` type (reversible aggregates with operating-mode enum)
- `IfcTank / THERMALTANK` → `ThermalEnergyStorage`
- Unique `GlobalId` carried on all relevant types

### Adopted from VDI 3814

- Hard separation of Anlagen-Automation (`TechnicalSystem` hierarchy) vs. Raum-Automation (`RoomTerminal` + `Sensor` + `Actuator` at the Space)
- `OperatingMode` enum on Space (Comfort/Economy/Standby/Protection/Off/Auto)
- `HeatPumpOperatingMode` and `TerminalOperatingMode` enums for plant and terminal modes
- Functional terminology in attribute names (e.g. `ValveType: ChangeoverHeatingCooling`, `Damper.AirflowSetpoint`)
- Schedule as separate entity (Management-Ebene)
- TechnicalSystem.Identifier carrying VDI-3814-style tags ("HZG-01", "KWL-01")

### Adopted from ISO 4157
- `Space.RoomNumber` (4157-2, daily use), `Space.RoomIdentifier` (4157-3, immutable lifecycle), `BuildingStorey.StoreyNumber` / `FloorDesignation` (4157-1)

### Adopted from Project Haystack 4
- Indirectly, via the projection layer documented in `haystack-integration-concept.md`. Concrete mapping config + renderer are planned (Phase 1+3 of that concept). EnergyIQ runtime data itself carries no Haystack tags in v2.

### EnergyIQ Extensions (beyond standards)
- TimeSeries archives per Sensor type (CrateDB)
- `DistributionSystem.TotalEnergyConsumed/Delivered` aggregates for energy reporting
- Sensor `Manufacturer`/`Model`/`SerialNumber`/`Accuracy`/`IsFaulty` metadata
- AI optimization layer (external — application tier on top of CK)
- Multi-tenant runtime support via OctoMesh platform
