# EnergyIQ Construction Kit Spezifikation

## Übersicht

Das CK-Modell basiert auf:
- **ISO 16739-1:2024 (IFC 4.3)** für räumliche Struktur
- **VDI 3814** für Gebäudeautomation
- **Project Haystack** für semantische Interoperabilität (optional)
- **OO-Prinzip**: Messwerte als Attribute, nicht als separate Entitäten

## Typ-Hierarchie

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

Basisklasse für alle räumlichen Elemente.

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| GlobalId | String | Eindeutige ID (UUID) |
| Name | String | Kurzname |
| Description | String? | Beschreibung |
| LongName | String? | Ausführlicher Name |

---

### Site

Grundstück/Standort. Wurzel der räumlichen Hierarchie.

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| RefLatitude | Float? | Breitengrad |
| RefLongitude | Float? | Längengrad |
| RefElevation | Float? | Höhe über NN |

| Association | Target | Multiplicity | Beschreibung |
|-------------|--------|--------------|--------------|
| buildings | Building | 1:N | Gebäude auf dem Grundstück |

---

### Building

Gebäude.

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| ElevationOfRefHeight | Float? | Referenzhöhe |
| BuildingAddress | Address (Record) | Adresse |
| YearOfConstruction | Int? | Baujahr |
| GrossFloorArea | Float? | BGF gesamt |

| Association | Target | Multiplicity | Beschreibung |
|-------------|--------|--------------|--------------|
| storeys | BuildingStorey | 1:N (ordered) | Stockwerke |
| systems | TechnicalSystem | 1:N | TGA-Systeme |

---

### BuildingStorey

Stockwerk/Geschoss.

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| Elevation | Float | Höhe über Gebäude-Referenz |
| AboveGround | Boolean | Oberirdisch? |
| GrossFloorArea | Float? | BGF Geschoss |
| NetFloorArea | Float? | NGF Geschoss |

| Association | Target | Multiplicity | Beschreibung |
|-------------|--------|--------------|--------------|
| spaces | Space | 1:N | Räume im Geschoss |

---

### Space

Raum – zentrales Objekt für Energieoptimierung.

**Stammdaten:**

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| SpaceType | SpaceTypeEnum | Raumtyp |
| NetFloorArea | Float? | Nettofläche m² |
| GrossFloorArea | Float? | Bruttofläche m² |
| CeilingHeight | Float? | Raumhöhe m |
| DesignOccupancy | Int? | Geplante Belegung |

**Istwerte (TimeSeries):**

| Attribute | Type | Unit | Beschreibung |
|-----------|------|------|--------------|
| Temperature | Float? | °C | Raumtemperatur |
| Humidity | Float? | % | Relative Feuchte |
| CO2Level | Float? | ppm | CO₂-Konzentration |
| Illuminance | Float? | lx | Beleuchtungsstärke |
| PresenceDetected | Boolean? | - | Präsenz erkannt |
| WindowOpen | Boolean? | - | Fenster offen |

**Sollwerte:**

| Attribute | Type | Unit | Beschreibung |
|-----------|------|------|--------------|
| TemperatureSetpointHeating | Float? | °C | Heiz-Sollwert |
| TemperatureSetpointCooling | Float? | °C | Kühl-Sollwert |
| IlluminanceSetpoint | Float? | lx | Beleuchtungs-Sollwert |
| CO2Setpoint | Float? | ppm | CO₂-Sollwert |

**Stellgrößen (TimeSeries):**

| Attribute | Type | Unit | Beschreibung |
|-----------|------|------|--------------|
| HeatingValvePosition | Float? | % | Heizventil 0-100 |
| CoolingValvePosition | Float? | % | Kühlventil 0-100 |
| VentilationLevel | Float? | % | Lüftungsstufe |
| LightingLevel | Float? | % | Beleuchtung 0-100 |
| ShadingPosition | Float? | % | Beschattung 0-100 |

**Betriebsmodus:**

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| OperatingMode | OperatingModeEnum | Aktueller Modus |
| OccupancySchedule | ScheduleEntry[] | Belegungsplan |

**Energiekennzahlen (aggregiert):**

| Attribute | Type | Unit | Beschreibung |
|-----------|------|------|--------------|
| EnergyConsumptionHeating | Float? | kWh | Heizenergie (Periode) |
| EnergyConsumptionCooling | Float? | kWh | Kühlenergie (Periode) |
| EnergyConsumptionLighting | Float? | kWh | Beleuchtung (Periode) |
| EnergyConsumptionTotal | Float? | kWh | Gesamt (Periode) |

| Association | Target | Multiplicity | Beschreibung |
|-------------|--------|--------------|--------------|
| containedElements | BuildingElement | 1:N | Elemente im Raum |
| servedBy | TechnicalSystem | N:M | Versorgende Anlagen |

---

### BuildingElement (abstract)

Basisklasse für Bauelemente.

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| GlobalId | String | Eindeutige ID |
| Name | String | Bezeichnung |
| ObjectType | String? | Typ-Beschreibung |

| Association | Target | Multiplicity | Beschreibung |
|-------------|--------|--------------|--------------|
| containedInSpace | Space | N:1 | Raum-Zugehörigkeit |

---

### Door

Tür mit Zustandsattributen.

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| OverallHeight | Float | Höhe mm |
| OverallWidth | Float | Breite mm |
| IsExternal | Boolean | Außentür? |
| IsOpen | Boolean? | Offen? (TimeSeries) |
| IsLocked | Boolean? | Verriegelt? (TimeSeries) |

---

### Window

Fenster mit Zustandsattributen.

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| OverallHeight | Float | Höhe mm |
| OverallWidth | Float | Breite mm |
| IsOpen | Boolean? | Offen? (TimeSeries) |
| OpeningPosition | Float? | Öffnungsgrad % |

---

### ShadingDevice

Sonnenschutz (Jalousie, Rollo, Markise).

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| ShadingType | ShadingTypeEnum | Typ |
| Position | Float? | Ist-Position % (TimeSeries) |
| SlatAngle | Float? | Lamellenwinkel ° |
| PositionSetpoint | Float? | Soll-Position % |
| SlatAngleSetpoint | Float? | Soll-Winkel ° |

---

### Luminaire

Leuchte.

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| LuminaireType | LuminaireTypeEnum | Leuchtentyp |
| RatedPower | Float | Nennleistung W |
| IsOn | Boolean? | Ein? (TimeSeries) |
| DimmingLevel | Float? | Dimmwert % (TimeSeries) |
| DimmingLevelSetpoint | Float? | Soll-Dimmwert % |

---

### TechnicalSystem (abstract)

Basisklasse für TGA-Anlagen.

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| Identifier | String | Anlagenkennzeichen |
| Name | String | Bezeichnung |
| SystemType | SystemTypeEnum | Anlagentyp |
| IsRunning | Boolean? | In Betrieb? (TimeSeries) |
| FaultState | Boolean? | Störung? (TimeSeries) |

| Association | Target | Multiplicity | Beschreibung |
|-------------|--------|--------------|--------------|
| servesSpaces | Space | N:M | Versorgte Räume |
| containedInBuilding | Building | N:1 | Gebäude-Zuordnung |

---

### AirHandlingUnit

Lüftungsgerät (RLT-Anlage).

| Attribute | Type | Unit | Beschreibung |
|-----------|------|------|--------------|
| SupplyAirTemp | Float? | °C | Zuluft-Temperatur |
| ReturnAirTemp | Float? | °C | Abluft-Temperatur |
| OutdoorAirTemp | Float? | °C | Außenluft-Temperatur |
| SupplyAirTempSetpoint | Float? | °C | Zuluft-Sollwert |
| SupplyAirFlow | Float? | m³/h | Zuluft-Volumenstrom |
| FanSpeedSupply | Float? | % | Zuluft-Ventilator |
| FanSpeedReturn | Float? | % | Abluft-Ventilator |
| FilterDifferentialPressure | Float? | Pa | Filterdruck |
| HeatRecoveryEfficiency | Float? | % | WRG-Wirkungsgrad |
| HeatingCoilPosition | Float? | % | Heizregister |
| CoolingCoilPosition | Float? | % | Kühlregister |

---

### Boiler

Heizkessel.

| Attribute | Type | Unit | Beschreibung |
|-----------|------|------|--------------|
| SupplyTemp | Float? | °C | Vorlauf-Temperatur |
| ReturnTemp | Float? | °C | Rücklauf-Temperatur |
| SupplyTempSetpoint | Float? | °C | Vorlauf-Sollwert |
| ModulationLevel | Float? | % | Modulationsgrad |
| FuelConsumption | Float? | kWh | Verbrauch (TimeSeries) |
| Efficiency | Float? | % | Wirkungsgrad |

---

### Pump

Pumpe.

| Attribute | Type | Unit | Beschreibung |
|-----------|------|------|--------------|
| FlowRate | Float? | m³/h | Volumenstrom |
| Pressure | Float? | bar | Druck |
| SpeedSetpoint | Float? | % | Drehzahl-Sollwert |
| PowerConsumption | Float? | kW | Leistungsaufnahme |

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

### Räumliche Hierarchie

```
Site ──(1:N)──► Building ──(1:N)──► BuildingStorey ──(1:N)──► Space
```

### Element-Containment

```
Space ◄──(N:1)── BuildingElement
```

### TGA-Versorgung

```
Space ◄──(N:M)──► TechnicalSystem
```

### Gebäude-Zuordnung

```
Building ◄──(N:1)── TechnicalSystem
```

---

## TimeSeries-Attribute

Folgende Attribute sollen als TimeSeries geführt werden (historische Werte):

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
- Alle Temperatur- und Durchflusswerte
- Energieverbräuche

---

## Haystack-Kompatibilität (optional)

Für Interoperabilität mit Haystack-basierten Tools (SkySpark, FIN Framework, etc.) können optionale Haystack-Attribute geführt werden.

### HaystackTaggable (Mixin)

Kann auf alle relevanten Types angewendet werden:

| Attribute | Type | Beschreibung |
|-----------|------|--------------|
| haystackTags | String[] | Haystack Marker-Tags |
| haystackRefs | HaystackRef[] (Record) | Referenzen zu anderen Entitäten |
| haystackMeta | Map<String, Any>? | Zusätzliche Haystack-Metadaten |

### HaystackRef (Record)
```yaml
attributes:
  - refName: String      # z.B. "equipRef", "spaceRef", "siteRef"
  - targetId: String     # GlobalId des Zielobjekts
```

### Automatisches Tag-Mapping

| EnergyIQ Type | Haystack Tags |
|---------------|---------------|
| Site | site |
| Building | site (mit Gebäude-Tags) |
| Space | space, hvacZone |
| AirHandlingUnit | ahu, hvac, equip |
| Boiler | boiler, hvac, equip, hot, water |
| Pump | pump, equip |
| Temperature (Attr) | temp, sensor, point |
| TemperatureSetpoint | temp, sp, point |
| HeatingValvePosition | valve, cmd, point, hot, water |

### Beispiel mit Haystack-Tags

```yaml
- type: AirHandlingUnit
  globalId: "ahu-001"
  name: "RLT Zentrale"
  supplyAirTemp: 18.5
  # Haystack-Kompatibilität
  haystackTags: ["ahu", "hvac", "equip", "rooftop"]
  haystackRefs:
    - refName: "siteRef"
      targetId: "site-001"
    - refName: "spaceRef"
      targetId: "space-001"
```

---

## Beispiel-Instanz

```yaml
# Site
- type: Site
  globalId: "site-001"
  name: "Hauptstandort Wien"
  refLatitude: 48.2082
  refLongitude: 16.3738

# Building
- type: Building
  globalId: "bldg-001"
  name: "Bürogebäude A"
  yearOfConstruction: 2020
  grossFloorArea: 5000
  buildingAddress:
    street: "Technopark 1"
    postalCode: "1220"
    city: "Wien"
    country: "AT"

# BuildingStorey
- type: BuildingStorey
  globalId: "storey-eg"
  name: "EG"
  elevation: 0.0
  aboveGround: true

# Space
- type: Space
  globalId: "space-001"
  name: "Besprechung 1"
  spaceType: MeetingRoom
  netFloorArea: 25.0
  ceilingHeight: 2.8
  designOccupancy: 10
  temperatureSetpointHeating: 21.0
  temperatureSetpointCooling: 24.0
  illuminanceSetpoint: 500
  # Istwerte via TimeSeries
  temperature: 22.3
  humidity: 45.0
  presenceDetected: true
```
