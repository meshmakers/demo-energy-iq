# EnergyIQ Construction Kit Implementation Plan

This document tracks the historical phases of EnergyIQ development. The current state is **EnergyIQ-2.0.0**.

For ongoing / future work see:
- `space-restructuring-concept.md` — IFC/VDI restructure (delivered in 2.0.0)
- `haystack-integration-concept.md` — Haystack projection layer (mapping config + renderer pending)
- `haystack-adapter-concept.md` — Haystack REST API server (depends on the integration concept)

---

## Phase 1: Base Types (v1) ✓

### 1.1 Create Enums
- [x] `enums/spaceType.yaml` – SpaceType
- [x] `enums/operatingMode.yaml` – OperatingMode (VDI 3814)
- [x] `enums/shadingType.yaml` – ShadingType
- [x] `enums/luminaireType.yaml` – LuminaireType
- [x] `enums/systemType.yaml` – SystemType
- [x] `enums/dayOfWeek.yaml` – DayOfWeek

### 1.2 Create Records
- [x] `records/address.yaml` – Address
- [x] `records/scheduleEntry.yaml` – ScheduleEntry

### 1.3 Create Base Attributes
- [x] `attributes/globalId.yaml` – String
- [x] `attributes/temperature.yaml` – Double
- [x] `attributes/percentage.yaml` – Double (0-100)
- [x] `attributes/area.yaml` – Double (m²)
- [x] `attributes/length.yaml` – Double (mm)
- [x] `attributes/energy.yaml` – Double (kWh)

---

## Phase 2: Spatial Structure (v1) ✓

- [x] `types/site.yaml`, `types/building.yaml`, `types/buildingStorey.yaml`, `types/space.yaml`
- [x] Hierarchy via `Basic/Tree` + `Basic/TreeNode` (`System/ParentChild` inherited)

---

## Phase 3: Building Elements (v1) ✓

- [x] `types/buildingElement.yaml` (abstract)
- [x] `types/wall.yaml`, `door.yaml`, `window.yaml`, `shadingDevice.yaml`, `luminaire.yaml`
- [x] `associations/spaceElements.yaml` (Space → BuildingElement)

---

## Phase 4: Technical Systems (v1) ✓

- [x] `types/technicalSystem.yaml` (abstract)
- [x] `types/airHandlingUnit.yaml`, `boiler.yaml`, `chiller.yaml`, `pump.yaml`
- [x] `associations/systemSpaces.yaml`

---

## Phase 5: Haystack Compatibility (v1) ✓ — later removed in v2

In v1, Haystack tags were carried as mixin attributes on every type.

- [x] `records/haystackRef.yaml`
- [x] `attributes/haystack.yaml` (HaystackTags, HaystackMeta)
- [x] `attributes/haystackRefs.yaml`
- [x] Mixin attributes on all base + concrete types

**v2 decision:** All Haystack mixin attributes removed. Haystack compatibility is now provided via a projection layer (see `haystack-integration-concept.md`). The structural impedance that motivated mixins (Haystack Point as first-class entity vs. inline Space attribute) was resolved by the v2 Sensor-as-entity restructuring.

---

## Phase 6: Validation & Sample Data (v1) ✓
- [x] `ckModel.yaml` (`EnergyIQ-1.0.0`)
- [x] Initial demo: `data/bim/rt-firmianstrasse.yaml`

---

## Phase 7: TreeNode Migration (v1) ✓

Migration to OctoMesh Basic package for tree structures (mirrors `IfcRelAggregates`).

- [x] Site → `Basic/Tree`; Building / BuildingStorey / Space / TechnicalSystem → `Basic/TreeNode`
- [x] BuildingElement → `Basic/NamedEntity`
- [x] `attributes/name.yaml`, `description.yaml` removed (inherited from NamedEntity)
- [x] Redundant explicit hierarchy associations removed; `System/ParentChild` used throughout

---

## Phase 8: Renewable Energy Systems (v1) ✓

- [x] `attributes/photovoltaic.yaml`
- [x] `types/photovoltaicSystem.yaml`, `pvString.yaml`, `inverter.yaml`, `batteryStorage.yaml`
- [x] RT sample: 18.4 kWp PV, 4 strings, 2 inverters, 15 kWh battery

---

## Phase 10: ISO 4157 Room Designation (v1) ✓

- [x] `BuildingStorey.StoreyNumber` + `FloorDesignation` (4157-1)
- [x] `Space.RoomNumber` (4157-2), `Space.RoomIdentifier` (4157-3, immutable)

---

## Phase 11: Simulation Pipelines (v1) ✓ — updated in v2

- [x] `data/_pipelines/rt-simulation-adapters.yaml`
- [x] EdgePipeline: Math.Sinus / Math.Triangle simulators, 10s polling
- [x] MeshPipeline: CreateUpdateInfo@1 + ApplyChanges@1
- [x] **v2 update:** MeshPipeline now targets Sensor / Actuator / Terminal entities (not Space attributes)

---

## Phase 12: IFC/VDI Restructuring (v2.0.0) ✓

Full restructure following IFC 4.3 and VDI 3814 (Anlagen- vs. Raumautomation split). Concept: `space-restructuring-concept.md`.

### 12.1 New Enums
- [x] `enums/heatPumpOperatingMode.yaml` (Off, Standby, Heating, ActiveCooling, PassiveCooling, DomesticHotWater, Defrost, Fault)
- [x] `enums/heatSource.yaml` (Air, Ground, Water, Exhaust, Hybrid)
- [x] `enums/valveType.yaml` (Heating, Cooling, Reversible, ChangeoverHeatingCooling, Mixing, Bypass, Isolation)
- [x] `enums/terminalOperatingMode.yaml`
- [x] `enums/scheduleType.yaml`
- [x] `enums/distributionSystemType.yaml`
- [x] `enums/spaceType.yaml` extended with 11 residential types (LivingRoom, Bedroom, Bathroom, …)

### 12.2 New Records
- [x] `records/psetSpaceThermalRequirements.yaml`
- [x] `records/psetSpaceLightingRequirements.yaml`
- [x] `records/psetSpaceOccupancyRequirements.yaml`

### 12.3 New Associations
- [x] `associations/spaceSensors.yaml`, `spaceActuators.yaml`, `spaceTerminals.yaml`
- [x] `associations/equipmentSensors.yaml`, `equipmentActuators.yaml`
- [x] `associations/terminalActuators.yaml`, `terminalServedBy.yaml`
- [x] `associations/systemMembers.yaml`
- [x] `associations/spaceSchedules.yaml`

### 12.4 New Plant Types
- [x] `types/heatPump.yaml` (reversible aggregate)
- [x] `types/thermalEnergyStorage.yaml`

### 12.5 Terminal-Unit Hierarchy
- [x] `types/roomTerminal.yaml` (abstract), `hydronicTerminal.yaml` (abstract)
- [x] `types/radiator.yaml`, `radiantSurface.yaml`, `chilledBeam.yaml`, `fanCoilUnit.yaml`
- [x] `types/airTerminal.yaml`, `electricHeater.yaml`

### 12.6 Sensor Hierarchy
- [x] `types/sensor.yaml` (abstract)
- [x] `types/temperatureSensor.yaml`, `humiditySensor.yaml`, `co2Sensor.yaml`, `illuminanceSensor.yaml`
- [x] `types/presenceSensor.yaml`, `windowContactSensor.yaml`, `genericSensor.yaml`

### 12.7 Actuator Hierarchy
- [x] `types/actuator.yaml` (abstract)
- [x] `types/valve.yaml`, `damper.yaml`, `dimmer.yaml`, `motor.yaml`

### 12.8 Supporting Types
- [x] `types/externalSpace.yaml` (IfcExternalSpatialElement)
- [x] `types/schedule.yaml` (M:N to Space)
- [x] `types/distributionSystem.yaml`

### 12.9 Space Entrümpelt
- [x] Removed: Temperature, Humidity, CO2Level, Illuminance, PresenceDetected, WindowOpen, HeatingValvePosition, CoolingValvePosition, VentilationLevel, LightingLevel, ShadingPosition, TemperatureSetpointHeating/Cooling, CO2Setpoint, IlluminanceSetpoint, ScheduleEntries, EnergyConsumption*
- [x] Added: Pset records (Thermal/Lighting/Occupancy), PredefinedType, new associations to Sensor/Actuator/Terminal/Schedule

### 12.10 Haystack Cleanup
- [x] Removed `attributes/haystack.yaml`, `attributes/haystackRefs.yaml`, `records/haystackRef.yaml`
- [x] Removed HaystackTags / HaystackRefs / HaystackMeta from all 9 affected types

### 12.11 Plant-Equipment Updates
- [x] Boiler / Chiller / AHU / Pump: added EquipmentSensors, EquipmentActuators, TerminalServedBy, SystemMembers associations
- [x] Window / Door: added EquipmentSensors
- [x] ShadingDevice / Luminaire: added EquipmentActuators

### 12.12 Version Bump
- [x] `ckModel.yaml`: `EnergyIQ-2.0.0`
- [x] CK build clean: 0 warnings, 0 errors

---

## Phase 13: RT Data Migration Firmianstrasse (v2.0.0) ✓

`data/bim/rt-firmianstrasse.yaml` rewritten — ~140 entities (up from ~35 in v1):

- [x] 4 spatial top-level entities (Site, 2 Buildings, 4 storeys)
- [x] 18 interior Spaces with Pset records
- [x] 5 ExternalSpaces (Terrasse EG/Dachterrasse OG/Balkon DG/Zufahrt/Garten)
- [x] ~50 Sensors (Temperature/Humidity/CO2/Illuminance/Presence/WindowContact distributed across rooms)
- [x] ~20 RoomTerminals (RadiantSurface for HG with IsReversible, Radiator for NG, AirTerminal for KWL outlets)
- [x] ~25 Actuators (Valves, Dampers, Motor for shading)
- [x] HeatPump replacing v1 Boiler (`IsReversibleAggregate: true`, OperatingMode `Heating`, COP 3.8 / SCOP 4.2)
- [x] ChangeoverHeatingCooling Valve at HeatPump (passive cooling mode switch)
- [x] ThermalEnergyStorage (500l buffer)
- [x] AirHandlingUnit (KWL), Pump, full PV chain (unchanged)
- [x] 4 Schedules (Wohnen-Werktag, Wohnen-Wochenende, Schlafzimmer-Nacht, Buero-Werktag)
- [x] 3 DistributionSystems (Heizkreis, Lüftung, Elektrisch/PV)
- [x] All v1 Haystack tag arrays removed

---

## Phase 14: Archives (v2.0.0) ✓

Moved from `octo-adapter-loxone` to `demo-energy-iq` (archives reference EnergyIQ types).

- [x] `data/_general/rt-archives-energyiq.yaml`
- [x] TemperatureSensorArchive (rtId `6a0e000000000000000a0001`)
- [x] HumiditySensorArchive (`6a0e000000000000000a0002`)
- [x] CO2SensorArchive (`6a0e000000000000000a0003`)
- [x] `Path` references the display name `CurrentValue` (unified across sensor subtypes)
- [x] `om_importrt.ps1` imports + activates archives (Upsert mode)

---

## Phase 15: Loxone Pipeline Adaptation (v2.0.0) ✓

`octo-adapter-loxone/scripts/_general/rt-pipelines-loxone.yaml`:

- [x] **Store Control States** pipeline: `BackfillFromRtEntity@1` + `SaveStreamDataInArchive@1` fan-out to 3 sensor archives
- [x] Old SpaceArchive rtId references removed
- [x] **AI Auto-Map** disabled — prompt + JSON contract assume Space attribute targets, not Sensor entities
- [x] **Rules-based Auto-Map** disabled — `GenerateDataPointMappings@1` needs a Space→Sensor navigation step
- [x] **Validate Coverage** disabled — `ValidateDataPointCoverage@1` rules check Space attributes that moved to Sensors
- [x] All disabled pipelines carry migration TODO comments

---

## Phase 16: Documentation (v2.0.0) ✓

- [x] `CLAUDE.md` — full v2 hierarchy, IFC/VDI principles, associations table
- [x] `docs/developer-guide.md` — rewritten for v2
- [x] `docs/construction-kit.md` — type-by-type catalog for v2
- [x] `docs/standards-reference.md` — IFC 4.3 / VDI 3814 / Haystack 4 mappings
- [x] `docs/implementation-plan.md` — this file
- [x] `docs/space-restructuring-concept.md` — Status: umgesetzt
- [x] `docs/haystack-integration-concept.md` — Status: EnergyIQ-Cleanup done, mapping config pending

---

## Phase 17: Haystack Mapping Config ✓

Phase 1 of `haystack-integration-concept.md`. 35 type mappings + index + README.

- [x] `src/EnergyIqHaystackMapping/mapping/_index.yaml` — PH4 lib metadata, default tags, unit conventions, identity strategy
- [x] `src/EnergyIqHaystackMapping/mapping/README.md` — mapping schema documentation
- [x] Spatial (5): Site, Building, BuildingStorey, Space, ExternalSpace
- [x] Sensors (7): TemperatureSensor, HumiditySensor, CO2Sensor, IlluminanceSensor, PresenceSensor, WindowContactSensor, GenericSensor (each → ph::Point 1:1)
- [x] Actuators (4): Valve, Damper, Dimmer, Motor (Equip + synthetic Position/Setpoint points)
- [x] Terminals (6): Radiator, RadiantSurface, ChilledBeam, FanCoilUnit, AirTerminal, ElectricHeater
- [x] Plant (6): HeatPump, Boiler, Chiller, AirHandlingUnit, Pump, ThermalEnergyStorage
- [x] BuildingElements (4): Door, Window, ShadingDevice, Luminaire (Wall intentionally skipped)
- [x] PV (4): PhotovoltaicSystem, PVString, Inverter, BatteryStorage
- [x] Identity: prefer `GlobalId`, fallback `rtId`, prefix `@energyiq:`
- [x] PH4-conformant unit strings throughout
- [x] Schedule / DistributionSystem intentionally not mapped (logical aggregates — defer until concrete consumer)

---

## Phase 18: Haystack Export Renderer ✓ (JSON grid)

Phase 3 of `haystack-integration-concept.md`. Standalone CLI tool that consumes
the v2 mapping config + a runtime model YAML, emits a PH4 JSON grid.

- [x] `src/EnergyIqHaystackExport/` project (net10.0 console app)
- [x] `Mapping/` — POCO records + YamlDotNet-based loader for `_index.yaml` and per-type mappings
- [x] `Runtime/` — `RtModel`/`RtEntity`/`RtRecord` plus `RtModelLoader` reading the OctoMesh
      runtime-model YAML schema (entities + attributes + associations + nested records,
      including bracket-string arrays inside RecordArray)
- [x] `Rendering/EntityRenderer` — applies a `TypeMapping` to an `RtEntity`:
      - emits one main PH dict (id, dis, markers, default + per-type tags, refs)
      - resolves `parent` and `ancestor` refs via `System/ParentChild` navigation
      - emits one synthetic Point dict per `PointMapping` (used by actuators / equipment
        with multiple measurement/setpoint values)
      - converts attribute values to PH wrappers (`PhMarker`, `PhNumber{unit}`, `PhRef`)
        and resolves enum mappings (CK enum key → PH string)
      - reads nested record fields via dotted paths (`AddressValue.Street`,
        `ThermalRequirementsRecord.SpaceTemperature`)
- [x] `Rendering/JsonGridWriter` — PH4 JSON grid output (`_kind:grid`, meta with phLib +
      version, cols + rows, value kinds: ref/marker/number)
- [x] CLI: `--rt`, `--mapping`, `--output`
- [x] Verified end-to-end: `rt-firmianstrasse.yaml` (140 entities) → 359 PH dicts
      (1 site + 29 spaces + 63 equip + 266 points), valid PH4 JSON
- [x] Mapping files corrected to reference underlying CK attribute IDs (not display names) —
      RT YAML stores attributes by global id, so the mapping must match
- [x] `Rendering/IGridWriter` — common writer interface
- [x] `Rendering/ZincGridWriter` — PH4 Zinc wire format (compact grid: meta line + cols line
      + data rows with `M`/`N`/`T`/`F`/`@ref`/number-with-unit/string encodings)
- [x] `Rendering/TrioWriter` — PH4 Trio dict-per-block format with `---` separators
- [x] CLI `--format json|zinc|trio` (or inferred from output extension)
- [x] Ref identity fix: `refIdPrefix` is `energyiq:` (no leading `@`); the `@` sigil is added
      by Zinc/Trio writers and omitted by JSON per PH4 spec
- [x] Verified all three formats against rt-firmianstrasse.yaml:
      JSON ~232K / 11559 lines, Zinc ~192K / 361 lines, Trio ~128K / 4495 lines
- [ ] Hooks into the Haystack REST API service (`read`, `nav`, `hisRead`) — see
      `haystack-adapter-concept.md`. The renderer's core can be embedded directly.
- [ ] Compound attributes (e.g. emit a single `geoCoord: Coord(lat, lng)` from two CK
      attributes instead of separate `geoCoord.lat`/`geoCoord.lng` tags) — out of scope for now

---

## Phase 19: PH4 lib Generator ✓ (Xeto)

Phase 4 of `haystack-integration-concept.md`. Emits a Xeto-format lib definition
from the mapping config — registerable as a starting point in SkySpark / FIN
(exact registration syntax may need minor per-tool adjustments).

- [x] `Generation/LibGenerator.cs` — reads the mapping library, emits a Xeto lib
- [x] CLI `--mode export | lib` (export mode is the default; lib mode skips --rt)
- [x] Pragma header with version, haystackVersion, depends (`ph`, `phIoT`), org metadata
- [x] One Xeto spec per non-abstract EnergyIQ type, grouped by purpose (Spatial /
      Plant equipment / Room terminals / Sensors / Actuators / Building elements /
      Photovoltaic) with `// =====` section banners
- [x] Markers emitted as `name: Marker` slots
- [x] Refs emitted as `Ref<of:"TargetSpec">?` with target-spec name resolution
- [x] Attribute slots: `name: Kind <unit:"...">?` (e.g. `area: Number <unit:"m²">?`)
- [x] Tag-path dots flattened to underscore in slot names (Xeto identifiers don't
      allow dots) — original PH tag name preserved in a trailing comment
- [x] Synthetic Point sub-mappings listed as comments under the parent spec
- [x] Sample run against the v2 mapping config: 36 specs, ~700-line output file
      (`out/energyIq.xeto`)

---

## Phase 20: Loxone Auto-Map Re-Enablement (planned)

Follow-up to Phase 15 — requires Pipeline-Node enhancements in `octo-communication-controller-services`:

- [ ] Extend `GenerateDataPointMappings@1` with a "navigate container → child entity by ckTypeId + association" step
- [ ] Extend `ValidateDataPointCoverage@1` with a rule form "required associated entity of ckTypeId X via role Y"
- [ ] Rewrite AI Auto-Map prompt + JSON contract for sensor-targeted mappings
- [ ] Re-enable the three pipelines

---

## Out of Scope (kept compatible for later)

See `space-restructuring-concept.md` §12 for the full list. Notable items:
- Decentralised HRV units per room
- Solarthermie (`SolarThermalCollector`)
- BHKW (`CombinedHeatAndPower`)
- Pelletkessel — `Boiler` PredefinedType variant or dedicated subtype
- Submetering / Smart Meter — `EnergyMeter` sensor subtype + DistributionSystem binding
- Wallbox / EV charger (`ElectricVehicleCharger` TechnicalSystem subtype)
- Weather-compensated heating curve
- Native IFC export

---

## CK YAML Reference

### Schema Reference
Every CK YAML file must start with:
```yaml
$schema: https://schemas.meshmakers.cloud/construction-kit-elements.schema.json
```

Model metadata (`ckModel.yaml`):
```yaml
$schema: https://schemas.meshmakers.cloud/construction-kit-meta.schema.json
modelId: EnergyIQ-2.0.0
dependencies:
- Basic-[2.0,3.0)
```

### Reference Syntax
- `${this}` = current model (EnergyIQ)
- `${Basic}` = Basic package (NamedEntity, Tree, TreeNode)
- `${System}` = System model (base types)

### Value Types
`String`, `Boolean`, `DateTime`, `Int`, `Double`, `StringArray`, `IntArray`, `Record`, `RecordArray`, `TimeSpan`, `Enum`, `Int64`, `DateTimeOffset`, `Binary`, `BinaryLinked`, `GeospatialPoint`.

### Implementation Order
Enums → Records → Attributes → Associations → Types (abstract first, then concrete).

### Naming Convention
- Attribute `id` and `name`: PascalCase
- Type `typeId`, Enum `enumId`, Record `recordId`: PascalCase
- Association `id`, `inboundName`, `outboundName`: PascalCase

### Enum Attribute
```yaml
- id: SpaceTypeValue
  valueType: Enum
  valueCkEnumId: ${this}/SpaceType
```

### Record Attribute
```yaml
- id: ThermalRequirementsRecord
  valueType: Record
  valueCkRecordId: ${this}/PsetSpaceThermalRequirements
```

### RecordArray Attribute
```yaml
- id: ScheduleEntries
  valueType: RecordArray
  valueCkRecordId: ${this}/ScheduleEntry
```

### Abstract Type
```yaml
types:
- typeId: Sensor
  derivedFromCkTypeId: ${this}/BuildingElement
  isAbstract: true
```

### Notes
1. **Always include `$schema`** — every CK YAML.
2. **TimeSeries** is automatic — no marking required in CK.
3. **Documentation must be updated** after every CK structural change (CLAUDE.md mandates this).
4. **Arrays inside RecordArray** require bracket-string encoding in RT YAML: `value: '[0, 1, 2]'` not block-style or inline flow.
