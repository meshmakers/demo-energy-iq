# EnergyIQ Construction Kit - Developer Guide

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

# 4. Import Construction Kits (Basic + EnergyIQ)
./om_importck.ps1

# 5. Import Runtime data (adapters, pipelines, demo building)
./om_importrt.ps1
```

After setup, the simulation automatically generates data every 10 seconds.

**Access GraphQL API:** `https://localhost:5001/graphql` (Tenant: `energyiqdemo`)

---

## Introduction

EnergyIQ is an OctoMesh Construction Kit (CK) model library for intelligent building energy optimization. It provides a standards-compliant domain model that combines:

- **ISO 16739-1:2024 (IFC 4.3)** - Industry Foundation Classes for spatial structure
- **ISO 4157** - Construction drawings designation systems (storeys, rooms)
- **VDI 3814** - German building automation standard for HVAC control
- **Project Haystack** - Semantic tagging for IoT interoperability

## Design Philosophy

### Object-Oriented vs. DataPoint-Centric

Traditional building automation systems (BACnet, Modbus) use a **DataPoint-centric** approach where measurements are separate entities linked to equipment:

```
# DataPoint-centric (traditional)
Space --> DataPoint("Temperature")
Space --> DataPoint("Humidity")
Space --> DataPoint("CO2")
```

EnergyIQ uses an **object-oriented** approach where measurements are attributes directly on the object:

```
# Object-oriented (EnergyIQ)
Space:
  temperature: 22.3
  humidity: 45.0
  co2Level: 800
```

**Benefits:**
- Natural, intuitive data model
- Hierarchical relationships are explicit
- Easier querying via GraphQL
- TimeSeries automatically supported by OctoMesh

### Standards Integration

```
                    BIM / Planning                    Operations / IoT
                         │                                │
              ┌──────────┴──────────┐         ┌──────────┴──────────┐
              │                     │         │                     │
         ISO 16739              VDI 3814              Project
           (IFC)                  (GA)               Haystack
              │                     │                    │
         "Structure"           "Functions"          "Semantics"
         What is where?        How is it            What does the
         (Rooms, Walls)        controlled?          datapoint mean?
              │                     │                    │
              └──────────┬──────────┴────────────────────┘
                         │
                         ▼
                    ┌─────────┐
                    │EnergyIQ │  ← Combines all three
                    └─────────┘
```

## Type Hierarchy

### Base Types from OctoMesh Basic Package

EnergyIQ leverages the OctoMesh Basic package for standardized tree structures. This aligns with IFC's `IfcRelAggregates` for spatial decomposition:

```
NamedEntity (Basic)                    # Provides: Name, Description
│
├── Tree (Basic)                       # Root of a tree structure
│   └── Site                           # ← IfcSite (root of spatial hierarchy)
│
├── TreeNode (Basic)                   # Node with ParentChild association
│   ├── Building → Site                # ← IfcBuilding
│   ├── BuildingStorey → Building      # ← IfcBuildingStorey
│   ├── Space → BuildingStorey         # ← IfcSpace (central object)
│   └── TechnicalSystem → Building     # HVAC equipment (abstract)
│       ├── AirHandlingUnit, Boiler, Chiller, Pump
│       └── haystackTags, haystackRefs, haystackMeta
│
└── NamedEntity (Basic)                # For non-hierarchical types
    └── BuildingElement (abstract)     # Walls, doors, windows, etc.
        ├── Wall, Door, Window, ShadingDevice, Luminaire
        └── haystackTags, haystackRefs, haystackMeta
```

### Spatial Structure (IFC 4.3)

The spatial hierarchy uses the **ParentChild** association inherited from `Basic/TreeNode`, which maps directly to IFC's `IfcRelAggregates`:

```
Site (Tree)                 ← IfcSite
  └── Building (TreeNode)   ← IfcBuilding (ParentChild → Site)
        └── BuildingStorey  ← IfcBuildingStorey (ParentChild → Building)
              └── Space     ← IfcSpace (ParentChild → BuildingStorey)
```

**Association:**
- `System/ParentChild` (inherited): Tree navigation (IFC: IfcRelAggregates)

### ISO 4157 Room Designation

EnergyIQ implements ISO 4157 for standardized room and storey designation:

#### ISO 4157-1: Storey Numbering
| Attribute | Description | Example |
|-----------|-------------|---------|
| `storeyNumber` | Consecutive number from bottom, starting with 1 | 1, 2, 3 |
| `floorDesignation` | National floor code (German convention) | EG, 1.OG, DG |

#### ISO 4157-2: Room Numbers (Daily Use)
| Attribute | Description | Example |
|-----------|-------------|---------|
| `roomNumber` | Floor prefix + 2-digit sequential number | EG01, 1OG02, DG03 |

Format: `{FloorPrefix}{2-digit number}`
- EG (Erdgeschoss): EG01, EG02, EG03...
- 1.OG (1. Obergeschoss): 1OG01, 1OG02...
- DG (Dachgeschoss): DG01, DG02...
- Nebengebäude: NG-EG01, NG-EG02...
- Außenbereich: A01, A02...

#### ISO 4157-3: Room Identifiers (Lifecycle)
| Attribute | Description | Example |
|-----------|-------------|---------|
| `roomIdentifier` | Immutable identifier: I# + storey + 3-digit seq | I#1001, I#2015 |

Format: `I#{storeyNumber}{3-digit sequence}`
- Storey 1: I#1001, I#1002, I#1003...
- Storey 2: I#2001, I#2002...
- Storey 3: I#3001, I#3002...
- Outdoor (Storey 0): I#0001, I#0002...

**Important:** Room identifiers are **immutable** throughout the building lifecycle and should never change, even during remodeling.

### Building Elements (IFC 4.3)

Physical elements contained in spaces:

| Type | IFC Mapping | Key Attributes |
|------|-------------|----------------|
| Wall | IfcWall | - |
| Door | IfcDoor | overallHeight, overallWidth, isOpen, isLocked |
| Window | IfcWindow | overallHeight, overallWidth, isOpen, openingPosition |
| ShadingDevice | IfcShadingDevice | shadingType, position, slatAngle |
| Luminaire | IfcLightFixture | luminaireType, ratedPower, isOn, dimmingLevel |

**Association:**
- `SpaceElements`: Space (1:N) ↔ BuildingElement

### Technical Systems (VDI 3814)

HVAC and building automation systems:

| Type | IFC/VDI Mapping | Key Attributes |
|------|-----------------|----------------|
| AirHandlingUnit | IfcUnitaryEquipment | supplyAirTemp, returnAirTemp, fanSpeed, coilPositions |
| Boiler | IfcBoiler | supplyTemp, returnTemp, modulationLevel, efficiency |
| Chiller | IfcChiller | supplyTemp, returnTemp, modulationLevel, powerConsumption |
| Pump | IfcPump | flowRate, pressure, speedSetpoint, powerConsumption |

**Associations:**
- `System/ParentChild`: TechnicalSystem → Building (inherited from TreeNode)
- `SystemSpaces`: TechnicalSystem (N:M) ↔ Space

### Renewable Energy Systems

Photovoltaic and energy storage systems:

| Type | IFC Mapping | Key Attributes |
|------|-------------|----------------|
| PhotovoltaicSystem | IfcSystem | totalRatedPower (kWp), totalCurrentPower (kW), gridFeedIn, selfConsumption |
| PVString | IfcSolarDevice | ratedPower (kWp), orientation, tilt, moduleCount, currentPower |
| Inverter | IfcUnitaryEquipment | ratedPower (kVA), dcPower, acPower, efficiency |
| BatteryStorage | IfcElectricFlowStorageDevice | ratedCapacity (kWh), stateOfCharge (%), chargingPower, cycleCount |

**Hierarchy:**
```
PhotovoltaicSystem (TreeNode → Building)
├── PVString (TreeNode → PhotovoltaicSystem)
├── Inverter (TreeNode → PhotovoltaicSystem)
└── BatteryStorage (TreeNode → PhotovoltaicSystem)
```

**Association:**
- `System/ParentChild`: All components linked via tree hierarchy

## Space: The Central Object

The `Space` type is the most complex, combining IFC structure with VDI 3814 room automation:

### Master Data
```yaml
spaceType: MeetingRoom      # SpaceType enum
netFloorArea: 25.0          # m²
grossFloorArea: 28.0        # m²
ceilingHeight: 2.8          # m
designOccupancy: 10         # persons
```

### Actual Values (TimeSeries)
```yaml
temperature: 22.3           # °C
humidity: 45.0              # %
co2Level: 800               # ppm
illuminance: 500            # lux
presenceDetected: true      # boolean
windowOpen: false           # boolean
```

### Setpoints
```yaml
temperatureSetpointHeating: 21.0   # °C
temperatureSetpointCooling: 24.0   # °C
illuminanceSetpoint: 500           # lux
co2Setpoint: 1000                  # ppm
```

### Control Signals
```yaml
heatingValvePosition: 45.0   # 0-100%
coolingValvePosition: 0.0    # 0-100%
ventilationLevel: 60.0       # 0-100%
lightingLevel: 80.0          # 0-100%
shadingPosition: 30.0        # 0-100%
```

### Operating Mode
```yaml
operatingMode: Comfort       # OperatingMode enum
occupancySchedule:           # ScheduleEntry[]
  - daysOfWeek: [0, 1, 2, 3, 4]  # Mon-Fri
    startTime: "08:00"
    endTime: "18:00"
    mode: Comfort
```

### Energy Consumption
```yaml
energyConsumptionHeating: 150.0   # kWh
energyConsumptionCooling: 50.0    # kWh
energyConsumptionLighting: 25.0   # kWh
energyConsumptionTotal: 225.0     # kWh
```

## Haystack Compatibility

All types support optional Haystack tagging for interoperability with tools like SkySpark and FIN Framework:

```yaml
Space:
  name: "Meeting Room 1"
  temperature: 22.3
  haystackTags: ["space", "room", "meetingRoom", "hvacZone"]
  haystackRefs:
    - refName: "siteRef"
      targetId: "site-001"
    - refName: "equipRef"
      targetId: "ahu-001"

AirHandlingUnit:
  name: "AHU-01"
  supplyAirTemp: 18.5
  haystackTags: ["ahu", "hvac", "equip", "rooftop"]
```

### Automatic Tag Mapping

| EnergyIQ Type | Haystack Tags |
|---------------|---------------|
| Site | site |
| Building | site (with building tags) |
| Space | space, hvacZone |
| AirHandlingUnit | ahu, hvac, equip |
| Boiler | boiler, hvac, equip, hot, water |
| Pump | pump, equip |

## Enumerations

### SpaceType
```
Office, MeetingRoom, Corridor, Toilet, Kitchen,
TechnicalRoom, Storage, Parking, Lobby, Staircase,
Elevator, ServerRoom, Laboratory, Workshop, Other
```

### OperatingMode (VDI 3814)
```
Comfort    - Full comfort, occupancy expected
Economy    - Reduced setpoints, lower energy use
Standby    - Minimal conditioning, ready for occupancy
Protection - Freeze/overheat protection only
Off        - System disabled
Auto       - Automatic mode selection
```

### SystemType
```
Heating, Cooling, Ventilation, Combined, Lighting, Shading
```

## Records

### Address
```yaml
street: "Technopark 1"
postalCode: "1220"
city: "Vienna"
country: "AT"
```

### ScheduleEntry
```yaml
daysOfWeek: [0, 1, 2, 3, 4]  # Monday=0 to Sunday=6
startTime: "08:00"
endTime: "18:00"
mode: Comfort
```

### HaystackRef
```yaml
refName: "equipRef"
targetId: "ahu-001"
```

## TimeSeries Support

OctoMesh automatically provides TimeSeries support for all numeric and boolean attributes. No special marking is required in the CK definition. Historical values are stored and can be queried via GraphQL:

```graphql
query {
  space(id: "space-001") {
    name
    temperature {
      current
      history(from: "2024-01-01", to: "2024-01-31") {
        timestamp
        value
      }
    }
  }
}
```

## Simulation Pipelines

EnergyIQ includes simulation pipelines for generating realistic demo data without requiring actual sensors.

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ EdgePipeline (Simulation Adapter)                           │
│                                                             │
│  FromPolling (10s) → Simulation@1 → LinearScaler → Project  │
│                          ↓                                  │
│              Math.Sinus, Math.Triangle generators           │
│                          ↓                                  │
│                 ToPipelineDataEvent                         │
└─────────────────────────────────────────────────────────────┘
                          ↓
                    Event Hub
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ MeshPipeline (Update Entities)                              │
│                                                             │
│  FromPipelineDataEvent → CreateUpdateInfo@1 → ApplyChanges  │
│                               ↓                             │
│                    Update Space, PV, HVAC attributes        │
└─────────────────────────────────────────────────────────────┘
```

### Simulation Profiles

| Attribute | Simulator | Range | Description |
|-----------|-----------|-------|-------------|
| temperature | Math.Sinus | 18-24°C | Daily cycle around setpoint |
| humidity | Math.Sinus | 35-65% | Phase-shifted from temperature |
| co2Level | Math.Triangle | 500-900 ppm | Occupancy pattern |
| illuminance | Math.Sinus | 100-700 lux | Daylight curve |
| heatingValvePosition | Math.Sinus | 20-70% | Follows temperature demand |
| ventilationLevel | Math.Sinus | 30-70% | Follows CO2 level |
| pvStringPower | Math.Sinus | 0-6 kW | Solar production curve |
| stateOfCharge | Math.Triangle | 30-90% | Battery charge/discharge |

### Simulated Entities

**Spaces (6 rooms):**
- Wohnbereich EG, Büro EG
- Schlafzimmer OG, Kinderzimmer OG
- Aufenthaltsraum DG, Büro DG 1

**PV System:**
- PhotovoltaicSystem (total power, grid feed-in, self-consumption)
- 4 PV Strings (current power per string)
- 2 Inverters (DC/AC power)
- Battery Storage (state of charge, charging power)

**HVAC:**
- Boiler/Heat Pump (supply/return temp, modulation)
- Air Handling Unit (supply air temp, fan speed)

### Configuration

The simulation runs with a 10-second polling interval. Configuration file: `data/_pipelines/rt-simulation-adapters.yaml`

#### Pipeline Definition Structure

```yaml
# EdgePipeline - Data Generation
triggers:
  - type: FromPolling@1
    interval: 00:00:10           # Polling interval (HH:MM:SS)

transformations:
  # 1. Generate base values (0-1 range)
  - type: Simulation@1
    simulations:
      - targetPath: tempBase
        simulatorKey: Math.Sinus
        configuration: "{periodMinutes:1440, phaseOffsetMinutes:360}"

  # 2. Scale to realistic ranges
  - type: LinearScaler@1
    path: $.tempBase
    targetPath: temperature
    scaleInputMin: 0
    scaleInputMax: 1
    scaleOutputMin: 18            # Output: 18-24°C
    scaleOutputMax: 24

  # 3. Send to event hub
  - type: ToPipelineDataEvent@1
```

```yaml
# MeshPipeline - Entity Updates
triggers:
  - type: FromPipelineDataEvent@1

transformations:
  # Update each entity
  - type: CreateUpdateInfo@1
    targetPath: $._updateItems
    targetValueKind: Array
    targetValueWriteMode: Append
    updateKind: UPDATE
    rtId: "6789a00000000000000011d1"    # RT-ID must be quoted!
    ckTypeId: EnergyIQ/Space
    attributeUpdates:
      - attributeName: Temperature      # PascalCase required
        attributeValueType: Double
        valuePath: $.temperature

  # Apply all updates
  - type: ApplyChanges@1
    path: _updateItems
```

#### Available Simulators

| Simulator | Description | Configuration |
|-----------|-------------|---------------|
| `Math.Sinus` | Sine wave (0-1) | `periodMinutes`, `phaseOffsetMinutes` |
| `Math.Triangle` | Triangle wave (0-1) | `periodMinutes` |
| `Math.IntRandom` | Random integer | `min`, `max` |
| `Math.Constant` | Fixed value | `value` |

#### Extending the Simulation

To add a new simulated entity:

1. Add simulation in EdgePipeline (Simulation@1):
```yaml
- targetPath: newValueBase
  simulatorKey: Math.Sinus
  configuration: "{periodMinutes:720}"
```

2. Add scaler (LinearScaler@1):
```yaml
- type: LinearScaler@1
  path: $.newValueBase
  targetPath: newValue
  scaleInputMin: 0
  scaleInputMax: 1
  scaleOutputMin: 10
  scaleOutputMax: 50
```

3. Add update in MeshPipeline (CreateUpdateInfo@1):
```yaml
- type: CreateUpdateInfo@1
  targetPath: $._updateItems
  targetValueKind: Array
  targetValueWriteMode: Append
  updateKind: UPDATE
  rtId: "your-entity-rtid"
  ckTypeId: EnergyIQ/YourType
  attributeUpdates:
    - attributeName: NewValue
      attributeValueType: Double
      valuePath: $.newValue
```

**Important Notes:**
- `rtId` values must be quoted strings (e.g., `"6789a00000000000000011d1"`)
- `attributeName` must use PascalCase matching the CK type definition's `name` field
- `attributeValueType` must match the CK attribute's `valueType`

## Demo Data: Firmianstraße 31A

The project includes a complete demo building located at Firmianstraße 31A, 5020 Salzburg, Austria.

### Building Structure

```
Site: Firmianstraße 31A (47.8055, 12.9892)
├── Hauptgebäude (Main Building)
│   ├── EG (Ground Floor)
│   │   ├── Wohnbereich (Living Area) - 45m²
│   │   ├── Büro EG (Office) - 15m²
│   │   ├── Küche (Kitchen) - 12m²
│   │   └── Flur EG (Corridor) - 8m²
│   ├── OG (Upper Floor)
│   │   ├── Schlafzimmer (Bedroom) - 18m²
│   │   ├── Badezimmer OG (Bathroom) - 8m²
│   │   ├── Kinderzimmer (Children's Room) - 14m²
│   │   └── Flur OG (Corridor) - 6m²
│   └── DG (Attic)
│       ├── Aufenthaltsraum (Living Room) - 25m²
│       ├── Büro DG 1 (Office 1) - 12m²
│       └── Büro DG 2 (Office 2) - 10m²
└── Nebengebäude (Auxiliary Building)
    └── EG
        ├── Garage - 25m²
        ├── Werkstatt (Workshop) - 15m²
        └── Waschküche (Laundry) - 10m²
```

### PV System

```
PhotovoltaicSystem (18.4 kWp total)
├── PVString Ost (East) - 4.8 kWp, 90°, 30° tilt
├── PVString Süd (South) - 6.0 kWp, 180°, 25° tilt
├── PVString NG (Auxiliary) - 4.0 kWp, 180°, 15° tilt
├── PVString Zaun (Fence) - 3.6 kWp, 180°, 90° tilt
├── Inverter 1 - 10 kVA (Ost + Süd)
├── Inverter 2 - 8 kVA (NG + Zaun)
└── BatteryStorage - 10 kWh
```

### HVAC Systems

| System | Type | Description |
|--------|------|-------------|
| Wärmepumpe | Boiler | Heat pump, supply 28-40°C |
| KWL | AirHandlingUnit | Ventilation with heat recovery, 85% efficiency |
| Heizkreispumpe | Pump | Heating circuit pump |

### RT-IDs Reference

For pipeline development, use these RT-IDs:

| Entity | RT-ID |
|--------|-------|
| Wohnbereich EG | `6789a00000000000000011d1` |
| Büro EG | `6789a00000000000000012d2` |
| Schlafzimmer OG | `6789a00000000000000021d8` |
| Kinderzimmer OG | `6789a00000000000000023da` |
| Aufenthaltsraum DG | `6789a00000000000000031de` |
| Büro DG 1 | `6789a00000000000000032df` |
| PhotovoltaicSystem | `6789a00000000000000080a1` |
| PVString Ost | `6789a00000000000000081b1` |
| PVString Süd | `6789a00000000000000082b2` |
| PVString NG | `6789a00000000000000083b3` |
| PVString Zaun | `6789a00000000000000084b4` |
| Inverter 1 | `6789a00000000000000085c1` |
| Inverter 2 | `6789a00000000000000086c2` |
| BatteryStorage | `6789a00000000000000087d1` |
| Wärmepumpe | `6789a00000000000000060f1` |
| KWL | `6789a00000000000000061f2` |

## File Organization

```
src/EnergyIqCkModel/ConstructionKit/
├── ckModel.yaml              # Model ID: EnergyIQ-1.0.0, depends on Basic
├── enums/
│   ├── spaceType.yaml
│   ├── operatingMode.yaml
│   ├── shadingType.yaml
│   ├── luminaireType.yaml
│   ├── systemType.yaml
│   └── dayOfWeek.yaml
├── records/
│   ├── address.yaml
│   ├── scheduleEntry.yaml
│   └── haystackRef.yaml
├── attributes/
│   ├── spatial.yaml          # Site, Building, Storey, Space attributes
│   ├── buildingElements.yaml # Door, Window, etc. attributes
│   ├── technicalSystems.yaml # AHU, Boiler, etc. attributes
│   ├── haystack.yaml         # Haystack tagging attributes
│   └── ...                   # Individual attribute files
├── types/
│   ├── site.yaml             # Derives from Basic/Tree
│   ├── building.yaml         # Derives from Basic/TreeNode
│   ├── buildingStorey.yaml   # Derives from Basic/TreeNode
│   ├── space.yaml            # Derives from Basic/TreeNode
│   ├── buildingElement.yaml  # Abstract, derives from Basic/NamedEntity
│   ├── wall.yaml
│   ├── door.yaml
│   ├── window.yaml
│   ├── shadingDevice.yaml
│   ├── luminaire.yaml
│   ├── technicalSystem.yaml  # Abstract, derives from Basic/TreeNode
│   ├── airHandlingUnit.yaml
│   ├── boiler.yaml
│   ├── chiller.yaml
│   ├── pump.yaml
│   ├── photovoltaicSystem.yaml  # PV system container
│   ├── pvString.yaml            # PV module string
│   ├── inverter.yaml            # DC/AC inverter
│   └── batteryStorage.yaml      # Battery storage
└── associations/
    ├── spaceElements.yaml    # Space → BuildingElement (1:N)
    └── systemSpaces.yaml     # TechnicalSystem ↔ Space (N:M)
    # Note: Spatial hierarchy + TechnicalSystem→Building uses System/ParentChild

data/
├── bim/                          # RT (Runtime) model examples
│   └── rt-firmianstrasse.yaml    # Demo: Firmianstraße 31A, Salzburg
└── _pipelines/                   # Communication pipelines
    └── rt-simulation-adapters.yaml  # Simulation pipelines for demo data
```

## Future Extensions

Potential additions for future versions:

1. **Additional HVAC Equipment**
   - Heat exchangers
   - Cooling towers
   - Variable air volume (VAV) boxes
   - Fan coil units (FCU)

2. **Energy Metering**
   - Electric meters
   - Gas meters
   - Water meters
   - Sub-metering

3. **Renewable Energy** ✓
   - ~~Photovoltaic systems~~ (implemented)
   - ~~Battery storage~~ (implemented)
   - Heat pumps

4. **Advanced Analytics**
   - Fault detection rules
   - Energy benchmarking
   - Predictive maintenance

## References

- [ISO 16739-1:2024 (IFC 4.3)](https://www.iso.org/standard/84123.html)
- [buildingSMART IFC Documentation](https://ifc43-docs.standards.buildingsmart.org/)
- [VDI 3814 Building Automation](https://www.vdi.de/richtlinien/unsere-richtlinien-highlights/vdi-3814)
- [Project Haystack](https://project-haystack.org/)
- [OctoMesh Construction Kit Documentation](https://docs.meshmakers.cloud/)
