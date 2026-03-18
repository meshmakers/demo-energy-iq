# EnergyIQ Construction Kit Implementation Plan

## Phase 1: Base Types ✓

### 1.1 Create Enums
- [x] `enums/spaceType.yaml` – SpaceTypeEnum
- [x] `enums/operatingMode.yaml` – OperatingModeEnum
- [x] `enums/shadingType.yaml` – ShadingTypeEnum
- [x] `enums/luminaireType.yaml` – LuminaireTypeEnum
- [x] `enums/systemType.yaml` – SystemTypeEnum
- [x] `enums/dayOfWeek.yaml` – DayOfWeekEnum

### 1.2 Create Records
- [x] `records/address.yaml` – Address
- [x] `records/scheduleEntry.yaml` – ScheduleEntry

### 1.3 Create Base Attributes
- [x] `attributes/globalId.yaml` – String
- [x] `attributes/name.yaml` – String
- [x] `attributes/description.yaml` – String (optional)
- [x] `attributes/temperature.yaml` – Double
- [x] `attributes/percentage.yaml` – Double (0-100)
- [x] `attributes/area.yaml` – Double (m²)
- [x] `attributes/length.yaml` – Double (mm)
- [x] `attributes/energy.yaml` – Double (kWh)

---

## Phase 2: Spatial Structure ✓

### 2.1 Abstract Base Types
- [x] `types/spatialElement.yaml` – SpatialElement (abstract)

### 2.2 Concrete Spatial Types
- [x] `types/site.yaml` – Site
- [x] `types/building.yaml` – Building
- [x] `types/buildingStorey.yaml` – BuildingStorey
- [x] `types/space.yaml` – Space (with all attributes)

### 2.3 Associations
- [x] `associations/siteBuildings.yaml` – Site → Building (1:N)
- [x] `associations/buildingStoreys.yaml` – Building → Storey (1:N)
- [x] `associations/storeySpaces.yaml` – Storey → Space (1:N)

---

## Phase 3: Building Elements ✓

### 3.1 Abstract Base
- [x] `types/buildingElement.yaml` – BuildingElement (abstract)

### 3.2 Concrete Elements
- [x] `types/wall.yaml` – Wall
- [x] `types/door.yaml` – Door
- [x] `types/window.yaml` – Window
- [x] `types/shadingDevice.yaml` – ShadingDevice
- [x] `types/luminaire.yaml` – Luminaire

### 3.3 Associations
- [x] `associations/spaceElements.yaml` – Space → BuildingElement (1:N)

---

## Phase 4: Technical Systems ✓

### 4.1 Abstract Base
- [x] `types/technicalSystem.yaml` – TechnicalSystem (abstract)

### 4.2 Concrete Systems
- [x] `types/airHandlingUnit.yaml` – AirHandlingUnit
- [x] `types/boiler.yaml` – Boiler
- [x] `types/chiller.yaml` – Chiller
- [x] `types/pump.yaml` – Pump

### 4.3 Associations
- [x] `associations/systemSpaces.yaml` – TechnicalSystem ↔ Space (N:M)
- [x] `associations/buildingSystems.yaml` – Building → TechnicalSystem (1:N)

---

## Phase 5: Haystack Compatibility ✓

- [x] `records/haystackRef.yaml` – HaystackRef Record
- [x] `attributes/haystack.yaml` – HaystackTags, HaystackMeta
- [x] `attributes/haystackRefs.yaml` – HaystackRefs (RecordArray)
- [x] Haystack attributes added to abstract base types (SpatialElement, BuildingElement, TechnicalSystem)

---

## Phase 6: Validation & Testing

- [x] `ckModel.yaml` updated (modelId: EnergyIQ-1.0.0)
- [x] Build executed – successful
- [x] Sample data created – `data/bim/rt-firmianstrasse.yaml` (Firmianstraße 31A, Salzburg)
- [ ] Test GraphQL queries

---

## Phase 7: TreeNode Migration ✓

Migration to OctoMesh Basic Package for standardized tree structures (corresponds to IFC IfcRelAggregates):

### 7.1 Dependencies Updated
- [x] `ckModel.yaml` – Added dependency on `Basic-[2.0,3.0)`
- [x] `EnergyIqCkModel.csproj` – PackageReference to `Meshmakers.Octo.Sdk.Packages.Basic`
- [x] `GlobalUsings.cs` – `using Meshmakers.Octo.Sdk.Packages.Basic.Generated.Basic.v2`

### 7.2 Spatial Types Migrated to TreeNode
- [x] `types/spatialElement.yaml` – REMOVED (replaced by TreeNode inheritance)
- [x] `types/site.yaml` – Now derives from `Basic/Tree`
- [x] `types/building.yaml` – Now derives from `Basic/TreeNode` (ParentChild inherited)
- [x] `types/buildingStorey.yaml` – Now derives from `Basic/TreeNode`
- [x] `types/space.yaml` – Now derives from `Basic/TreeNode`

### 7.3 Other Types Migrated to NamedEntity
- [x] `types/buildingElement.yaml` – Now derives from `Basic/NamedEntity`
- [x] `types/technicalSystem.yaml` – Now derives from `Basic/NamedEntity`

### 7.4 Attributes Cleaned Up
- [x] `attributes/name.yaml` – REMOVED (inherited from NamedEntity)
- [x] `attributes/description.yaml` – REMOVED (inherited from NamedEntity)

### 7.5 RT Sample Updated
- [x] `EnergyIQ/Name` → `System/Name`
- [x] `EnergyIQ/Description` → `System/Description`
- [x] rtIds corrected: Schema requires exactly 24 hexadecimal characters (`^[0-9a-fA-F]{24}$`)
  - Old IDs like `6789a000000000000000site` contained invalid characters (s, i, t, e)
  - New IDs are purely hexadecimal: `6789a00000000000000000a1`

### 7.6 Redundant Associations Removed
- [x] `associations/siteBuildings.yaml` – REMOVED (System/ParentChild suffices)
- [x] `associations/buildingStoreys.yaml` – REMOVED (System/ParentChild suffices)
- [x] `associations/storeySpaces.yaml` – REMOVED (System/ParentChild suffices)
- [x] `associations/buildingSystems.yaml` – REMOVED (System/ParentChild suffices)
- [x] `types/technicalSystem.yaml` – Now derives from `Basic/TreeNode` (instead of NamedEntity)
- [x] RT Sample: all hierarchy associations → `System/ParentChild`

---

## Phase 8: Renewable Energy Systems ✓

### 8.1 New Attributes
- [x] `attributes/photovoltaic.yaml` – PV, Inverter, Battery attributes

### 8.2 New Types (all TreeNode)
- [x] `types/photovoltaicSystem.yaml` – PV system container
- [x] `types/pvString.yaml` – PV string (module group)
- [x] `types/inverter.yaml` – Inverter
- [x] `types/batteryStorage.yaml` – Battery storage

### 8.3 RT Sample Extended
- [x] PV system with 18.4 kWp total capacity
- [x] 4 strings: Main roof east (4.8 kWp), Main roof south (6.0 kWp), Annex (4.0 kWp), PV fence (3.6 kWp)
- [x] 2 inverters (10 kVA + 8 kVA)
- [x] Battery storage 15 kWh (LiFePO4)

---

## Phase 9: Haystack Adapter (Planned)

See concept: [`docs/haystack-adapter-concept.md`](haystack-adapter-concept.md)

### 9.1 Basics (MVP)
- [ ] ASP.NET Core project setup
- [ ] `about`, `ops`, `formats` endpoints
- [ ] `read` endpoint with filter support
- [ ] JSON Grid Builder
- [ ] EnergyIQ → Haystack tag mapping

### 9.2 Navigation & History
- [ ] `nav` endpoint (ParentChild traversal)
- [ ] `hisRead` endpoint (OctoMesh TimeSeries)
- [ ] Haystack filter parser
- [ ] Zinc format support

### 9.3 Write & Real-Time
- [ ] `hisWrite`, `pointWrite` endpoints
- [ ] Watch mechanism (WebSocket)
- [ ] SCRAM authentication

---

## Phase 10: ISO 4157 Room Designation ✓

Implementation of the ISO 4157 standard for standardized room and storey designation.

### 10.1 BuildingStorey Attributes
- [x] `storeyNumber` (Int) – ISO 4157-1 storey number from bottom
- [x] `floorDesignation` (String) – National floor code (GF, 1F, TF)

### 10.2 Space Attributes
- [x] `roomNumber` (String) – ISO 4157-2 room number (GF01, 1F02, TF03)
- [x] `roomIdentifier` (String) – ISO 4157-3 room identifier (I#1001, I#2015)

### 10.3 RT Sample Updated
- [x] All storeys with storeyNumber and floorDesignation
- [x] All rooms with roomNumber and roomIdentifier
- [x] German convention: EG, 1.OG, DG

### 10.4 Documentation
- [x] Developer guide updated with ISO 4157 section

---

## Phase 11: Simulation Pipelines ✓

Implementation of simulation pipelines for the EnergyIQ model based on the OctoMesh Simulator framework.

### 11.1 Infrastructure
- [x] `data/_pipelines/` directory for pipelines
- [x] `rt-simulation-adapters.yaml` created

### 11.2 Pipeline Components
- [x] Pool Entity (Simulation Pool)
- [x] EdgeAdapter Entity (meshmakers/octo-communication-adapter-simulation)
- [x] DataPipeline Entity (Container)
- [x] EdgePipeline with Simulation@1 (10s polling interval)
- [x] MeshPipeline with CreateUpdateInfo@1 and ApplyChanges@1

### 11.3 Simulated Entities

**Rooms (6 main rooms):**
- [x] Living area GF (6789a00000000000000011d1)
- [x] Office GF (6789a00000000000000012d2)
- [x] Bedroom 1F (6789a00000000000000021d8)
- [x] Children's room 1F (6789a00000000000000023da)
- [x] Lounge TF (6789a00000000000000031de)
- [x] Office TF 1 (6789a00000000000000032df)

**PV System:**
- [x] PhotovoltaicSystem (totalCurrentPowerKW, gridFeedIn, selfConsumption)
- [x] PVString 1-4 (currentPowerKW)
- [x] Inverter 1-2 (dcPower, acPower)
- [x] BatteryStorage (stateOfCharge, chargingPower)

**HVAC:**
- [x] Boiler/Heat pump (supplyTemp, returnTemp, modulationLevel)
- [x] AirHandlingUnit (supplyAirTemp, fanSpeedSupply)

### 11.4 Simulation Profiles

| Attribute | Simulator | Range | Description |
|-----------|-----------|-------|-------------|
| temperature | Math.Sinus | 18-24°C | Diurnal cycle around setpoint |
| humidity | Math.Sinus | 35-65% | Phase-shifted |
| co2Level | Math.Triangle | 500-900 ppm | Rise during occupancy |
| illuminance | Math.Sinus | 100-700 lux | Daylight progression |
| heatingValvePosition | Math.Sinus | 20-70% | Follows temperature |
| ventilationLevel | Math.Sinus | 30-70% | Follows CO2 |
| pvStringPower | Math.Sinus | 0-6 kW | Solar progression |
| stateOfCharge | Math.Triangle | 30-90% | Charge/discharge cycle |

### 11.5 Documentation
- [x] Developer guide updated with simulation section

---

## CK YAML Example Structure

### Enum Example
```yaml
# enums/spaceType.yaml
$schema: https://schemas.meshmakers.cloud/construction-kit-elements.schema.json
enums:
- enumId: SpaceType
  values:
  - key: 0
    name: Office
  - key: 1
    name: MeetingRoom
  - key: 2
    name: Corridor
  # ...
```

### Attribute Example
```yaml
# attributes/temperature.yaml
$schema: https://schemas.meshmakers.cloud/construction-kit-elements.schema.json
attributes:
- id: Temperature
  valueType: Double
```

### Type Example (TreeNode)
```yaml
# types/space.yaml
$schema: https://schemas.meshmakers.cloud/construction-kit-elements.schema.json
types:
- typeId: Space
  derivedFromCkTypeId: ${Basic}/TreeNode  # Inherits ParentChild, Name, Description
  associations:
  # ParentChild inherited from TreeNode - enables: Space → BuildingStorey
  - id: ${this}/SpaceElements
    targetCkTypeId: ${this}/BuildingElement
  attributes:
  - id: ${this}/SpaceTypeValue
    name: spaceType
  - id: ${this}/Temperature
    name: temperature
  # ...
```

### Association Example
```yaml
# associations/spaceElements.yaml
$schema: https://schemas.meshmakers.cloud/construction-kit-elements.schema.json
associationRoles:
- id: SpaceElements
  inboundName: containedElements
  outboundName: containedInSpace
  inboundMultiplicity: N
  outboundMultiplicity: ZeroOrOne
```

---

## Notes for Claude Code

1. **Always specify schema:** `$schema: https://schemas.meshmakers.cloud/construction-kit-elements.schema.json`

2. **References:**
   - `${this}` = current model (EnergyIQ)
   - `${Basic}` = Basic package (NamedEntity, Tree, TreeNode)
   - `${System}` = System model (base types)

3. **Value Types:** String, Boolean, DateTime, Int, Double, StringArray, IntArray, Record, RecordArray, TimeSpan, Enum, Int64, DateTimeOffset, Binary, BinaryLinked, GeospatialPoint

4. **Inheritance from Basic:**
   - Spatial types (hierarchical): derive from `${Basic}/Tree` or `${Basic}/TreeNode`
   - Other types: derive from `${Basic}/NamedEntity` (provides Name + Description)
   - TreeNode automatically inherits ParentChild association for tree structure

5. **Enum Attributes:**
   ```yaml
   - id: SpaceTypeValue
     valueType: Enum
     valueCkEnumId: ${this}/SpaceType
   ```

6. **Record Attributes:**
   ```yaml
   - id: AddressValue
     valueType: Record
     valueCkRecordId: ${this}/Address
   ```

7. **Abstract Types:** Mark with `isAbstract: true`

8. **TimeSeries:** Automatically supported by OctoMesh, no special marking in CK required

9. **Order:** First Enums/Records/Attributes, then Types, then Associations

10. **Documentation:** Update `docs/developer-guide.md` after every structural change!
