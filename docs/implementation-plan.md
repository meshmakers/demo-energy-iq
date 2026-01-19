# Implementierungsplan EnergyIQ Construction Kit

## Phase 1: Basis-Typen ✓

### 1.1 Enums erstellen
- [x] `enums/spaceType.yaml` – SpaceTypeEnum
- [x] `enums/operatingMode.yaml` – OperatingModeEnum
- [x] `enums/shadingType.yaml` – ShadingTypeEnum
- [x] `enums/luminaireType.yaml` – LuminaireTypeEnum
- [x] `enums/systemType.yaml` – SystemTypeEnum
- [x] `enums/dayOfWeek.yaml` – DayOfWeekEnum

### 1.2 Records erstellen
- [x] `records/address.yaml` – Address
- [x] `records/scheduleEntry.yaml` – ScheduleEntry

### 1.3 Basis-Attribute erstellen
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
- [x] `types/space.yaml` – Space (mit allen Attributen)

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

## Phase 5: Haystack-Kompatibilität ✓

- [x] `records/haystackRef.yaml` – HaystackRef Record
- [x] `attributes/haystack.yaml` – HaystackTags, HaystackMeta
- [x] `attributes/haystackRefs.yaml` – HaystackRefs (RecordArray)
- [x] Haystack-Attribute zu Abstract Base Types hinzugefügt (SpatialElement, BuildingElement, TechnicalSystem)

---

## Phase 6: Validierung & Test

- [x] `ckModel.yaml` aktualisiert (modelId: EnergyIQ-1.0.0)
- [x] Build ausgeführt – erfolgreich
- [x] Sample-Daten erstellen – `data/bim/rt-firmianstrasse.yaml` (Firmianstraße 31A, Salzburg)
- [ ] GraphQL-Queries testen

---

## Phase 7: TreeNode-Umstellung ✓

Umstellung auf OctoMesh Basic Package für standardisierte Baumstrukturen (entspricht IFC IfcRelAggregates):

### 7.1 Dependencies aktualisiert
- [x] `ckModel.yaml` – Dependency auf `Basic-[2.0,3.0)` hinzugefügt
- [x] `EnergyIqCkModel.csproj` – PackageReference auf `Meshmakers.Octo.Sdk.Packages.Basic`
- [x] `GlobalUsings.cs` – `using Meshmakers.Octo.Sdk.Packages.Basic.Generated.Basic.v2`

### 7.2 Spatial Types auf TreeNode umgestellt
- [x] `types/spatialElement.yaml` – ENTFERNT (ersetzt durch TreeNode-Ableitung)
- [x] `types/site.yaml` – leitet jetzt von `Basic/Tree` ab
- [x] `types/building.yaml` – leitet jetzt von `Basic/TreeNode` ab (ParentChild geerbt)
- [x] `types/buildingStorey.yaml` – leitet jetzt von `Basic/TreeNode` ab
- [x] `types/space.yaml` – leitet jetzt von `Basic/TreeNode` ab

### 7.3 Andere Typen auf NamedEntity umgestellt
- [x] `types/buildingElement.yaml` – leitet jetzt von `Basic/NamedEntity` ab
- [x] `types/technicalSystem.yaml` – leitet jetzt von `Basic/NamedEntity` ab

### 7.4 Attributes bereinigt
- [x] `attributes/name.yaml` – ENTFERNT (von NamedEntity geerbt)
- [x] `attributes/description.yaml` – ENTFERNT (von NamedEntity geerbt)

### 7.5 RT Sample aktualisiert
- [x] `EnergyIQ/Name` → `System/Name`
- [x] `EnergyIQ/Description` → `System/Description`
- [x] rtIds korrigiert: Schema erfordert exakt 24 hexadezimale Zeichen (`^[0-9a-fA-F]{24}$`)
  - Alte IDs wie `6789a000000000000000site` enthielten ungültige Zeichen (s, i, t, e)
  - Neue IDs sind rein hexadezimal: `6789a00000000000000000a1`

### 7.6 Redundante Associations entfernt
- [x] `associations/siteBuildings.yaml` – ENTFERNT (System/ParentChild reicht)
- [x] `associations/buildingStoreys.yaml` – ENTFERNT (System/ParentChild reicht)
- [x] `associations/storeySpaces.yaml` – ENTFERNT (System/ParentChild reicht)
- [x] `associations/buildingSystems.yaml` – ENTFERNT (System/ParentChild reicht)
- [x] `types/technicalSystem.yaml` – leitet jetzt von `Basic/TreeNode` ab (statt NamedEntity)
- [x] RT Sample: alle Hierarchie-Associations → `System/ParentChild`

---

## Phase 8: Renewable Energy Systems ✓

### 8.1 Neue Attribute
- [x] `attributes/photovoltaic.yaml` – PV, Inverter, Battery Attribute

### 8.2 Neue Types (alle TreeNode)
- [x] `types/photovoltaicSystem.yaml` – PV-Anlage Container
- [x] `types/pvString.yaml` – PV-String (Modulgruppe)
- [x] `types/inverter.yaml` – Wechselrichter
- [x] `types/batteryStorage.yaml` – Batteriespeicher

### 8.3 RT Sample erweitert
- [x] PV-Anlage mit 18.4 kWp Gesamtleistung
- [x] 4 Strings: Hauptdach Ost (4.8 kWp), Hauptdach Süd (6.0 kWp), Nebengebäude (4.0 kWp), PV-Zaun (3.6 kWp)
- [x] 2 Wechselrichter (10 kVA + 8 kVA)
- [x] Batteriespeicher 15 kWh (LiFePO4)

---

## Phase 9: Haystack Adapter (geplant)

Siehe Konzept: [`docs/haystack-adapter-concept.md`](haystack-adapter-concept.md)

### 9.1 Basis (MVP)
- [ ] ASP.NET Core Projekt Setup
- [ ] `about`, `ops`, `formats` Endpoints
- [ ] `read` Endpoint mit Filter-Support
- [ ] JSON Grid Builder
- [ ] EnergyIQ → Haystack Tag Mapping

### 9.2 Navigation & History
- [ ] `nav` Endpoint (ParentChild Traversal)
- [ ] `hisRead` Endpoint (OctoMesh TimeSeries)
- [ ] Haystack Filter Parser
- [ ] Zinc Format Support

### 9.3 Schreiben & Echtzeit
- [ ] `hisWrite`, `pointWrite` Endpoints
- [ ] Watch-Mechanismus (WebSocket)
- [ ] SCRAM Authentication

---

## Phase 10: ISO 4157 Raumbezeichnung ✓

Implementierung der ISO 4157 Norm für standardisierte Raum- und Geschossbezeichnung.

### 10.1 BuildingStorey Attribute
- [x] `storeyNumber` (Int) – ISO 4157-1 Geschossnummer von unten
- [x] `floorDesignation` (String) – Nationaler Etagencode (EG, 1.OG, DG)

### 10.2 Space Attribute
- [x] `roomNumber` (String) – ISO 4157-2 Raumnummer (EG01, 1OG02, DG03)
- [x] `roomIdentifier` (String) – ISO 4157-3 Raumkennzeichen (I#1001, I#2015)

### 10.3 RT Sample aktualisiert
- [x] Alle Geschosse mit storeyNumber und floorDesignation
- [x] Alle Räume mit roomNumber und roomIdentifier
- [x] Deutsche Konvention: EG, 1.OG, DG

### 10.4 Dokumentation
- [x] Developer Guide aktualisiert mit ISO 4157 Abschnitt

---

## Phase 11: Simulation Pipelines ✓

Implementierung von Simulations-Pipelines für das EnergyIQ-Modell basierend auf dem OctoMesh Simulator-Framework.

### 11.1 Infrastruktur
- [x] `data/_pipelines/` Verzeichnis für Pipelines
- [x] `rt-simulation-adapters.yaml` erstellt

### 11.2 Pipeline-Komponenten
- [x] Pool Entity (Simulation Pool)
- [x] EdgeAdapter Entity (meshmakers/octo-communication-adapter-simulation)
- [x] DataPipeline Entity (Container)
- [x] EdgePipeline mit Simulation@1 (10s Polling-Intervall)
- [x] MeshPipeline mit CreateUpdateInfo@1 und ApplyChanges@1

### 11.3 Simulierte Entitäten

**Räume (6 Haupträume):**
- [x] Wohnbereich EG (6789a00000000000000011d1)
- [x] Büro EG (6789a00000000000000012d2)
- [x] Schlafzimmer OG (6789a00000000000000021d8)
- [x] Kinderzimmer OG (6789a00000000000000023da)
- [x] Aufenthaltsraum DG (6789a00000000000000031de)
- [x] Büro DG 1 (6789a00000000000000032df)

**PV-System:**
- [x] PhotovoltaicSystem (totalCurrentPowerKW, gridFeedIn, selfConsumption)
- [x] PVString 1-4 (currentPowerKW)
- [x] Inverter 1-2 (dcPower, acPower)
- [x] BatteryStorage (stateOfCharge, chargingPower)

**HVAC:**
- [x] Boiler/Wärmepumpe (supplyTemp, returnTemp, modulationLevel)
- [x] AirHandlingUnit (supplyAirTemp, fanSpeedSupply)

### 11.4 Simulations-Profile

| Attribut | Simulator | Bereich | Beschreibung |
|----------|-----------|---------|--------------|
| temperature | Math.Sinus | 18-24°C | Tagesgang um Sollwert |
| humidity | Math.Sinus | 35-65% | Phasenversetzt |
| co2Level | Math.Triangle | 500-900 ppm | Anstieg bei Belegung |
| illuminance | Math.Sinus | 100-700 lux | Tageslichtverlauf |
| heatingValvePosition | Math.Sinus | 20-70% | Folgt Temperatur |
| ventilationLevel | Math.Sinus | 30-70% | Folgt CO2 |
| pvStringPower | Math.Sinus | 0-6 kW | Sonnenverlauf |
| stateOfCharge | Math.Triangle | 30-90% | Lade/Entladezyklus |

### 11.5 Dokumentation
- [x] Developer Guide mit Simulation-Abschnitt aktualisiert

---

## CK YAML Beispiel-Struktur

### Enum Beispiel
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

### Attribute Beispiel
```yaml
# attributes/temperature.yaml
$schema: https://schemas.meshmakers.cloud/construction-kit-elements.schema.json
attributes:
- id: Temperature
  valueType: Double
```

### Type Beispiel (TreeNode)
```yaml
# types/space.yaml
$schema: https://schemas.meshmakers.cloud/construction-kit-elements.schema.json
types:
- typeId: Space
  derivedFromCkTypeId: ${Basic}/TreeNode  # Erbt ParentChild, Name, Description
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

### Association Beispiel
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

## Hinweise für Claude Code

1. **Schema immer angeben:** `$schema: https://schemas.meshmakers.cloud/construction-kit-elements.schema.json`

2. **Referenzen:**
   - `${this}` = aktuelles Modell (EnergyIQ)
   - `${Basic}` = Basic-Paket (NamedEntity, Tree, TreeNode)
   - `${System}` = System-Modell (Basis-Typen)

3. **Value Types:** String, Boolean, DateTime, Int, Double, StringArray, IntArray, Record, RecordArray, TimeSpan, Enum, Int64, DateTimeOffset, Binary, BinaryLinked, GeospatialPoint

4. **Vererbung von Basic:**
   - Spatial Types (hierarchisch): von `${Basic}/Tree` oder `${Basic}/TreeNode` ableiten
   - Andere Types: von `${Basic}/NamedEntity` ableiten (gibt Name + Description)
   - TreeNode erbt automatisch ParentChild Association für Baumstruktur

5. **Enum-Attribute:**
   ```yaml
   - id: SpaceTypeValue
     valueType: Enum
     valueCkEnumId: ${this}/SpaceType
   ```

6. **Record-Attribute:**
   ```yaml
   - id: AddressValue
     valueType: Record
     valueCkRecordId: ${this}/Address
   ```

7. **Abstract Types:** Mit `isAbstract: true` markieren

8. **TimeSeries:** Werden durch OctoMesh automatisch unterstützt, keine spezielle Markierung im CK nötig

9. **Reihenfolge:** Erst Enums/Records/Attributes, dann Types, dann Associations

10. **Dokumentation:** Nach jeder strukturellen Änderung `docs/developer-guide.md` aktualisieren!
