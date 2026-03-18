# Standards Reference

## Overview: Standards Landscape

```
                    BIM / Planning                    Operations / IoT
                         │                                │
              ┌──────────┴──────────┐         ┌──────────┴──────────┐
              │                     │         │                     │
         ISO 16739              VDI 3814              Project
           (IFC)                  (BA)               Haystack
              │                     │                    │
         "Structure"           "Functions"          "Semantics"
         What is where?        How is it controlled? What does the
         (Rooms, Walls)        (Controllers, Sched.) data point mean?
              │                     │                    │
              └──────────┬──────────┴────────────────────┘
                         │
                         ▼
                    ┌─────────┐
                    │EnergyIQ │  ← Combines all three
                    └─────────┘
```

---

## ISO 16739-1:2024 (IFC 4.3)

### Overview

**Industry Foundation Classes (IFC)** is an open international standard for Building Information Modeling (BIM). The current version IFC 4.3 (ISO 16739-1:2024) extends the scope to infrastructure (bridges, roads, railways).

**Origin:** Architecture/Construction (buildingSMART)
**Focus:** Planning & Construction ("As-Built")
**Data:** Static (geometry, structure, properties)

### Relevant Concepts for EnergyIQ

#### Spatial Structure

IFC defines a hierarchical spatial decomposition:

```
IfcProject
└── IfcSite                    → Site
    └── IfcBuilding            → Building
        └── IfcBuildingStorey  → BuildingStorey
            └── IfcSpace       → Space
```

The relationships are established via `IfcRelAggregates`.

#### Spatial Containment

Building elements are assigned to spaces via `IfcRelContainedInSpatialStructure`:

```
IfcSpace ◄── IfcRelContainedInSpatialStructure ──► IfcWall, IfcDoor, IfcWindow, ...
```

#### Property Sets

IFC uses PropertySets for additional properties:
- `Pset_SpaceCommon` – General space properties
- `Pset_SpaceThermalRequirements` – Thermal requirements
- `Pset_SpaceOccupancyRequirements` – Occupancy requirements

**Mapping to OctoMesh:** PropertySets → Records or direct attributes

#### Building Elements

| IFC Entity | EnergyIQ Type |
|------------|---------------|
| IfcWall | Wall |
| IfcDoor | Door |
| IfcWindow | Window |
| IfcShadingDevice | ShadingDevice |
| IfcLightFixture | Luminaire |

#### Building Systems

| IFC Entity | EnergyIQ Type |
|------------|---------------|
| IfcSystem | TechnicalSystem |
| IfcDistributionSystem | HVACSystem |
| IfcUnitaryEquipment | AirHandlingUnit |
| IfcBoiler | Boiler |
| IfcPump | Pump |

### Sources

- [buildingSMART IFC Specification](https://technical.buildingsmart.org/standards/ifc/)
- [IFC 4.3 Documentation](https://ifc43-docs.standards.buildingsmart.org/)
- [ISO 16739-1:2024](https://www.iso.org/standard/84123.html)

---

## VDI 3814 (Building Automation)

### Overview

The **VDI Guideline 3814** describes the state of the art for planning and implementing building automation (BA) systems. It was fundamentally revised in 2019 and integrates the former VDI 3813.

**Origin:** Building services planning, Germany (VDI)
**Focus:** Planning & documentation of BA systems
**Data:** Function descriptions, data point lists

### Guideline Series Structure

| Part | Content |
|------|---------|
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
├── Room Automation (RA)
│   ├── Temperature control
│   ├── Lighting control
│   ├── Sun protection control
│   └── Presence detection
├── Plant Automation (PA)
│   ├── HVAC systems (Heating, Ventilation, Air Conditioning)
│   ├── Sanitary systems
│   └── Electrical engineering
└── BA Management
    ├── Monitoring
    ├── Operation
    └── Optimization
```

### BA Functions (Part 3.1)

Basic building automation functions as function blocks:

#### General Functions
- **Switching** – On/off control
- **Limit monitoring** – Alarm on threshold violation
- **Time schedule** – Time-controlled actions
- **Counter** – Operating hours, energy

#### Room Automation
- **Temperature control** – PI/PID control for heating/cooling
- **Lighting control** – Switching/dimming function
- **Sun protection control** – Position and slats
- **Presence detection** – Motion sensor logic

#### Plant Automation
- **PID controller** – Universal controller
- **Sequence control** – Sequential control
- **Pump control** – On/off with interlock
- **Valve control** – Open/close/modulating

### Data Point Types

| Type | Direction | Signal | Example |
|------|-----------|--------|---------|
| Binary input | Input | Binary | Window contact |
| Binary output | Output | Binary | Pump on/off |
| Analog input | Input | Analog | Temperature |
| Analog output | Output | Analog | Valve position |
| Counter input | Input | Counter | Energy meter |

**Mapping to OctoMesh:**
- In VDI 3814, data points are standalone objects
- In EnergyIQ/OctoMesh: Data points = attributes on the object (OO approach)

### BA Identification (Part 4.1)

System identification schema:
```
+Site=Building-Storey-Room.System:Component%Signal
```

Example:
```
+Vienna=BldgA-GF-B001.HTG:SL%Temp
```

### Sources

- [VDI 3814 Overview](https://www.vdi.de/richtlinien/unsere-richtlinien-highlights/vdi-3814)
- [VDI 3814 Part 1 – Fundamentals](https://www.vdi.de/richtlinien/details/vdi-3814-blatt-1-gebaeudeautomation-ga-grundlagen)
- [VDI 3814 Part 3.1 – BA Functions](https://www.vdi.de/richtlinien/details/vdi-3814-blatt-31-gebaeudeautomation-ga-ga-funktionen-automationsfunktionen)

---

## Project Haystack

### Overview

**Project Haystack** is an open-source initiative (since 2014) for standardizing semantic tagging of IoT and building data. It solves the problem that BA data points often have cryptic names and machines cannot understand their meaning.

**Origin:** BA operations, USA (industry consortium)
**Focus:** Runtime data, interoperability
**Data:** Semantic tags for data points

**Founding members:** Siemens, Intel, J2 Innovations, SkyFoundry, Lynxspring, Legrand

### The Problem

```
BACnet data point:
  Name: "AHU1.SF.SPD"
  Value: 75.0

→ What does that mean? A human knows, a machine does not.
```

### The Solution: Semantic Tagging

```
With Haystack tags:
  Name: "AHU1.SF.SPD"
  Value: 75.0
  Tags: { ahu, supply, fan, speed, sensor, unit:"%" }
          │     │      │    │      │
          │     │      │    │      └── Type: Measurement
          │     │      │    └── What: Speed
          │     │      └── Component: Fan
          │     └── Air side: Supply
          └── Equipment: Air handling unit
```

### Core Concepts

#### 1. Tags (Vocabulary)
Standardized terms such as `temp`, `humidity`, `ahu`, `vav`, `pump`, `sensor`, `cmd`, `sp` (setpoint).

#### 2. Marker Tags vs. Value Tags
```
Marker:  { hot, water, pump }           ← Presence only
Value:   { unit: "°C", maxVal: 100 }    ← With value
```

#### 3. Conjuncts (Compound Tags)
```
chilled-water    ← chilled + water
hot-water-plant  ← hot + water + plant
```

#### 4. Taxonomy (Inheritance)
```
equip
├── hvac
│   ├── ahu
│   ├── vav
│   └── fcu
├── meter
│   ├── elec-meter
│   └── gas-meter
└── pump
```

#### 5. References (Relationships)
```
VAV-01:
  tags: { vav, hvac, equip }
  equipRef: @ahu-01           ← Belongs to AHU-01
  spaceRef: @room-101         ← Serves room 101
```

### Haystack 5 + Xeto (2024/25)

The current version extends Haystack from "flat tagging" to a complete ontology:

| Version | Concept |
|---------|---------|
| Haystack 1-4 | Flat tags, loose conventions |
| Haystack 5 | Formal ontology with type hierarchy |
| Xeto | Schema language for validation |

```
Haystack 5 = Semantics ("What does it mean?")
Xeto       = Structure ("What must it look like?")
```

### Haystack vs. OctoMesh CK

| Aspect | Haystack | OctoMesh CK |
|--------|----------|-------------|
| Model | Tag-based (flat) | Object-oriented |
| Typing | Implicit via tags | Explicit classes |
| Inheritance | Taxonomy | True class hierarchy |
| Relationships | Reference tags | Typed associations |
| Validation | Xeto (new) | Schema-based |
| Time series | External (SkySpark etc.) | Integrated |

**OctoMesh CK is more expressive**, but Haystack has broad industry adoption.

### Integration in EnergyIQ

#### Option 1: Haystack Tags as Attribute (Recommended)

```yaml
Space:
  name: "Meeting Room 1"
  temperature: 22.3
  haystackTags: ["space", "room", "meetingRoom", "hvacZone"]

AirHandlingUnit:
  name: "AHU-01"
  supplyAirTemp: 18.5
  haystackTags: ["ahu", "hvac", "equip"]
  haystackRefs:
    siteRef: "@site-001"
    spaceRef: ["@space-001", "@space-002"]
```

#### Option 2: Automatic Tag Mapping

```
EnergyIQ Type    →  Haystack Tags
─────────────────────────────────
Space            →  space, hvacZone
AirHandlingUnit  →  ahu, hvac, equip
Boiler           →  boiler, hvac, equip, hot, water
Temperature      →  temp, sensor, point
```

#### Option 3: Haystack Export

EnergyIQ model → Haystack JSON/Zinc for external tools (SkySpark, FIN Framework).

### Related Standards & Convergence

```
┌─────────────────────────────────────────────────────┐
│              ASHRAE 223P (in development)            │
│                                                     │
│     Haystack + Brick Schema + BACnet = Unified      │
└─────────────────────────────────────────────────────┘
```

| Standard | Focus | Status |
|----------|-------|--------|
| **Haystack** | Tagging for BA/IoT | Active, Version 5 |
| **Brick Schema** | Ontology for buildings | Academic, UC Berkeley |
| **ASHRAE 223P** | Unification | In development |
| **SAREF4BLDG** | EU Smart Appliances | EU standard |

### Sources

- [Project Haystack](https://project-haystack.org/)
- [Haystack Documentation](https://project-haystack.org/doc)
- [Haystack 5 Announcement](https://marketing.project-haystack.org/)
- [Xeto Schema Language](https://project-haystack.org/doc/docHaystack/Xeto)

---

## Standards Comparison

| Aspect | IFC | VDI 3814 | Haystack | EnergyIQ |
|--------|-----|----------|----------|----------|
| **Origin** | BIM/Architecture | Building services, Germany | BA/IoT, USA | OctoMesh |
| **Focus** | Planning & Construction | Planning & Documentation | Operations & Runtime | Energy & Optimization |
| **Data Model** | OO (EXPRESS) | Function blocks | Tags (flat→ontology) | OO (CK) |
| **Structure** | Spatial | Functional | Semantic | Combined |
| **Time Series** | No | Partially | External | Integrated |
| **Relationships** | Explicit | Implicit | Reference tags | Associations |
| **Format** | STEP/XML | Proprietary | JSON/Zinc | GraphQL/YAML |

---

## Integration in EnergyIQ

### Adopted from IFC
- Spatial hierarchy (Site → Building → Storey → Space)
- Building elements (Door, Window, etc.)
- Unique GlobalIds
- PropertySet concept → Records

### Adopted from VDI 3814
- Classification into room/plant automation
- Functional description (actual/setpoint/control output)
- Identification schemas
- Operating modes

### Adopted from Haystack (Optional)
- Semantic tags for interoperability
- Reference concept for equipment relationships
- Industry vocabulary for analytics tools

### EnergyIQ Extensions
- TimeSeries as first-class citizen
- AI optimization layer
- Energy aggregation
- OO modeling (attributes instead of data points)
- Validation via CK schema
