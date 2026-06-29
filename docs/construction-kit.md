# EnergyIQ Construction Kit Specification

**Version:** EnergyIQ-2.0.0
**Depends on:** Basic-[2.0,3.0)

## Overview

The CK model is based on:
- **ISO 16739-1:2024 (IFC 4.3)** for spatial structure, equipment, sensors, actuators, and property sets (Pset_*)
- **VDI 3814** for building automation (Anlagenautomation / Raumautomation split)

Project Haystack 4 compatibility is provided through a separate projection / mapping layer documented in `haystack-integration-concept.md` — not via mixin attributes on the domain types.

This file is a reference catalog. For the design rationale see `developer-guide.md` and `space-restructuring-concept.md`.

## Type Hierarchy

```
NamedEntity (Basic)
├── Tree (Basic)
│   └── Site
├── TreeNode (Basic)                          # inherits ParentChild
│   ├── Building
│   ├── BuildingStorey
│   ├── Space
│   ├── ExternalSpace
│   └── TechnicalSystem (abstract)
│       ├── HeatPump
│       ├── Boiler
│       ├── Chiller
│       ├── Pump
│       ├── AirHandlingUnit
│       └── ThermalEnergyStorage
├── NamedEntity (Basic)
│   ├── BuildingElement (abstract)
│   │   ├── Wall, Door, Window
│   │   ├── ShadingDevice, Luminaire
│   │   ├── RoomTerminal (abstract)
│   │   │   ├── HydronicTerminal (abstract)
│   │   │   │   ├── Radiator, RadiantSurface, ChilledBeam, FanCoilUnit
│   │   │   ├── AirTerminal
│   │   │   └── ElectricHeater
│   │   ├── Sensor (abstract)
│   │   │   ├── TemperatureSensor, HumiditySensor, CO2Sensor,
│   │   │   ├── IlluminanceSensor, PresenceSensor, WindowContactSensor,
│   │   │   └── GenericSensor
│   │   └── Actuator (abstract)
│   │       ├── Valve, Damper, Dimmer, Motor
│   ├── Schedule
│   ├── DistributionSystem
│   └── PhotovoltaicSystem
│       ├── PVString
│       ├── Inverter
│       └── BatteryStorage
```

---

## Spatial Types

### Site

Site/property. Root of the spatial hierarchy. Derives from `Basic/Tree`.

IFC: `IfcSite`.

| Attribute | Type | Description |
|-----------|------|-------------|
| GlobalId | String? | IFC GlobalId (UUID) |
| LongName | String? | Long descriptive name |
| RefLatitude | Double? | Latitude (WGS84) |
| RefLongitude | Double? | Longitude (WGS84) |
| RefElevation | Double? | Elevation above sea level (m) |

Children: `Building`, `ExternalSpace` via inherited `ParentChild`.

---

### Building

Building. Derives from `Basic/TreeNode`.

IFC: `IfcBuilding`.

| Attribute | Type | Description |
|-----------|------|-------------|
| GlobalId | String? | IFC GlobalId |
| LongName | String? | |
| ElevationOfRefHeight | Double? | Reference height (m) |
| BuildingAddress | Address (Record) | Postal address |
| YearOfConstruction | Int? | |
| GrossFloorArea | Double? | Total gross floor area (m²) |

Parent: `Site` via inherited `ParentChild`.
Children: `BuildingStorey`, `TechnicalSystem`, `PhotovoltaicSystem` via inherited `ParentChild`.

---

### BuildingStorey

Storey / floor. Derives from `Basic/TreeNode`.

IFC: `IfcBuildingStorey`. ISO 4157-1.

| Attribute | Type | Description |
|-----------|------|-------------|
| GlobalId | String? | |
| LongName | String? | |
| Elevation | Double | Height above building reference (m) |
| AboveGround | Boolean? | |
| GrossFloorArea | Double? | Storey gross area (m²) |
| NetFloorArea | Double? | Storey net area (m²) |
| StoreyNumber | Int? | ISO 4157-1 consecutive number from bottom |
| FloorDesignation | String? | National code (EG, 1.OG, DG, UG) |

Parent: `Building`.
Children: `Space`, `ExternalSpace`.

---

### Space

Room. Derives from `Basic/TreeNode`. Central object of room automation.

IFC: `IfcSpace`. VDI 3814 Raumautomation. ISO 4157-2/3.

**v2 note:** Measurements (Temperature, Humidity, CO2Level, …) and control signals (HeatingValvePosition, VentilationLevel, …) no longer live on Space — they moved to dedicated `Sensor` / `Actuator` / `RoomTerminal` entities reached via the associations listed below.

| Attribute | Type | Description |
|-----------|------|-------------|
| GlobalId | String? | |
| LongName | String? | |
| RoomNumber | String? | ISO 4157-2 (e.g. "EG01") |
| RoomIdentifier | String? | ISO 4157-3 immutable (e.g. "I#1001") |
| SpaceType | Enum SpaceType? | Office, MeetingRoom, LivingRoom, Bedroom, … (extended in v2) |
| PredefinedType | String? | IFC USERDEFINED slot, free text |
| NetFloorArea | Double? | m² |
| GrossFloorArea | Double? | m² |
| CeilingHeight | Double? | m |
| ThermalRequirements | PsetSpaceThermalRequirements? | IFC Pset_* (design targets) |
| LightingRequirements | PsetSpaceLightingRequirements? | IFC Pset_* |
| OccupancyRequirements | PsetSpaceOccupancyRequirements? | IFC Pset_* |
| OperatingMode | Enum OperatingMode? | Comfort, Economy, Standby, Protection, Off, Auto |

| Association | Target | Inverse role | Mult. |
|---|---|---|---|
| ParentChild (inherited) | BuildingStorey | ContainsChild | N:1 |
| SpaceElements | BuildingElement | ContainedInSpace | 1:N |
| SpaceSensors | Sensor | SensorInSpace | 1:N |
| SpaceActuators | Actuator | ActuatorInSpace | 1:N |
| SpaceTerminals | RoomTerminal | ContainedInSpace | 1:N |
| SystemSpaces | TechnicalSystem | ServedBy | N:N |
| SpaceSchedules | Schedule | SchedulesAppliedTo | N:N |

---

### ExternalSpace

External spatial element — terrace, garden, balcony, driveway, parking surface. Derives from `Basic/TreeNode`.

IFC: `IfcExternalSpatialElement`.

Distinct from `Space` — does not carry Pset_* records or interior sensor obligations.

| Attribute | Type | Description |
|-----------|------|-------------|
| GlobalId | String? | |
| LongName | String? | |
| RoomNumber | String? | e.g. "A01" |
| RoomIdentifier | String? | e.g. "I#0001" |
| SpaceType | Enum SpaceType? | |
| PredefinedType | String? | e.g. TERRACE, GARDEN, BALCONY, ROOFTERRACE, DRIVEWAY |
| NetFloorArea | Double? | Outdoor area (m²) |

Parent: `Site`, `Building`, or `BuildingStorey` via inherited `ParentChild`.
Allowed associations: `SpaceSensors` (outdoor weather/irradiance), `SpaceElements` (outdoor luminaires, shading).

---

## Plant Equipment

All TechnicalSystem subtypes share these attributes from the abstract base:

`Identifier`, `SystemType` (Enum), `IsRunning` (Boolean), `FaultState` (Boolean).

Parent: `Building` via inherited `ParentChild`.

### HeatPump *(NEW v2)*

Reversible heat pump aggregate. Heating, active cooling, passive cooling, DHW.

IFC: `IfcUnitaryEquipment/HEATPUMP`.

| Attribute | Type | Description |
|---|---|---|
| HeatSource | Enum HeatSource? | Air, Ground, Water, Exhaust, Hybrid |
| IsReversible | Boolean? | |
| HeatingCapacity | Double? | Rated kW |
| CoolingCapacity | Double? | Rated kW |
| OperatingMode | Enum HeatPumpOperatingMode? | Off, Standby, Heating, ActiveCooling, PassiveCooling, DomesticHotWater, Defrost, Fault |
| SupplyTemp, ReturnTemp, SupplyTempSetpoint | Double? | Sink-side (°C) |
| ModulationLevel | Double? | 0-100% |
| PowerConsumption | Double? | kW |
| SourceInletTemp, SourceOutletTemp | Double? | Source-side (°C) |
| COP, SCOP, EER, SEER | Double? | Performance |

| Association | Target | Mult. |
|---|---|---|
| EquipmentSensors | Sensor | 1:N |
| EquipmentActuators | Actuator | 1:N |
| TerminalServedBy | RoomTerminal | N:N |
| SystemMembers | DistributionSystem | N:N |

---

### Boiler

Classical fuel boiler. IFC: `IfcBoiler`.

| Attribute | Type |
|---|---|
| SupplyTemp, ReturnTemp, SupplyTempSetpoint | Double? |
| ModulationLevel | Double? |
| FuelConsumption | Double? |
| Efficiency | Double? |

Same associations as HeatPump, plus `SystemSpaces`.

---

### Chiller

Cooling-only chiller. IFC: `IfcChiller`.

Attributes: SupplyTemp/ReturnTemp/SupplyTempSetpoint, ModulationLevel, PowerConsumption, Efficiency.
Associations: `EquipmentSensors`, `EquipmentActuators`, `TerminalServedBy`, `SystemMembers`, `SystemSpaces`.

---

### Pump

Circulation pump. IFC: `IfcPump`.

| Attribute | Type |
|---|---|
| FlowRate | Double? (m³/h) |
| Pressure | Double? (bar) |
| SpeedSetpoint | Double? (0-100%) |
| PowerConsumption | Double? (kW) |

Associations: `EquipmentSensors`, `EquipmentActuators`, `SystemMembers`, `SystemSpaces`.

---

### AirHandlingUnit

Central ventilation unit (KWL with heat recovery). IFC: `IfcUnitaryEquipment`.

| Attribute | Type |
|---|---|
| SupplyAirTemp, ReturnAirTemp, OutdoorAirTemp | Double? |
| SupplyAirTempSetpoint | Double? |
| SupplyAirFlow | Double? |
| FanSpeedSupply, FanSpeedReturn | Double? |
| FilterDifferentialPressure | Double? (Pa) |
| HeatRecoveryEfficiency | Double? (0-100%) |
| HeatingCoilPosition, CoolingCoilPosition | Double? |

Associations: `EquipmentSensors`, `EquipmentActuators`, `TerminalServedBy` → `AirTerminal`, `SystemMembers`, `SystemSpaces`.

---

### ThermalEnergyStorage *(NEW v2)*

Buffer tank / hot-water cylinder. IFC: `IfcTank/THERMALTANK`.

| Attribute | Type |
|---|---|
| StorageCapacity | Double? (kWh or l) |
| StorageVolume | Double? (l) |
| StorageTempTop, StorageTempMiddle, StorageTempBottom | Double? (°C, stratification) |
| ChargeLevel | Double? (0-100%) |

Associations: `EquipmentSensors`, `SystemMembers`.

---

## Room Terminals *(NEW v2 — VDI 3814 Raumautomation)*

### RoomTerminal (abstract)

Base for room-level terminal units. Derives from `BuildingElement`.

| Attribute | Type |
|---|---|
| OperatingMode | Enum TerminalOperatingMode? (Off, Heating, Cooling, Ventilating, Standby) |
| NominalPower | Double? (kW) |

| Association | Target | Mult. |
|---|---|---|
| SpaceTerminals | Space | N:1 |
| TerminalActuators | Actuator | 1:N |
| EquipmentSensors | Sensor | 1:N |
| TerminalServedBy | TechnicalSystem | N:N |
| SystemMembers | DistributionSystem | N:N |

### HydronicTerminal (abstract)

Water-based terminal. Adds: `SupplyTemp`, `ReturnTemp`, `FlowRate`, `IsReversible`.

### Radiator

Conventional water-based radiator. IFC: `IfcSpaceHeater/RADIATOR`. No extra attributes — valve modeled as separate `Valve` actuator via `TerminalActuators`.

### RadiantSurface

Underfloor / ceiling / wall radiant terminal. Can be reversible (heating + passive cooling) when `IsReversible = true`. IFC: `IfcSpaceHeater/RADIATOR` USERDEFINED.

| Attribute | Type |
|---|---|
| ActiveArea | Double? (m²) |
| MaxSupplyTemp | Double? (dew-point safety for cooling) |

### ChilledBeam

Chilled beam. IFC: `IfcCooledBeam`. Some installations support heating — set `IsReversible = true`.

### FanCoilUnit

Forced-air + hydronic coil(s). IFC: `IfcUnitaryEquipment + IfcFan`.

| Attribute | Type |
|---|---|
| HeatingValvePosition, CoolingValvePosition | Double? (0-100%, for 4-pipe) |
| FanSpeed, FanSpeedSetpoint | Double? (0-100%) |
| FanStage | Int? (0-3 or 0-5) |

Individual valves can additionally be modeled as separate `Valve` actuators via `TerminalActuators`.

### AirTerminal

VAV/CAV box, diffuser, grille. IFC: `IfcAirTerminal` / `IfcAirTerminalBox`.

| Attribute | Type |
|---|---|
| BoxType | String? (VAV, CAV, Diffuser, Grille) |
| AirflowRate | Double? (m³/h) |
| AirflowSetpoint | Double? (m³/h) |

Damper modeled as separate `Damper` actuator via `TerminalActuators`.

### ElectricHeater

Electric room heater — convector, IR panel, towel rail. IFC: `IfcSpaceHeater/CONVECTOR` ELECTRIC USERDEFINED.

| Attribute | Type |
|---|---|
| PowerConsumption | Double? |
| PowerLevel | Double? (0-100% — uses ModulationLevel attribute) |

---

## Sensors *(NEW v2 — IfcSensor)*

### Sensor (abstract)

Base for all sensor entities. Derives from `BuildingElement`.

Shared attributes: `Manufacturer`, `Model`, `SerialNumber`, `Accuracy`, `LastUpdate`, `IsFaulty`.

| Association | Target | Mult. |
|---|---|---|
| SpaceSensors | Space | N:1 |
| EquipmentSensors | BuildingElement (equipment or terminal) | N:1 |

### Concrete Subtypes

Each concrete sensor subtype exposes its measurement under the unified attribute name `CurrentValue` (display name), backed by a typed global attribute:

| Subtype | CurrentValue type | Unit | IFC PredefinedType |
|---|---|---|---|
| TemperatureSensor | Double | °C | TEMPERATURESENSOR |
| HumiditySensor | Double | %RH | HUMIDITYSENSOR |
| CO2Sensor | Double | ppm | CO2SENSOR |
| IlluminanceSensor | Double | lux | LIGHTSENSOR |
| PresenceSensor | Boolean | — | MOVEMENTSENSOR |
| WindowContactSensor | Boolean (true=closed) | — | CONTACTSENSOR |
| GenericSensor | String | (carrier) | USERDEFINED |

The underlying global attribute IDs are unchanged from v1 (`Temperature`, `Humidity`, `CO2Level`, …) — only the `name` at the type is `CurrentValue`. Archive `Path` references the name; mapping config and pipelines reference the id.

---

## Actuators *(NEW v2 — IfcActuator)*

### Actuator (abstract)

Base for all actuators. Derives from `BuildingElement`.

Shared attributes: `Manufacturer`, `Model`, `LastUpdate`, `IsFaulty`.

| Association | Target | Mult. |
|---|---|---|
| SpaceActuators | Space | N:1 |
| EquipmentActuators | BuildingElement | N:1 |
| TerminalActuators | RoomTerminal | N:1 |

### Valve

IFC: `IfcValve`.

| Attribute | Type |
|---|---|
| ValveType | Enum ValveType? (Heating, Cooling, Reversible, ChangeoverHeatingCooling, Mixing, Bypass, Isolation) |
| Position | Double? (0-100%) |
| PositionSetpoint | Double? |

### Damper

IFC: `IfcDamper`. Attributes: `Position`, `PositionSetpoint`, `AirflowRate`, `AirflowSetpoint`.

### Dimmer

Standalone lighting dimmer / electronic ballast. For dimming as a property of a Luminaire, use `Luminaire.DimmingLevel` directly. Attributes: `Level`, `LevelSetpoint`.

### Motor

Variable-speed drive — shading drives, pump VSDs, fan motors. Attributes: `State`, `Speed`, `SpeedSetpoint`, `PowerConsumption`.

---

## Building Elements (lightly changed)

### Wall

IFC: `IfcWall`. No attributes beyond inherited `Name`, `Description`, `GlobalId`, `ObjectType`. Association: `SpaceElements`.

### Door

IFC: `IfcDoor`.

| Attribute | Type |
|---|---|
| OverallHeight, OverallWidth | Double? (mm) |
| IsExternal, IsOpen, IsLocked | Boolean? |

Associations: `SpaceElements`, `EquipmentSensors` (contact sensor).

### Window

IFC: `IfcWindow`.

| Attribute | Type |
|---|---|
| OverallHeight, OverallWidth | Double? (mm) |
| IsOpen | Boolean? |
| OpeningPosition | Double? (0-100%) |

Associations: `SpaceElements`, `EquipmentSensors` (contact sensor).

### ShadingDevice

IFC: `IfcShadingDevice`.

| Attribute | Type |
|---|---|
| ShadingType | Enum ShadingType? (Blind, Shutter, Awning, Screen, Curtain) |
| Position, SlatAngle | Double? |
| PositionSetpoint, SlatAngleSetpoint | Double? |

Associations: `SpaceElements`, `EquipmentActuators` (motor).

### Luminaire

IFC: `IfcLightFixture`.

| Attribute | Type |
|---|---|
| LuminaireType | Enum LuminaireType? |
| RatedPower | Double? (W) |
| IsOn | Boolean? |
| DimmingLevel | Double? (0-100%) |
| DimmingLevelSetpoint | Double? |

Associations: `SpaceElements`, `EquipmentActuators` (dimmer).

---

## Supporting Types *(NEW v2)*

### Schedule

Operational schedule shared across spaces. IFC analog: `IfcWorkSchedule`.

| Attribute | Type |
|---|---|
| ScheduleType | Enum ScheduleType? (Occupancy, Heating, Cooling, Ventilation, Lighting, Shading, Custom) |
| Entries | RecordArray<ScheduleEntry> |
| IsActive | Boolean? |
| ValidFrom, ValidTo | DateTime? |

Association: `SpaceSchedules` → Space (N:N).

### DistributionSystem

Logical grouping of equipment belonging to one distribution system. IFC: `IfcDistributionSystem`.

| Attribute | Type |
|---|---|
| SystemType | Enum DistributionSystemType? (Heating, Cooling, Ventilation, Electrical, Sanitary, DomesticHotWater, DomesticColdWater, Drainage, Communication, Solar, Other) |
| TotalEnergyConsumed | Double? (kWh) |
| TotalEnergyDelivered | Double? (kWh) |
| SystemEfficiency | Double? |
| LastEnergyUpdate | DateTime? |

Association: `SystemMembers` → NamedEntity (N:N, polymorphic — equipment, terminals, sensors).

---

## Photovoltaic System (unchanged)

### PhotovoltaicSystem (TreeNode)

Container. Parent: `Building`.

| Attribute | Type |
|---|---|
| Identifier | String |
| TotalRatedPowerKWp | Double? |
| TotalCurrentPowerKW | Double? |
| TotalEnergyProducedKWh | Double? |
| GridFeedIn, SelfConsumption | Double? (kW) |
| IsRunning, FaultState | Boolean? |

### PVString (TreeNode → PhotovoltaicSystem)

Group of connected PV modules. IFC: `IfcSolarDevice`.

| Attribute | Type |
|---|---|
| Identifier, ModuleType | String? |
| RatedPowerKWp | Double? |
| Orientation, Tilt | Double? (degrees) |
| ModuleCount | Int? |
| CurrentPowerKW, EnergyProducedKWh, DcVoltage | Double? |
| IsRunning, FaultState | Boolean? |

### Inverter (TreeNode → PhotovoltaicSystem)

| Attribute | Type |
|---|---|
| Identifier | String |
| RatedPowerKVA | Double? |
| DcPower, AcPower | Double? (kW) |
| DcVoltage, AcVoltage | Double? (V) |
| Frequency | Double? (Hz) |
| InverterEfficiency | Double? (%) |
| Temperature | Double? (°C) |
| IsRunning, FaultState | Boolean? |

### BatteryStorage (TreeNode → PhotovoltaicSystem)

IFC: `IfcElectricFlowStorageDevice`.

| Attribute | Type |
|---|---|
| Identifier | String |
| RatedCapacityKWh, MaxChargePower, MaxDischargePower | Double? |
| StateOfCharge | Double? (0-100%) |
| ChargingPower, DischargingPower | Double? (kW) |
| BatteryTemperature | Double? (°C) |
| CycleCount | Int? |
| IsCharging, IsDischarging, IsRunning, FaultState | Boolean? |

---

## Associations Summary

| Association role | Inbound name | Outbound name | Mult. | Description |
|---|---|---|---|---|
| ParentChild (Basic) | — | — | 1:N | Spatial / tree hierarchy |
| SpaceElements | ContainedElements | ContainedInSpace | 1:N | BuildingElement in Space |
| StoreyElements | ContainedStoreyElements | ContainedInStorey | 1:N | Whole-floor sub-meter in BuildingStorey (2.3.0) |
| SpaceSensors | ContainedSensors | SensorInSpace | 1:N | Sensor in Space |
| SpaceActuators | ContainedActuators | ActuatorInSpace | 1:N | Actuator in Space |
| SpaceTerminals | ContainedTerminals | ContainedInSpace | 1:N | RoomTerminal in Space |
| EquipmentSensors | AttachedSensors | AttachedToEquipment | 1:N | Sensor on plant equipment |
| EquipmentActuators | AttachedActuators | AttachedToEquipment | 1:N | Actuator on plant equipment |
| TerminalActuators | TerminalActuators | AttachedToTerminal | 1:N | Actuator on terminal |
| SystemMembers | SystemMembers | MemberOfSystem | N:N | DistributionSystem members |
| SpaceSchedules | SchedulesAppliedTo | UsedSchedules | N:N | Schedule ↔ Space |
| TerminalServedBy | ServesTerminals | ServedBy | N:N | Plant ↔ Terminal |
| SystemSpaces (legacy) | ServesSpaces | ServedBy | N:N | Plant ↔ Space (high-level) |

---

## Enumerations

### SpaceType (extended in v2)
| Key | Name | Note |
|---|---|---|
| 0 | Office | |
| 1 | MeetingRoom | |
| 2 | Corridor | |
| 3 | Toilet | |
| 4 | Kitchen | |
| 5 | TechnicalRoom | |
| 6 | Storage | |
| 7 | Parking | |
| 8 | Lobby | |
| 9 | Staircase | |
| 10 | Elevator | |
| 11 | ServerRoom | |
| 12 | Laboratory | |
| 13 | Workshop | |
| 14 | Other | |
| 15 | LivingRoom | NEW v2 |
| 16 | Bedroom | NEW v2 |
| 17 | Bathroom | NEW v2 |
| 18 | DiningRoom | NEW v2 |
| 19 | Lounge | NEW v2 |
| 20 | ChildrensRoom | NEW v2 |
| 21 | GuestRoom | NEW v2 |
| 22 | Laundry | NEW v2 |
| 23 | Garage | NEW v2 |
| 24 | WalkInCloset | NEW v2 |
| 25 | Anteroom | NEW v2 |

### OperatingMode (VDI 3814, unchanged)
0=Comfort, 1=Economy, 2=Standby, 3=Protection, 4=Off, 5=Auto

### HeatPumpOperatingMode *(NEW v2)*
0=Off, 1=Standby, 2=Heating, 3=ActiveCooling, 4=PassiveCooling, 5=DomesticHotWater, 6=Defrost, 7=Fault

### HeatSource *(NEW v2)*
0=Air, 1=Ground, 2=Water, 3=Exhaust, 4=Hybrid

### ValveType *(NEW v2)*
0=Heating, 1=Cooling, 2=Reversible, 3=ChangeoverHeatingCooling, 4=Mixing, 5=Bypass, 6=Isolation

### TerminalOperatingMode *(NEW v2)*
0=Off, 1=Heating, 2=Cooling, 3=Ventilating, 4=Standby

### ScheduleType *(NEW v2)*
0=Occupancy, 1=Heating, 2=Cooling, 3=Ventilation, 4=Lighting, 5=Shading, 6=Custom

### DistributionSystemType *(NEW v2)*
0=Heating, 1=Cooling, 2=Ventilation, 3=Electrical, 4=Sanitary, 5=DomesticHotWater, 6=DomesticColdWater, 7=Drainage, 8=Communication, 9=Solar, 10=Other

### SystemType (unchanged)
0=Heating, 1=Cooling, 2=Ventilation, 3=Combined, 4=Lighting, 5=Shading

### ShadingType (unchanged)
0=Blind, 1=Shutter, 2=Awning, 3=Screen, 4=Curtain

### LuminaireType (unchanged)
0=Ceiling, 1=Pendant, 2=Recessed, 3=Wall, 4=Floor, 5=Desk, 6=Emergency

### DayOfWeek (unchanged)
0=Monday … 6=Sunday

---

## Records

### Address (unchanged)
| Field | Type |
|---|---|
| Street | String |
| PostalCode | String |
| City | String |
| Country | String |

### ScheduleEntry (unchanged)
| Field | Type | Note |
|---|---|---|
| DaysOfWeek | IntArray | Use bracket-string `'[0, 1, 2, 3, 4]'` when nested in RecordArray |
| StartTime | String | "HH:MM" |
| EndTime | String | |
| Mode | Enum OperatingMode | |

### PsetSpaceThermalRequirements *(NEW v2 — IFC Pset_SpaceThermalRequirements)*
| Field | Type |
|---|---|
| SpaceTemperature, SpaceTemperatureMin, SpaceTemperatureMax | Double? |
| SpaceHumidity, SpaceHumidityMin, SpaceHumidityMax | Double? |
| CO2SetpointMax | Double? |

### PsetSpaceLightingRequirements *(NEW v2 — IFC Pset_SpaceLightingRequirements)*
| Field | Type |
|---|---|
| IlluminanceTarget, IlluminanceMin | Double? |
| ArtificialLighting, NaturalLighting | Boolean? |

### PsetSpaceOccupancyRequirements *(NEW v2 — IFC Pset_SpaceOccupancyRequirements)*
| Field | Type |
|---|---|
| OccupancyType | String? (free text — living, sleeping, office, transient) |
| OccupancyNumberPeak | Int? |
| AreaPerOccupant, OccupancyTimePerDay | Double? |

---

## Notes on attribute IDs vs. names

CK distinguishes between **global attribute id** and **display name at the type**. Many shared attributes (e.g. `Temperature`, `Humidity`, `CO2Level`) are defined once globally and re-exposed under different display names at different types. v2 makes this distinction visible:

- At `TemperatureSensor`: id = `EnergyIQ/Temperature`, name = `CurrentValue`
- At `HumiditySensor`: id = `EnergyIQ/Humidity`, name = `CurrentValue`
- At `Valve`: id = `EnergyIQ/ValvePosition`, name = `Position`
- At `Damper`: id = `EnergyIQ/DamperPosition`, name = `Position`

Consequences:
- Pipeline `attributeUpdates` reference the **name** at the type (e.g. `Temperature` at TemperatureSensor — the attribute is registered under that name there)
- Archive `Column.Path` references the **name** at the target type (`CurrentValue` for all Sensor archives)
- GraphQL queries use the **name** at the type
- DataPointMapping `TargetAttributePath` references the **name** at the type
