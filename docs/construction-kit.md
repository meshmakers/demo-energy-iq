# EnergyIQ Construction Kit Specification

## Overview

The CK model is based on:
- **ISO 16739-1:2024 (IFC 4.3)** for spatial structure
- **VDI 3814** for building automation
- **Project Haystack** for semantic interoperability (optional)
- **OO principle**: Measurements as attributes, not as separate entities

## Type Hierarchy

```
Entity (System)
├── SpatialElement (abstract)
│   ├── Site
│   ├── Building
│   ├── BuildingStorey
│   └── Space
├── BuildingElement (abstract)
│   ├── Wall
│   ├── Door
│   ├── Window
│   ├── ShadingDevice
│   └── Luminaire
└── TechnicalSystem (abstract)
    ├── HVACSystem
    ├── AirHandlingUnit
    ├── Boiler
    ├── Chiller
    └── Pump
```

---

## Types

### SpatialElement (abstract)

Base class for all spatial elements.

| Attribute | Type | Description |
|-----------|------|-------------|
| GlobalId | String | Unique ID (UUID) |
| Name | String | Short name |
| Description | String? | Description |
| LongName | String? | Full name |

---

### Site

Site/property. Root of the spatial hierarchy.

| Attribute | Type | Description |
|-----------|------|-------------|
| RefLatitude | Float? | Latitude |
| RefLongitude | Float? | Longitude |
| RefElevation | Float? | Elevation above sea level |

| Association | Target | Multiplicity | Description |
|-------------|--------|--------------|-------------|
| buildings | Building | 1:N | Buildings on the site |

---

### Building

Building.

| Attribute | Type | Description |
|-----------|------|-------------|
| ElevationOfRefHeight | Float? | Reference height |
| BuildingAddress | Address (Record) | Address |
| YearOfConstruction | Int? | Year of construction |
| GrossFloorArea | Float? | Total gross floor area |

| Association | Target | Multiplicity | Description |
|-------------|--------|--------------|-------------|
| storeys | BuildingStorey | 1:N (ordered) | Storeys |
| systems | TechnicalSystem | 1:N | Building services systems |

---

### BuildingStorey

Storey/floor.

| Attribute | Type | Description |
|-----------|------|-------------|
| Elevation | Float | Height above building reference |
| AboveGround | Boolean | Above ground? |
| GrossFloorArea | Float? | Gross floor area of storey |
| NetFloorArea | Float? | Net floor area of storey |

| Association | Target | Multiplicity | Description |
|-------------|--------|--------------|-------------|
| spaces | Space | 1:N | Rooms on the storey |

---

### Space

Room – the central object for energy optimization.

**Master Data:**

| Attribute | Type | Description |
|-----------|------|-------------|
| SpaceType | SpaceTypeEnum | Room type |
| NetFloorArea | Float? | Net floor area m² |
| GrossFloorArea | Float? | Gross floor area m² |
| CeilingHeight | Float? | Room height m |
| DesignOccupancy | Int? | Planned occupancy |

**Actual Values (TimeSeries):**

| Attribute | Type | Unit | Description |
|-----------|------|------|-------------|
| Temperature | Float? | °C | Room temperature |
| Humidity | Float? | % | Relative humidity |
| CO2Level | Float? | ppm | CO₂ concentration |
| Illuminance | Float? | lx | Illuminance level |
| PresenceDetected | Boolean? | - | Presence detected |
| WindowOpen | Boolean? | - | Window open |

**Setpoints:**

| Attribute | Type | Unit | Description |
|-----------|------|------|-------------|
| TemperatureSetpointHeating | Float? | °C | Heating setpoint |
| TemperatureSetpointCooling | Float? | °C | Cooling setpoint |
| IlluminanceSetpoint | Float? | lx | Illuminance setpoint |
| CO2Setpoint | Float? | ppm | CO₂ setpoint |

**Control Outputs (TimeSeries):**

| Attribute | Type | Unit | Description |
|-----------|------|------|-------------|
| HeatingValvePosition | Float? | % | Heating valve 0-100 |
| CoolingValvePosition | Float? | % | Cooling valve 0-100 |
| VentilationLevel | Float? | % | Ventilation level |
| LightingLevel | Float? | % | Lighting 0-100 |
| ShadingPosition | Float? | % | Shading 0-100 |

**Operating Mode:**

| Attribute | Type | Description |
|-----------|------|-------------|
| OperatingMode | OperatingModeEnum | Current mode |
| OccupancySchedule | ScheduleEntry[] | Occupancy schedule |

**Energy Metrics (Aggregated):**

| Attribute | Type | Unit | Description |
|-----------|------|------|-------------|
| EnergyConsumptionHeating | Float? | kWh | Heating energy (period) |
| EnergyConsumptionCooling | Float? | kWh | Cooling energy (period) |
| EnergyConsumptionLighting | Float? | kWh | Lighting energy (period) |
| EnergyConsumptionTotal | Float? | kWh | Total (period) |

| Association | Target | Multiplicity | Description |
|-------------|--------|--------------|-------------|
| containedElements | BuildingElement | 1:N | Elements in the room |
| servedBy | TechnicalSystem | N:M | Serving systems |

---

### BuildingElement (abstract)

Base class for building elements.

| Attribute | Type | Description |
|-----------|------|-------------|
| GlobalId | String | Unique ID |
| Name | String | Designation |
| ObjectType | String? | Type description |

| Association | Target | Multiplicity | Description |
|-------------|--------|--------------|-------------|
| containedInSpace | Space | N:1 | Room assignment |

---

### Door

Door with state attributes.

| Attribute | Type | Description |
|-----------|------|-------------|
| OverallHeight | Float | Height mm |
| OverallWidth | Float | Width mm |
| IsExternal | Boolean | External door? |
| IsOpen | Boolean? | Open? (TimeSeries) |
| IsLocked | Boolean? | Locked? (TimeSeries) |

---

### Window

Window with state attributes.

| Attribute | Type | Description |
|-----------|------|-------------|
| OverallHeight | Float | Height mm |
| OverallWidth | Float | Width mm |
| IsOpen | Boolean? | Open? (TimeSeries) |
| OpeningPosition | Float? | Opening degree % |

---

### ShadingDevice

Sun protection (venetian blind, roller shutter, awning).

| Attribute | Type | Description |
|-----------|------|-------------|
| ShadingType | ShadingTypeEnum | Type |
| Position | Float? | Actual position % (TimeSeries) |
| SlatAngle | Float? | Slat angle ° |
| PositionSetpoint | Float? | Position setpoint % |
| SlatAngleSetpoint | Float? | Angle setpoint ° |

---

### Luminaire

Light fixture.

| Attribute | Type | Description |
|-----------|------|-------------|
| LuminaireType | LuminaireTypeEnum | Luminaire type |
| RatedPower | Float | Rated power W |
| IsOn | Boolean? | On? (TimeSeries) |
| DimmingLevel | Float? | Dimming level % (TimeSeries) |
| DimmingLevelSetpoint | Float? | Dimming setpoint % |

---

### TechnicalSystem (abstract)

Base class for building services systems.

| Attribute | Type | Description |
|-----------|------|-------------|
| Identifier | String | System identifier |
| Name | String | Designation |
| SystemType | SystemTypeEnum | System type |
| IsRunning | Boolean? | Running? (TimeSeries) |
| FaultState | Boolean? | Fault? (TimeSeries) |

| Association | Target | Multiplicity | Description |
|-------------|--------|--------------|-------------|
| servesSpaces | Space | N:M | Served rooms |
| containedInBuilding | Building | N:1 | Building assignment |

---

### AirHandlingUnit

Air handling unit (AHU).

| Attribute | Type | Unit | Description |
|-----------|------|------|-------------|
| SupplyAirTemp | Float? | °C | Supply air temperature |
| ReturnAirTemp | Float? | °C | Return air temperature |
| OutdoorAirTemp | Float? | °C | Outdoor air temperature |
| SupplyAirTempSetpoint | Float? | °C | Supply air setpoint |
| SupplyAirFlow | Float? | m³/h | Supply air flow rate |
| FanSpeedSupply | Float? | % | Supply fan speed |
| FanSpeedReturn | Float? | % | Return fan speed |
| FilterDifferentialPressure | Float? | Pa | Filter differential pressure |
| HeatRecoveryEfficiency | Float? | % | Heat recovery efficiency |
| HeatingCoilPosition | Float? | % | Heating coil position |
| CoolingCoilPosition | Float? | % | Cooling coil position |

---

### Boiler

Boiler.

| Attribute | Type | Unit | Description |
|-----------|------|------|-------------|
| SupplyTemp | Float? | °C | Supply temperature |
| ReturnTemp | Float? | °C | Return temperature |
| SupplyTempSetpoint | Float? | °C | Supply temperature setpoint |
| ModulationLevel | Float? | % | Modulation level |
| FuelConsumption | Float? | kWh | Consumption (TimeSeries) |
| Efficiency | Float? | % | Efficiency |

---

### Pump

Pump.

| Attribute | Type | Unit | Description |
|-----------|------|------|-------------|
| FlowRate | Float? | m³/h | Flow rate |
| Pressure | Float? | bar | Pressure |
| SpeedSetpoint | Float? | % | Speed setpoint |
| PowerConsumption | Float? | kW | Power consumption |

---

## Enums

### SpaceTypeEnum
```
Office, MeetingRoom, Corridor, Toilet, Kitchen,
TechnicalRoom, Storage, Parking, Lobby, Staircase,
Elevator, ServerRoom, Laboratory, Workshop, Other
```

### OperatingModeEnum
```
Comfort, Economy, Standby, Protection, Off, Auto
```

### ShadingTypeEnum
```
Blind, Shutter, Awning, Screen, Curtain
```

### LuminaireTypeEnum
```
Ceiling, Pendant, Recessed, Wall, Floor, Desk, Emergency
```

### SystemTypeEnum
```
Heating, Cooling, Ventilation, Combined, Lighting, Shading
```

---

## Records

### Address
```yaml
attributes:
  - Street: String
  - PostalCode: String
  - City: String
  - Country: String
```

### ScheduleEntry
```yaml
attributes:
  - DaysOfWeek: DayOfWeekEnum[]   # Mon, Tue, ...
  - StartTime: String             # HH:mm
  - EndTime: String               # HH:mm
  - Mode: OperatingModeEnum
```

---

## Associations

### Spatial Hierarchy

```
Site ──(1:N)──► Building ──(1:N)──► BuildingStorey ──(1:N)──► Space
```

### Element Containment

```
Space ◄──(N:1)── BuildingElement
```

### Building Services Supply

```
Space ◄──(N:M)──► TechnicalSystem
```

### Building Assignment

```
Building ◄──(N:1)── TechnicalSystem
```

---

## TimeSeries Attributes

The following attributes should be maintained as TimeSeries (historical values):

**Space:**
- Temperature, Humidity, CO2Level, Illuminance, PresenceDetected
- HeatingValvePosition, CoolingValvePosition, VentilationLevel, LightingLevel
- EnergyConsumption*

**BuildingElements:**
- Door.IsOpen, Door.IsLocked
- Window.IsOpen
- ShadingDevice.Position
- Luminaire.IsOn, Luminaire.DimmingLevel

**TechnicalSystems:**
- IsRunning, FaultState
- All temperature and flow values
- Energy consumption values

---

## Haystack Compatibility (Optional)

For interoperability with Haystack-based tools (SkySpark, FIN Framework, etc.), optional Haystack attributes can be maintained.

### HaystackTaggable (Mixin)

Can be applied to all relevant types:

| Attribute | Type | Description |
|-----------|------|-------------|
| haystackTags | String[] | Haystack marker tags |
| haystackRefs | HaystackRef[] (Record) | References to other entities |
| haystackMeta | Map<String, Any>? | Additional Haystack metadata |

### HaystackRef (Record)
```yaml
attributes:
  - refName: String      # e.g., "equipRef", "spaceRef", "siteRef"
  - targetId: String     # GlobalId of the target object
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
| Temperature (Attr) | temp, sensor, point |
| TemperatureSetpoint | temp, sp, point |
| HeatingValvePosition | valve, cmd, point, hot, water |

### Example with Haystack Tags

```yaml
- type: AirHandlingUnit
  globalId: "ahu-001"
  name: "Central AHU"
  supplyAirTemp: 18.5
  # Haystack compatibility
  haystackTags: ["ahu", "hvac", "equip", "rooftop"]
  haystackRefs:
    - refName: "siteRef"
      targetId: "site-001"
    - refName: "spaceRef"
      targetId: "space-001"
```

---

## Example Instance

```yaml
# Site
- type: Site
  globalId: "site-001"
  name: "Main Site Vienna"
  refLatitude: 48.2082
  refLongitude: 16.3738

# Building
- type: Building
  globalId: "bldg-001"
  name: "Office Building A"
  yearOfConstruction: 2020
  grossFloorArea: 5000
  buildingAddress:
    street: "Technopark 1"
    postalCode: "1220"
    city: "Vienna"
    country: "AT"

# BuildingStorey
- type: BuildingStorey
  globalId: "storey-gf"
  name: "GF"
  elevation: 0.0
  aboveGround: true

# Space
- type: Space
  globalId: "space-001"
  name: "Meeting Room 1"
  spaceType: MeetingRoom
  netFloorArea: 25.0
  ceilingHeight: 2.8
  designOccupancy: 10
  temperatureSetpointHeating: 21.0
  temperatureSetpointCooling: 24.0
  illuminanceSetpoint: 500
  # Actual values via TimeSeries
  temperature: 22.3
  humidity: 45.0
  presenceDetected: true
```
