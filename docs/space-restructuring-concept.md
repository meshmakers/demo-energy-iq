# Space and Equipment Restructuring Concept

**Status:** Umgesetzt in EnergyIQ-2.0.0 (CK + RT-Daten Firmianstrasse); Mapping-Update und Doku-Pflege laufen separat
**Datum:** 2026-05-20
**Bezug:** Ergänzt `haystack-integration-concept.md` (Datenmodell-Architektur). Beide Refactorings wurden gemeinsam in `EnergyIQ-2.0.0` ausgeliefert.

---

## 1. Ausgangslage

Der `Space`-Typ trägt aktuell 28 optionale Attribute, darunter solche, die konzeptuell an Terminal-Units bzw. an separate Sensor-/Aktor-Entities gehören. Konkret:

- **Redundanz** mit existierenden Equipment-Typen: `WindowOpen` ↔ `Window.IsOpen`, `LightingLevel` ↔ `Luminaire.DimmingLevel`, `ShadingPosition` ↔ `ShadingDevice.Position`
- **Fehlende Terminal-Ebene:** Stellgrößen für Heizung/Kühlung/Lüftung (`HeatingValvePosition`, `CoolingValvePosition`, `VentilationLevel`) liegen direkt am Raum — gehören aber laut VDI 3814 an die Terminal-Einheit (Heizkörper, Fan-Coil, VAV-Box, Radiantfläche, …)
- **Keine Repräsentation reversibler Aggregate:** Eine Wärmepumpe mit Passivkühlung (gleicher Hydraulikkreis, umgeschaltet zwischen Heizen und Kühlen) ist im aktuellen Modell nicht sauber abbildbar — sie ist *weder* `Boiler` *noch* `Boiler + Chiller`
- **Vermischung Design ↔ Runtime:** Pset-Anforderungen (Soll-Bereiche nach IFC 4.3) und Runtime-Messwerte stehen auf demselben Typ
- **Außenbereiche** werden als reguläre `Space` mit `SpaceType: 14 (Other)` modelliert — IFC 4.3 hat dafür `IfcExternalSpatialElement`
- **`SpaceType`-Enum** ist büro-/gewerbe-zentrisch — Wohnraumtypen fehlen, daher landen 11 von 22 Räumen im Firmianstrasse-Beispiel als `Other`
- **Energieverbrauch am Raum** (`EnergyConsumptionHeating/Cooling/Lighting/Total`) ist konzeptuell unscharf — Energie wird an Geräten verbraucht, pro-Raum-Werte sind abgeleitet

Ziel ist ein **allgemeingültiges CK-Modell**, das von der Single-Family-Home-Anlage bis zum Gewerbeobjekt mit 4-Pipe-Fan-Coils und VAV-Lüftung tragfähig ist — auch wenn das für den Firmianstrasse-Use-Case zunächst Over-Engineering bedeutet.

---

## 2. Entscheidung

**Vollständige Umstrukturierung gemäß IFC 4.3 und VDI 3814:**

- Einführung einer **Terminal-Unit-Hierarchie** (Heizung/Kühlung/Lüftung am Raum)
- Einführung separater **Sensor-** und **Aktor-Entities** für Raum-Messwerte und -Stellgrößen
- Erweiterung der **Plant-Ebene** um `HeatPump`, `ThermalEnergyStorage`, `DistrictHeatingStation`
- Separate **Schedule-Entity** mit M:N-Beziehung zu Spaces
- **DistributionSystem** als logische Gruppierung mit Energieverbrauch
- **ExternalSpace** als eigener Typ für Außenbereiche
- **Pset-Records** als gekapselte Design-Anforderungen am Space (statt vermischt mit Runtime-Werten)
- **`SpaceType`-Enum erweitern** + IFC-konformes `PredefinedType`-Attribut mit USERDEFINED-Slot

Das Modell wird damit von ~19 auf ~35 CK-Types wachsen, ist dann aber IFC-/VDI-/Brick-konform und für reversible Aggregate, Mehrgewerk-Räume und Industrieanwendungen tragfähig.

---

## 3. Begründung

### 3.1 Allgemeingültigkeit erfordert IFC-Treue

Solange das Modell nur Firmianstrasse abdecken soll, reicht eine schlanke Variante mit Attributen am Raum. Für ein **wiederverwendbares Domain-Modell** über Wohn-, Büro- und Industriegebäude hinweg ist die strukturelle Trennung der VDI 3814 (Anlagen- vs. Raumautomation, Sensor/Aktor als eigene Geräte) der einzig saubere Weg. Sonst sammeln sich für jeden neuen Use Case weitere optionale Attribute am `Space` an, bis er unübersichtlich wird.

### 3.2 Passivkühlung als Testfall

Die Firmianstrasse hat eine Luft-Wasser-Wärmepumpe mit **Passivkühlung über dieselben Bodenheizschläuche** (Umschaltmodul). Das ist im aktuellen Modell nicht korrekt darstellbar:

- Es ist **kein Boiler** (Aggregat ist reversibel, kann auch kühlen)
- Es ist **kein Boiler + Chiller** (es gibt nur einen Hydraulikkreis, nicht zwei getrennte)
- Die Fußbodenheizung ist **ein** physisches Terminal mit Doppelfunktion, nicht "Radiator + ChilledBeam"

Beide Punkte zwingen zu:
- `HeatPump`-Typ mit `OperatingMode: Heating | PassiveCooling | ActiveCooling | Defrost | Standby`
- `RadiantSurface`-Terminal mit `IsReversible: Boolean` und modus-abhängiger Ventilstellung

### 3.3 Konsistenz mit existierenden Patterns

EnergyIQ macht es für Beleuchtung (`Luminaire.DimmingLevel`) und Sonnenschutz (`ShadingDevice.Position`) bereits **richtig** — separate Equipment-Entities am Raum. Heizung/Kühlung/Lüftung folgen jetzt demselben Muster — interne Konsistenz statt Sonderbehandlung.

### 3.4 Synergien mit Haystack-Projektion

Wenn Messwerte als separate Sensor-Entities modelliert sind, entfällt die "strukturelle Explosion" beim Haystack-Mapping (siehe `haystack-integration-concept.md` §3.3): ein EnergyIQ-`TemperatureSensor` ≈ ein PH-`Point`. Der Renderer wird einfacher, der reverse PH-Import (mittelfristig geplant) ebenfalls.

---

## 4. Neue Typ-Hierarchie

### 4.1 Spatial (geändert)

```
Site (Basic/Tree)
├── Building (Basic/TreeNode)
│   ├── BuildingStorey (Basic/TreeNode)
│   │   └── Space (Basic/TreeNode)              # entrümpelt
│   └── ExternalSpace (Basic/TreeNode)          # NEU - IfcExternalSpatialElement
└── ExternalSpace (Basic/TreeNode)              # direkt unter Site möglich
```

### 4.2 Plant Level (TechnicalSystem)

```
TechnicalSystem (abstract, Basic/TreeNode)
├── Boiler                                       # existierend - klassische Brennstoff-Kessel
├── Chiller                                      # existierend - reine Kältemaschinen
├── HeatPump                                     # NEU - reversibel, OperatingMode
├── ThermalEnergyStorage                         # NEU - Pufferspeicher
├── DistrictHeatingStation                       # NEU - Fernwärme-Übergabe (optional)
├── AirHandlingUnit                              # existierend
└── Pump                                         # existierend
```

### 4.3 Terminal Units (NEU)

```
RoomTerminal (abstract, BuildingElement)         # NEU - VDI 3814 Raumterminal
├── HydronicTerminal (abstract)                  # NEU - wasserbasiert
│   ├── Radiator                                 # NEU - nur Heizen, ein Ventil
│   ├── RadiantSurface                           # NEU - Boden/Decken/Wand, IsReversible
│   ├── ChilledBeam                              # NEU - i.d.R. Kühlen, optional H+C
│   └── FanCoilUnit                              # NEU - forced air + Wasser, 2/4-pipe
├── AirTerminal                                  # NEU - VAV-Box, Luftauslass, Klappenkasten
└── ElectricHeater                               # NEU - Elektroheizkörper, IR-Strahler
```

### 4.4 Sensoren (NEU)

```
Sensor (abstract, BuildingElement)               # NEU - IfcSensor
├── TemperatureSensor                            # CurrentValue, Unit
├── HumiditySensor
├── CO2Sensor
├── IlluminanceSensor
├── PresenceSensor                               # PIR / Bewegungsmelder
├── WindowContactSensor                          # binär
└── GenericSensor                                # Fallback für proprietäre/unbekannte Sensoren
```

### 4.5 Aktoren (NEU)

```
Actuator (abstract, BuildingElement)             # NEU - IfcActuator
├── Valve                                        # ValveType: Heating | Cooling | Reversible
├── Damper                                       # für Lüftung
├── Dimmer                                       # für Beleuchtung (eigenständig)
└── Motor                                        # für Sonnenschutz, Pumpen, Gebläse
```

Hinweis: `Luminaire`/`ShadingDevice`/`Window`/`Door` behalten ihre internen State-Attribute (`IsOn`, `DimmingLevel`, `Position`, `IsOpen` …). Sie sind das Building Element selbst — das interne Stellglied wird nicht künstlich ausgelagert.

### 4.6 Building Elements (Bestand, geringe Änderungen)

```
BuildingElement (abstract, Basic/NamedEntity)
├── Wall                                         # existierend
├── Door                                         # existierend
├── Window                                       # existierend
├── ShadingDevice                                # existierend
├── Luminaire                                    # existierend
├── RoomTerminal (abstract)                      # NEU - siehe 4.3
├── Sensor (abstract)                            # NEU - siehe 4.4
└── Actuator (abstract)                          # NEU - siehe 4.5
```

### 4.7 Logische Gruppierungen (NEU)

```
Schedule (Basic/NamedEntity)                     # NEU - M:N zu Spaces
- ScheduleEntries: RecordArray<ScheduleEntry>
- ScheduleType: Enum (Occupancy, Heating, Lighting, Custom)

DistributionSystem (Basic/NamedEntity)           # NEU - IfcDistributionSystem
- PredefinedType: Enum (Heating, Cooling, Ventilation, Electrical, Sanitary, DomesticHotWater)
- TotalEnergyConsumed: Double
```

### 4.8 PV-System (unverändert)

```
PhotovoltaicSystem, PVString, Inverter, BatteryStorage  # bleiben wie sind
```

---

## 5. Neue Assoziationen

| ID | Inbound/Outbound | Multiplicity | Zweck |
|---|---|---|---|
| `SpaceTerminals` | ContainedTerminals / ContainedInSpace | N : ZeroOrOne | Terminal-Units im Raum |
| `SpaceSensors` | ContainedSensors / ContainedInSpace | N : ZeroOrOne | Sensoren im Raum |
| `SpaceActuators` | ContainedActuators / ContainedInSpace | N : ZeroOrOne | Aktoren im Raum |
| `EquipmentSensors` | AttachedSensors / AttachedToEquipment | N : ZeroOrOne | Sensoren an Plant-Equipment (z.B. Boiler-Vorlauftemp) |
| `EquipmentActuators` | AttachedActuators / AttachedToEquipment | N : ZeroOrOne | Aktoren an Plant-Equipment |
| `TerminalActuators` | ContainedActuators / ContainedInTerminal | N : ZeroOrOne | Ventile am HydronicTerminal, Dämpfer am AirTerminal |
| `SystemMembers` | SystemMembers / MemberOfSystem | N : N | DistributionSystem ↔ TechnicalSystem/Terminal |
| `SpaceSchedules` | UsedSchedules / SchedulesAppliedTo | N : N | Schedule ↔ Space |
| `TerminalServedBy` | ServedBy / Serves | N : N | Terminal-Unit ↔ Plant-Equipment (Heizkreis-Zuordnung) |

Die existierenden Assoziationen `SpaceElements` und `SystemSpaces` bleiben für Wall/Door/Window/Luminaire/ShadingDevice bzw. die übergeordnete Systemversorgung.

---

## 6. Was sich am `Space` ändert

### 6.1 Bleibt

| Attribut | Begründung |
|---|---|
| `GlobalId`, `LongName`, `RoomNumber`, `RoomIdentifier` | IFC-Identifikation |
| `SpaceTypeValue` | erweiterter Enum (siehe 6.4) |
| `PredefinedType` *(NEU)* | IFC-konformer USERDEFINED-Slot |
| `NetFloorArea`, `GrossFloorArea`, `CeilingHeight`, `DesignOccupancy` | Master-Daten |
| `OperatingModeValue` | Raum-Betriebsmodus (VDI 3814, valide Raum-Ebene) |

### 6.2 Wird zu Pset-Records gekapselt

Statt einzelner Attribute → strukturierte Records am Space (entkoppelt Design-Anforderungen):

```yaml
# records/psetSpaceThermalRequirements.yaml
- recordId: PsetSpaceThermalRequirements
  attributes:
    - id: SpaceTemperature                  # Komfortwert
    - id: SpaceTemperatureMin                # untere Grenze
    - id: SpaceTemperatureMax                # obere Grenze
    - id: SpaceHumidity                      # Sollwert
    - id: SpaceHumidityMin
    - id: SpaceHumidityMax
    - id: CO2SetpointMax

# records/psetSpaceLightingRequirements.yaml
- recordId: PsetSpaceLightingRequirements
  attributes:
    - id: IlluminanceTarget
    - id: IlluminanceMin

# records/psetSpaceOccupancyRequirements.yaml
- recordId: PsetSpaceOccupancyRequirements
  attributes:
    - id: OccupancyType
    - id: OccupancyNumberPeak
    - id: AreaPerOccupant
```

Damit ersetzt:
- `TemperatureSetpointHeating`/`TemperatureSetpointCooling` → `PsetSpaceThermalRequirements.SpaceTemperatureMin/Max`
- `CO2Setpoint` → `PsetSpaceThermalRequirements.CO2SetpointMax`
- `IlluminanceSetpoint` → `PsetSpaceLightingRequirements.IlluminanceTarget`

### 6.3 Wandert ab

| Attribut | Wandert nach |
|---|---|
| `Temperature` | `TemperatureSensor` (separate Entity am Space) |
| `Humidity` | `HumiditySensor` |
| `CO2Level` | `CO2Sensor` |
| `Illuminance` | `IlluminanceSensor` |
| `PresenceDetected` | `PresenceSensor` |
| `WindowOpen` | weg (existiert an `Window.IsOpen`) |
| `HeatingValvePosition` | `Radiator.ValvePosition` oder `FanCoilUnit.HeatingValvePosition` oder `RadiantSurface.ValvePosition` |
| `CoolingValvePosition` | `FanCoilUnit.CoolingValvePosition` oder `ChilledBeam.ValvePosition` oder `RadiantSurface` (modus-abhängig) |
| `VentilationLevel` | `AirTerminal.DamperPosition` / `AirflowSetpoint` |
| `LightingLevel` | weg (existiert an `Luminaire.DimmingLevel`) |
| `ShadingPosition` | weg (existiert an `ShadingDevice.Position`) |
| `ScheduleEntries` | `Schedule`-Entity via `SpaceSchedules`-Assoziation |
| `EnergyConsumptionHeating/Cooling/Lighting/Total` | weg vom Space; pro-Raum-Verbrauch ist *calculated* (Aggregation aus DistributionSystem + Pro-Rata) — nicht im Modell gespeichert |

### 6.4 SpaceType-Enum-Erweiterung

```yaml
enums:
- enumId: SpaceType
  values:
  - { key: 0,  name: Office }
  - { key: 1,  name: MeetingRoom }
  - { key: 2,  name: Corridor }
  - { key: 3,  name: Toilet }
  - { key: 4,  name: Kitchen }
  - { key: 5,  name: TechnicalRoom }
  - { key: 6,  name: Storage }
  - { key: 7,  name: Parking }
  - { key: 8,  name: Lobby }
  - { key: 9,  name: Staircase }
  - { key: 10, name: Elevator }
  - { key: 11, name: ServerRoom }
  - { key: 12, name: Laboratory }
  - { key: 13, name: Workshop }
  - { key: 14, name: Other }
  # NEU - Wohnraumtypen
  - { key: 15, name: LivingRoom }
  - { key: 16, name: Bedroom }
  - { key: 17, name: Bathroom }
  - { key: 18, name: DiningRoom }
  - { key: 19, name: Lounge }
  - { key: 20, name: ChildrensRoom }
  - { key: 21, name: GuestRoom }
  - { key: 22, name: Laundry }
  - { key: 23, name: Garage }            # ist nicht ParkingSlot, sondern Räumlichkeit
  - { key: 24, name: WalkInCloset }
  # Für IFC-USERDEFINED → PredefinedType-Attribut mit Free-Text
```

Plus neues Attribut `PredefinedType: String (optional)` für IFC's USERDEFINED-Mechanismus.

### 6.5 Vorher / Nachher Zahlen

| | Vorher | Nachher |
|---|---|---|
| Attribute auf `Space` | 28 | 10–12 (inkl. 3 Pset-Records) |
| CK-Types gesamt | 19 | ~35 |
| Entities pro Raum (Firmianstrasse-Schnitt) | 1 | 4–8 (Space + 2–4 Sensoren + 1 Terminal + 1 Aktor) |

---

## 7. Firmianstrasse durchgespielt

Beispiel-Raum: **Wohnbereich** (Hauptgebäude EG, 45 m², 6 Personen Design-Belegung).

### Vorher (aktueller Stand)

```
1× Space (Wohnbereich) mit 26 belegten Attributen
1× Window (Süd-Fenster)
1× ShadingDevice (Raffstore Süd)
3× TechnicalSystem-Verknüpfungen (HZG-01, KWL-01 via SystemSpaces)
```

### Nachher (Variante C)

```
1× Space (Wohnbereich)
    - Pset: SpaceThermalRequirements { 21°C ± 2, max 1000ppm CO2 }
    - Pset: SpaceLightingRequirements { 300 lx }
    - Pset: SpaceOccupancyRequirements { peak=6 persons }
    - Verknüpfung → Schedule "Wohnen-Werktag"

1× TemperatureSensor (Raumthermostat Wohnbereich)
    - CurrentValue: 21.5 °C
    - Verknüpfung → Space via SpaceSensors

1× HumiditySensor (Raum-Feuchte Wohnbereich)
    - CurrentValue: 48.0 %RH
    - Verknüpfung → Space

1× CO2Sensor (Raumluft Wohnbereich)
    - CurrentValue: 650 ppm
    - Verknüpfung → Space

1× IlluminanceSensor
    - CurrentValue: 350 lx

1× PresenceSensor
    - CurrentValue: true (Bool)

1× WindowContactSensor (am Süd-Fenster)
    - CurrentValue: closed (Bool)
    - Verknüpfung → Window via EquipmentSensors

1× RadiantSurface (Fußbodenheizung Wohnbereich)
    - IsReversible: true                     # ← Passivkühlungs-fähig
    - OperatingMode: Heating
    - SupplyTemp (lokal): 32 °C
    - Verknüpfung → Space via SpaceTerminals
    - Verknüpfung → HeatPump via TerminalServedBy
    - 1× Valve (HeatingCoolingValve) via TerminalActuators
        - ValveType: Reversible
        - Position: 35.0 %

1× Window (Süd-Fenster)                      # bleibt wie ist
1× ShadingDevice (Raffstore Süd)             # bleibt wie ist
1× Luminaire(s) im Raum                      # falls modelliert
1× AirTerminal (KWL-Auslass Wohnbereich)
    - DamperPosition: 45 %
    - Verknüpfung → AirHandlingUnit (KWL-01) via TerminalServedBy
```

Plus auf Plant-Ebene (einmalig pro Haus, nicht pro Raum):

```
1× HeatPump (statt aktueller Boiler-Modellierung)
    - OperatingMode: Heating (kann auch PassiveCooling)
    - HeatSource: Air
    - COP: 3.8
    - SupplyTemp: 35 °C, ReturnTemp: 28 °C, ModulationLevel: 65 %
    - 1× Valve (Umschaltmodul Heizen/Kühlen)
        - ValveType: ChangeoverHeatingCooling
        - Position: 0 (Heizmodus)

1× ThermalEnergyStorage (Pufferspeicher)
    - Capacity, Temperature(en), Charging-State

1× DistributionSystem "Heizkreis EG/OG/DG"
    - PredefinedType: Heating
    - SystemMembers → HeatPump, RadiantSurface (jede)
    - TotalEnergyConsumed: aggregiert

1× DistributionSystem "Lüftung Hauptgebäude"
    - PredefinedType: Ventilation
    - SystemMembers → AirHandlingUnit, AirTerminal (jede)
```

Außenbereiche werden zu `ExternalSpace`:

```
ExternalSpace (Garten, Zufahrt, Terrasse EG, Dachterrasse OG, Balkon DG)
    - kein NetFloorArea-Erfordernis (oder als Außenfläche)
    - keine Pset_Thermal-/Lighting-Requirements
    - keine Sensoren als Pflichtteil
```

### Entitätszahl Firmianstrasse

| | Vorher | Nachher (geschätzt) |
|---|---|---|
| Spaces (inkl. Außen) | 22 | 17 (innen) + 5 ExternalSpace |
| Sensoren | 0 | ~50 (ø 3 pro Innenraum) |
| Aktoren | 0 | ~25 (Ventile, Dämpfer) |
| Terminals | 0 | ~30 (Heizflächen + Lüftungsauslässe) |
| Building Elements (Window/Shading/Luminaire) | 3 | ~50 (vollständig modelliert) |
| Plant-Equipment | 7 (Boiler+KWL+Pumpe+PV-Komponenten) | 8 (HeatPump + Speicher + Rest) |
| Schedules | 0 | 3–4 (Wohnen-Werktag, Wohnen-Wochenende, Büro, Abwesend) |
| DistributionSystems | 0 | 4 (Heizen, Lüften, Elektrisch, PV) |
| **Total RT-Entities** | **~35** | **~190** |

Das ist eine 5–6-fache Entity-Multiplikation. Tragbar, weil die Mehrzahl identisch strukturierter Entities ist (Sensoren/Aktoren/Terminals) und über Mapping-Templates instanziiert werden kann.

---

## 8. Mapping-Konsequenzen

### 8.1 Loxone → EnergyIQ (klarer)

Ein Loxone `IRoomController` liefert via `Control.States` mehrere Datenpunkte (`actualTemp`, `targetTemp`, `mode`, `comfortTemp`). Nach Restrukturierung mappt jeder Datenpunkt auf eine *eigene* EnergyIQ-Entity:

```
Loxone Control (IRoomController, UUID xyz)
  States[Name='actualTemp'].CurrentValue
    → DataPointMapping → TemperatureSensor.CurrentValue (für diesen Raum)
  States[Name='targetTemp'].CurrentValue
    → DataPointMapping → Space.PsetSpaceThermalRequirements.SpaceTemperature
  States[Name='mode'].CurrentValue
    → DataPointMapping → Space.OperatingMode (mit Enum-Mapping)
```

Vorher: 1 Loxone-Control → 1 Space mit N gemappten Attributen (Pfade in DataPointMapping).
Nachher: 1 Loxone-Control → N Targets, jeder mit eigener DataPointMapping. Sauberer, weil jedes Mapping einen klaren 1:1-Charakter hat.

### 8.2 Haystack-Projektion (einfacher)

Aus dem `haystack-integration-concept.md` §3.3: "strukturelle Explosion" Attribut → Point. Mit Sensor-Entities entfällt das:

```
Vorher: Space.Temperature → erzeuge separates PH-Point-Dict
Nachher: TemperatureSensor → 1 PH-Point-Dict (1:1)
```

Die Mapping-Config wird kompakter und der Renderer einfacher. Das `points:`-Konstrukt in der Haystack-Mapping-Config (Attribut → Point-Explosion) wird durch direkte Type-Mappings ersetzt.

### 8.3 Update am Haystack-Konzept

Folgende Änderungen werden in `haystack-integration-concept.md` nachgezogen, nachdem dieses Refactoring umgesetzt ist:
- §3.3 Impedance-Mismatch wird kleiner — Hinweis auf erfolgte Restrukturierung
- §5 Typ-Mapping-Tabelle gewinnt Einträge für Sensor/Actuator/Terminal/etc.
- `points:`-Sektion im Mapping-Schema entfällt für Räume; bleibt für Plant-Equipment (Boiler/HeatPump/AHU haben weiter mehrere Werte am Equipment selbst)

---

## 9. Migrationspfad

### 9.1 EnergyIQ 2.0.0 (Breaking Change)

`EnergyIQ-2.0.0` enthält **beide** Refactorings gemeinsam:

- Haystack-Cleanup (siehe `haystack-integration-concept.md` §7)
- Space-/Equipment-Restrukturierung (dieses Dokument)

### 9.2 RT-Datenmigration Firmianstrasse

`data/bim/rt-firmianstrasse.yaml` wird komplett neu geschrieben:

1. Innen-Spaces behalten ihre `rtId`, werden entrümpelt
2. Außenbereiche werden zu `ExternalSpace`-Entities (eigene `rtId` muss neu vergeben werden, weil Type-Change)
3. Sensoren werden generiert (pro Raum: Temperatur-, Feuchte-, CO2-, Illuminance-, Presence-Sensor, jeweils nur wo Daten vorhanden sind)
4. Terminals werden modelliert (Fußbodenheizung pro Raum als `RadiantSurface`, KWL-Auslässe als `AirTerminal`)
5. Aktoren (Ventile, Klappen) als separate Entities pro Terminal
6. `Boiler` (Wärmepumpe) wird zu `HeatPump`
7. Schedules werden definiert (Wohnen-Werktag, Wochenende, etc.)
8. DistributionSystems anlegen und Members zuordnen
9. Pset-Records am Space befüllen (Sollwerte)

Geschätzter Migrationsaufwand RT-Datei: 1–2 Tage (manuelle Modellierung).

### 9.3 Externe Konsumenten

Keine externen Konsumenten von EnergyIQ-RT bekannt — Breaking Change ist daher tolerierbar. Falls künftig welche entstehen: Versionsbump signalisiert klare Inkompatibilität.

---

## 10. Phasenplan

| Phase | Inhalt | Liefergegenstand | Voraussetzung |
|---|---|---|---|
| **1. CK-Modell-Erweiterung** | Neue Typen anlegen: `HeatPump`, Terminal-Hierarchie, Sensor/Actuator-Hierarchie, `Schedule`, `DistributionSystem`, `ExternalSpace`. Neue Records (`Pset*`). Neue Assoziationen. Neue Enum-Werte. | Vollständiges `EnergyIQ-2.0.0` CK-Definition; baut grün | Konzept-Abnahme |
| **2. `Space` entrümpeln** | Attribute entfernen, die abwandern. Pset-Record-Attribute hinzufügen. | `EnergyIQ-2.0.0` final | Phase 1 |
| **3. Haystack-Cleanup** | (parallel zu 2 möglich) Haystack-Mixins raus, siehe `haystack-integration-concept.md` §7 | siehe Haystack-Konzept | unabhängig |
| **4. RT-Datenmigration Firmianstrasse** | `rt-firmianstrasse.yaml` komplett neu gemäß §9.2 | Vollständig modelliertes Beispiel | Phase 2+3 |
| **5. Dokumentation** | `developer-guide.md`, `construction-kit.md`, `standards-reference.md` aktualisieren | Konsistente Doku | Phase 2+3 |
| **6. Mapping-Config** | Haystack-Mapping-Config schreiben (siehe `haystack-integration-concept.md` Phase 1) | Vollständige PH4-Mapping-Config | Phase 1 |
| **7. Loxone-Mapping** | Bestehende DataPointMappings für Firmianstrasse-Loxone-Anbindung neu strukturieren (Sensor-targeted statt Space-attribute-targeted) | Funktionierende Loxone→EnergyIQ-Pipeline | Phase 4 |

Phasen 1–3 können in einem Schritt umgesetzt werden (großer Sprung auf 2.0.0). Danach 4–7 sequentiell oder teilparallel.

---

## 11. Beziehung zu anderen Konzepten

- **`haystack-integration-concept.md`**: Wird durch dieses Refactoring vereinfacht — Sensor-Entities ersetzen die "Point-Explosion" beim PH-Export. Nach Umsetzung dieses Konzepts wird das Haystack-Doc nachgezogen.
- **Loxone-CK (`Loxone-4.3.0`)**: Unverändert. Die Loxone-Quellseite bleibt wie sie ist; die Mapping-Targets werden granularer (Sensor statt Space-Attribut). DataPointMapping (`System.Communication`) unverändert.
- **Bestehender `haystack-adapter-concept.md`** (REST-API-Spec): Konsumiert weiterhin die Render-Pipeline aus Haystack-Konzept; profitiert indirekt durch klareres EnergyIQ.

---

## 12. Out-of-Scope (für spätere Erweiterung vorgemerkt)

Folgende Use Cases sind **nicht** Teil dieses Refactorings, aber kompatibel ergänzbar:

- Lüftung mit Wärmerückgewinnung pro Raum (dezentrale Geräte) — wäre als zusätzlicher `RoomTerminal`-Subtyp ergänzbar
- Solarthermie — als TechnicalSystem-Subtyp `SolarThermalCollector` mit eigenem Speicher-Anschluss
- BHKW (Blockheizkraftwerk) — als TechnicalSystem-Subtyp `CombinedHeatAndPower` mit elektrischer + thermischer Output-Modellierung
- Pelletkessel, Holzvergaser — als `Boiler`-PredefinedType-Variante oder eigener Subtyp
- Lüftungsanlagen mit Kreuz-/Rotationswärmetauscher — bereits durch `AirHandlingUnit.HeatRecoveryEfficiency` abgedeckt; spezifische Tauscher-Subtypen optional
- Sole-/Wasser-Speicher (separate Modellierung von Pufferspeicher und Trinkwasser-Erwärmer) — `ThermalEnergyStorage` mit `PredefinedType`-Erweiterung
- Smart-Meter und Submetering — eigenes `EnergyMeter`-Sensor-Subtyp + Verknüpfung zu DistributionSystem
- Wallbox/E-Ladestation — als TechnicalSystem-Subtyp `ElectricVehicleCharger`
- Heizkurve / wetterabhängige Regelung — als Schedule-Erweiterung oder eigenes `WeatherCompensationCurve`-Record
- IFC-Export selbst (Generierung von .ifc-Dateien aus EnergyIQ) — eigene Workstream

---

## 13. Offene Punkte (vor Phase 1)

- **`PredefinedType` als String oder als zusätzliches Enum?** Vorschlag: String mit Konvention (`USERDEFINED:Wohnzimmer` für Free-Text-Erweiterung, leer wenn Enum-Wert ausreicht). Alternative: paralleles Enum mit USERDEFINED-Key + Free-Text-Attribut. → Klärung in Phase 1.
- **Sensor-`CurrentValue`-Typ:** TemperatureSensor liefert Double, PresenceSensor liefert Bool, GenericSensor liefert String. Drei Optionen: (a) Pro Sensor-Subtyp eigenes typisiertes Attribut, (b) String + Konvertierung, (c) Polymorphes Record. Vorschlag (a) — pro Subtyp das passende `valueType`.
- **Naming-Konvention für Sensor-Subtyp-Werte:** `CurrentValue` vs `Temperature` vs `Value`? Loxone-CK nutzt `CurrentValue` (passt zum `DataPoint`-Record). Vorschlag: einheitlich `CurrentValue` an jedem Sensor.
- **Aktor-Status vs. Aktor-Command:** Braucht jeder Aktor `Position` (Istwert) + `PositionSetpoint` (Sollwert)? Vorschlag: ja, analog zum `ShadingDevice`-Pattern.
- **`HeatPump.HeatSource`:** Enum `Air | Ground | Water | Exhaust` — sinnvoll oder als String? Vorschlag: Enum.
- **`Valve.ValveType`:** Enum `Heating | Cooling | Reversible | ChangeoverHeatingCooling | Mixing | Bypass`. Final festziehen in Phase 1.
- **DistributionSystem-Energieverbrauch:** Stored vs. derived? Vorschlag: stored als Snapshot, mit `LastUpdatedAt`-Attribut für Aktualität.
- **Verzeichnis-Aufteilung im CK-Repo:** Bei 35 Typen empfiehlt sich evtl. Sub-Ordner-Struktur (`types/spatial/`, `types/plant/`, `types/terminal/`, `types/sensor/`, `types/actuator/`). Klärung in Phase 1.
