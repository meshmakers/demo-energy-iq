# Haystack Integration Concept

**Status:** EnergyIQ-Cleanup + Mapping-Config umgesetzt (EnergyIQ-2.0.0); Renderer-Implementierung offen
**Datum:** 2026-05-20
**Bezug:** Ergänzt `haystack-adapter-concept.md` (REST-API-Spec) um die zugrundeliegende Datenmodell-Architektur.

> **Update 2026-05-21 — Phase 1 + 2 umgesetzt:** Die EnergyIQ-Bereinigung wurde gemeinsam mit dem Space-Restructuring (siehe `space-restructuring-concept.md`) in `EnergyIQ-2.0.0` ausgeliefert. Haystack-Mixin-Attribute (`HaystackTags`/`HaystackRefs`/`HaystackMeta`) und der `HaystackRef`-Record sind entfernt. Sensoren/Aktoren/Terminals sind nun eigene Entities — die in §3.3 beschriebene "strukturelle Explosion" Attribut→Point ist damit für Sensoren *nicht mehr nötig* (1 Sensor = 1 PH-Point). Das `points:`-Konstrukt im Mapping-Schema wird primär noch für Plant-Equipment, Aktoren und Building-Elements (HeatPump/Boiler/AHU/Valve/Damper/Luminaire/… mit mehreren Werten am Equipment selbst) gebraucht.
>
> Die deklarative **Mapping-Config** liegt unter `src/EnergyIqHaystackMapping/mapping/` (35 Type-Mapping-YAMLs + `_index.yaml` + `README.md`). Sie deckt alle nicht-abstrakten EnergyIQ-2.0.0-Typen ab — Spatial (5), Sensoren (7), Aktoren (4), Terminals (6), Plant-Equipment (6), Building-Elements (4) und PV-Kette (4). Wand-Typ und logische Aggregat-Entities (Schedule, DistributionSystem) sind bewusst nicht gemappt (siehe `README.md`).
>
> Nächste Phase: Export-Renderer (Phase 3) — konsumiert diese Mapping-Config und emittiert PH4-Grids.

---

## 1. Ausgangslage und Frage

Das EnergyIQ-CK-Modell trägt aktuell an jedem nicht-abstrakten Typ drei Haystack-Mixin-Attribute (`HaystackTags`, `HaystackRefs`, `HaystackMeta`) sowie einen `HaystackRef`-Record. Im Vergleich dazu wurde für die OT-Anbindung (Loxone) ein **eigenes CK-Modell** (`Loxone-4.3.0`) angelegt, das über `System.Communication/DataPointMapping` (mit `MapsFrom`/`MapsTo` + `MappingExpression`) an EnergyIQ angebunden wird.

**Frage:** Soll Haystack analog zu Loxone ein eigenes CK-Modell bekommen, oder ist Haystack eher eine *Schnittstelle/Projektion* über dem EnergyIQ-Datenmodell?

**Aktuelle und mittelfristige Use-Cases:**
- **Jetzt:** Datenfluss entweder Loxone *oder* Haystack — kein paralleler Schreibzugriff auf dieselbe Größe. Haystack-Richtung primär Export (historisch/REST-API).
- **Mittelfristig:** Ein Project-Haystack-Server (z.B. SkySpark, FIN Framework) als Datenquelle/-senke — dafür ist ein PH-Adapter sinnvoll.

---

## 2. Entscheidung

**Haystack wird als Projektion/Interface über EnergyIQ implementiert, nicht als paralleles CK-Modell.**

Die Mapping-Logik ist eine deklarative Konfiguration (YAML-Asset, ein File pro EnergyIQ-Type), aus der drei Artefakte generiert werden:
1. **Export-Renderer** (RT-Entity → PH4-Grid in Zinc/JSON/Trio)
2. **PH4-`lib`-Definition** (formale Spec-Library für PH4-konsumierende Tools)
3. **Import-Mapper** (PH4-Grid → EnergyIQ-RT-Entity, später für PH-Adapter)

Die Haystack-Mixin-Attribute werden aus EnergyIQ entfernt; das Modell bleibt rein IFC/VDI.

---

## 3. Begründung

### 3.1 Loxone und Haystack sind nicht symmetrisch

| Aspekt | Loxone | Haystack |
|---|---|---|
| Was es ist | proprietäre Datenstruktur einer OT-Anlage | herstellerneutrales semantisches Vokabular |
| Identität nötig? | ja — `LoxoneUuid`, Polling-Targets, Adapter-Config | nein — die Domain-Entity *ist* die Haystack-Entity |
| Quellstruktur | eigene Hierarchie (Miniserver/Room/Control/SubControl) | Tags *beschreiben* eine andere (Domain-)Struktur |
| Metamodell | proprietäres OO-Modell | formales Spec-System (PH4) — selbst CK-ähnlich |

Loxone braucht ein eigenes CK, weil die Quellstruktur fundamental anders ist. Haystack ist Beschreibungssprache, die unsere CK-Modelle annotiert.

### 3.2 PH4 ist semantisch fast identisch zu CK

Project Haystack 4 hat ein formales Spec-System (`lib`, `spec`, Fantom-typisierte Tags). Die Konzepte mappen 1:1:

| CK | PH4 |
|---|---|
| CK Model (`EnergyIQ-1.0.1`) | `lib` (`energyIq`) |
| CK Type | `spec` |
| Attribute (mit `valueType`) | typed tag slot (`Marker`, `Str`, `Number`, `Ref`, …) |
| Enum | constrained `Str` mit Choices |
| `derivedFromCkTypeId` | spec inheritance |
| Association | `Ref`-Slot mit `of:`-Constraint |
| RT-Entity | dict |
| Collection RT-Entities | grid |

Ein eigenes Haystack-CK-Modell würde bedeuten, **ein Metamodell innerhalb eines Metamodells** zu modellieren — Doppelung ohne semantischen Mehrwert. EnergyIQ-CK *ist* bereits konzeptuell ein PH4-`lib`; was fehlt, ist die Übersetzung der Begrifflichkeit und ein Wire-Format-Renderer.

### 3.3 Strukturelle Impedance verbietet 1:1-Materialisierung

EnergyIQ folgt: *measurements are attributes ON objects* (`Space.Temperature` als Attribut).
Haystack folgt: **Points sind First-Class-Entities** mit Refs zurück zum Space/Equip.

Beispiel: ein EnergyIQ-`Space` mit ~17 Mess-/Soll-/Stellgrößen wird in Haystack zu:
- 1 Dict `Space` (Markers `space`, `room`)
- ~17 Dicts `Point` (Markers `point sensor temp air zone`, mit `spaceRef` zurück)

Das ist eine **strukturelle Explosion**, kein 1:1-Mapping. Genau deshalb taugt es nicht als parallel materialisiertes RT-Modell — bei vollständiger Materialisierung hätten wir ein Vielfaches der Entities und müssten sie synchron halten. Eine Projektion löst das on-the-fly.

### 3.4 Keine Konfliktauflösung nötig

Da nie gleichzeitig beide Kanäle (Loxone und Haystack) dieselbe Größe schreiben, entfällt die Source-of-Truth-Diskussion. Beide Datenflüsse können unabhängig modelliert werden.

---

## 4. Architektur

```
                          ┌─────────────────────────────────────┐
                          │  EnergyIQ-CK (Domain, IFC/VDI)      │
                          │  - 19 Types, 30 Attributes          │
                          │  - KEINE Haystack-Mixins mehr       │
                          └────────────┬────────────────────────┘
                                       │
                          ┌────────────▼────────────────────────┐
                          │  EnergyIqHaystackMapping (NEU)      │
                          │  Deklarative YAML-Config            │
                          │  - ein File pro EnergyIQ-Type       │
                          │  - PH4-Spec + Markers + Tags        │
                          │  - Attribute → Tag oder Point       │
                          │  - Refs (Parent/Ancestor)           │
                          └────┬───────────────────┬───────────┘
                               │                   │
                ┌──────────────┴───────┐   ┌──────┴────────────────┐
                ▼                      ▼   ▼                       ▼
  ┌────────────────────────┐  ┌──────────────────┐   ┌────────────────────────┐
  │ Export-Renderer        │  │ PH4-lib-Generator│   │ Import-Mapper          │
  │ (jetzt)                │  │ (jetzt/später)   │   │ (mittelfristig)        │
  │ RT-Entity → Grid       │  │ → Trio-Lib für   │   │ Grid → RT-Entity       │
  │ Zinc/JSON-Output       │  │   SkySpark/FIN   │   │ via PH-Adapter         │
  └────────────────────────┘  └──────────────────┘   └────────────────────────┘
                                                                  ▲
                                                                  │
                                                     ┌────────────┴────────────┐
                                                     │ PH-Adapter (eigenes CK)│
                                                     │ analog Loxone-Adapter   │
                                                     │ - PH-Server-Connection  │
                                                     │ - KEIN Haystack-Daten-  │
                                                     │   modell (Mapping       │
                                                     │   nutzt Projektion)     │
                                                     └─────────────────────────┘
```

### 4.1 Projekt- und Dateistruktur

```
demo-energy-iq/
├── src/
│   ├── EnergyIqCkModel/                       # bereinigt (Haystack-Attribute raus)
│   └── EnergyIqHaystackMapping/               # NEU
│       └── mapping/
│           ├── _index.yaml                    # Lib-Metadaten
│           ├── site.yaml
│           ├── building.yaml
│           ├── buildingStorey.yaml
│           ├── space.yaml                     # umfangreichster Mapping
│           ├── boiler.yaml
│           ├── chiller.yaml
│           ├── airHandlingUnit.yaml
│           ├── pump.yaml
│           ├── photovoltaicSystem.yaml
│           ├── pvString.yaml
│           ├── inverter.yaml
│           ├── batteryStorage.yaml
│           ├── shadingDevice.yaml
│           └── luminaire.yaml
└── docs/
    ├── haystack-integration-concept.md        # dieses Dokument
    └── haystack-adapter-concept.md            # REST-API-Spec (bestand)
```

### 4.2 Mapping-Schema

**Lib-Metadaten (`_index.yaml`):**

```yaml
phLib:
  name: energyIq
  version: 1.0.0
  haystackVersion: "4.0"
  baseLib: ph                 # PH-Standard-Lib (Site, Space, Equip, Point)
```

**Pro CK-Type (Beispiel-Schema):**

```yaml
ckTypeId: EnergyIQ/Space
phSpec: ph::Space             # PH4-Spec, die die Entity erfüllt
markers:                      # Marker-Tags, die immer gesetzt werden
  - space
  - room
tags:                         # statische Value-Tags
  - { name: tz, value: "Europe/Vienna" }     # aus Tenant-Context ableiten

# Ref-Mapping: CK-Association → PH-Ref-Slot
refs:
  - phRef: spaceRef
    sourceRole: ParentChild   # CK-Association (inbound)
    direction: parent         # parent | children | ancestor
  - phRef: siteRef
    sourceRole: ParentChild
    direction: ancestor       # traverse bis Site
    targetCkTypeId: EnergyIQ/Site

# Attribut → entweder Dict-Tag (Master-Daten) oder ausgelagerter Point (Messwerte)
attributes:
  - ckAttribute: NetFloorArea
    phTag: area
    kind: Number
    unit: "m²"
  - ckAttribute: CeilingHeight
    phTag: height
    kind: Number
    unit: "m"
  - ckAttribute: RoomNumber
    phTag: navName
    kind: Str
  - ckAttribute: SpaceTypeValue
    phTag: spaceType
    kind: Str
    enumMapping:              # CK enum key → PH string
      0: office
      1: meetingRoom
      # ...

# Messwerte/Sollwerte/Stellgrößen → separate Point-Entities mit Ref zurück
points:
  - ckAttribute: Temperature
    markers: [point, sensor, temp, air, zone]
    kind: Number
    unit: "°C"
    refTo: { phRef: spaceRef, target: self }
    navName: "Temp"
  - ckAttribute: TemperatureSetpointHeating
    markers: [point, sp, temp, air, zone, heating]
    kind: Number
    unit: "°C"
    writable: true
  - ckAttribute: PresenceDetected
    markers: [point, sensor, occupied]
    kind: Bool
```

---

## 5. Typ-Mapping-Übersicht (19 EnergyIQ-Typen)

| EnergyIQ-Type | PH4-Spec | Marker | Anzahl Points |
|---|---|---|---|
| `Site` | `ph::Site` | `site` | 0 (`geoCoord`, `geoElevation` als Tags) |
| `Building` | `ph::Space` | `space, building` | 0 (Master-Daten als Tags) |
| `BuildingStorey` | `ph::Space` | `space, floor` | 0 |
| `Space` | `ph::Space` | `space, room` | **~17** (Messwerte/Sollwerte/Stellgrößen/Verbräuche) |
| `Door` | `ph::Equip` | `equip, door` | 2 (IsOpen, IsLocked) |
| `Window` | `ph::Equip` | `equip, window` | 2 (IsOpen, OpeningPosition) |
| `ShadingDevice` | `ph::Equip` | `equip, shade` | 4 (Position, SlatAngle + 2 Setpoints) |
| `Luminaire` | `ph::Equip` | `equip, light, lighting` | 3 (IsOn, DimmingLevel + Setpoint) |
| `Boiler` | `ph::Equip` | `equip, hot-water-plant, boiler` | 6 |
| `Chiller` | `ph::Equip` | `equip, chilled-water-plant, chiller` | 6 |
| `AirHandlingUnit` | `ph::Equip` | `equip, ahu` | 11 |
| `Pump` | `ph::Equip` | `equip, pump, motor` | 4 |
| `PhotovoltaicSystem` | `ph::Equip` | `equip, solar, plant` | 5 |
| `PVString` | `ph::Equip` | `equip, solar, array` | 3 + Installations-Tags |
| `Inverter` | `ph::Equip` | `equip, inverter, ac, dc` | 7 |
| `BatteryStorage` | `ph::Equip` | `equip, ess, battery` | 6 |
| `Wall` | *(ausgeschlossen)* | — | — |
| `TechnicalSystem` | (abstract) | — | — |
| `BuildingElement` | (abstract) | — | — |

### 5.1 Räumliche Hierarchie (via Refs)

PH4-Hierarchie wird über `siteRef`/`spaceRef`/`equipRef` ausgedrückt:

```
Site
 └── Building (Space + building)         siteRef
      └── BuildingStorey (Space + floor) siteRef + spaceRef
           └── Space (Space + room)      siteRef + spaceRef
                ├── Equip (Luminaire/ShadingDevice etc.)  spaceRef + siteRef
                └── Point (alle Messwerte)                spaceRef + siteRef

Boiler/AHU/Pump (Equip, gebäudeweit)     siteRef + spaceRef (auf Building)
 └── Point (Boiler-Messwerte)            equipRef + siteRef

PhotovoltaicSystem (Equip + solar + plant) siteRef + spaceRef (Building)
 ├── PVString (Equip + solar + array)      equipRef → PV-System
 ├── Inverter (Equip + inverter)           equipRef → PV-System
 └── BatteryStorage (Equip + ess + battery) equipRef → PV-System
```

---

## 6. Aufgelöste Design-Entscheidungen

| Frage | Entscheidung | Begründung |
|---|---|---|
| Building-Modellierung | `ph::Space` + `building`-Marker, nicht als eigenes `Site` | PH4-konform, nestbar via `spaceRef`; eine Property kann mehrere Buildings haben (Hauptgebäude/Nebengebäude im Beispiel) |
| Wall-Mapping | **Ausgeschlossen** vom Haystack-Export | PH-Welt ist control/energy-focused; Wände sind reine BIM-Strukturinfo, in PH typischerweise nicht modelliert |
| Door/Window-Mapping | Eingeschlossen als `ph::Equip` mit Custom-Markern | Tragen relevante State-Points (`IsOpen`, `IsLocked`, `OpeningPosition`) für Energie-/HVAC-Kontext |
| ShadingDevice/Luminaire | Eingeschlossen als `ph::Equip` mit Custom-Markern | Echte HVAC-/Lighting-Relevanz, sind cmd-Targets |
| Mapping-Storage | YAML-Asset (Code-Repo), nicht runtime-editierbare Entities | Mapping ist *typabhängig*, nicht *instanzabhängig* — alle Spaces werden gleich exportiert. Pro-Tenant-Customization erstmal nicht nötig |
| Mapping-Symmetrie | Eine Config für Export, Import und Lib-Gen | Single Source of Truth, vermeidet Drift zwischen Richtungen |
| Konfliktauflösung Loxone↔Haystack | Nicht nötig | Aktuelle Use-Cases haben nur eine Quelle pro Größe |

---

## 7. EnergyIQ-Bereinigung

### 7.1 Zu entfernen

Aus `src/EnergyIqCkModel/ConstructionKit/`:

- `attributes/haystack.yaml` (definiert `HaystackTags`, `HaystackMeta`)
- `attributes/haystackRefs.yaml`
- `records/haystackRef.yaml`
- In **allen 14 nicht-abstrakten Typen** (`site.yaml`, `building.yaml`, `buildingStorey.yaml`, `space.yaml`, `wall.yaml`, `door.yaml`, `window.yaml`, `shadingDevice.yaml`, `luminaire.yaml`, `boiler.yaml`, `chiller.yaml`, `airHandlingUnit.yaml`, `pump.yaml`, `photovoltaicSystem.yaml`, `pvString.yaml`, `inverter.yaml`, `batteryStorage.yaml`) und in den abstrakten Basistypen `buildingElement.yaml`/`technicalSystem.yaml`:
  - `HaystackTags`-Eintrag
  - `HaystackRefs`-Eintrag
  - `HaystackMeta`-Eintrag

### 7.2 Versionsbump

`EnergyIQ-1.0.1` → **`EnergyIQ-2.0.0`** (Breaking Change: entfernte Attribute).

### 7.3 RT-Beispieldaten

In `data/bim/rt-firmianstrasse.yaml`:
- alle `EnergyIQ/HaystackTags`-Einträge entfernen (~30 Stellen)
- `dependencies:` von `EnergyIQ-1.0.0` auf `EnergyIQ-2.0.0` heben

### 7.4 Dokumentation

- `docs/developer-guide.md`: Haystack-Mixin-Erwähnungen entfernen, Verweis auf dieses Dokument einfügen
- `docs/construction-kit.md`: Haystack-Attribute aus Type-Beschreibungen entfernen

---

## 8. Phasenplan

| Phase | Inhalt | Liefergegenstand | Voraussetzung |
|---|---|---|---|
| **1. Mapping-Config** | `_index.yaml` + 16 Type-Mapping-YAMLs in `EnergyIqHaystackMapping/mapping/` schreiben | Vollständige, review-fähige Spezifikation; keine Code-Änderungen | — |
| **2. EnergyIQ-Cleanup** | Haystack-Attribute/Records aus CK entfernen, Version `2.0.0`, RT-Daten anpassen, Doku aktualisieren | EnergyIQ-2.0.0 ohne Haystack-Mixins; bestehende Tests/Builds grün | Phase 1 |
| **3. Export-Renderer** | Liest RT-Entities + Mapping-Config, emittiert PH4-Grid (Zinc + JSON) | CLI/Service der `rt-firmianstrasse.yaml` → vollständiges PH4-Output rendert, in SkySpark/FIN lesbar | Phase 1+2 |
| **4. PH4-`lib`-Generator** | Aus Mapping-Config Trio-File für PH4 generieren | EnergyIQ als formales PH4-`lib` registrierbar (`energyIq.lib`) | Phase 1 |
| **5. PH-Adapter** *(mittelfristig)* | Eigenes Adapter-CK (analog Loxone) für PH-Server-Connection; Reverse-Mapping nutzt dieselbe Config | PH-Server als Datenquelle anschließbar | Phase 1+3 |

Phasen 3 und 4 können parallel laufen; beide bauen nur auf Phase 1+2 auf.

---

## 9. Beziehung zu anderen Konzepten

- **`haystack-adapter-concept.md`** (REST-API-Spec): Beschreibt den *Server-Endpunkt* (Haystack REST `read`/`nav`/`hisRead`/…). Dieser konsumiert die in Phase 3 implementierte Render-Logik. Der Renderer liefert die Grids; der Adapter exposed die HTTP-Endpunkte.
- **Loxone-CK + `DataPointMapping`**: Bleibt unverändert. Beide Welten existieren parallel — Loxone als OT-Quelle mit eigenem CK, Haystack als semantische Projektion ohne CK.
- **EnergyIQ-CK**: Wird auf `2.0.0` gehoben und bleibt der semantische Kern.

---

## 10. Offene Punkte (nach Abnahme dieses Konzepts)

- **Unit-System:** PH4 hat eine umfangreiche Unit-Bibliothek (`unit.txt`). Die Mapping-Config muss PH-konforme Unit-Strings verwenden (`"°C"`, `"%RH"`, `"ppm"`, `"kWh"`, `"kW"`, `"kVA"`, `"V"`, `"A"`, `"Hz"`, `"m²"`, `"m"`, …). Liste wird in Phase 1 finalisiert.
- **`tz`-Tag-Quelle:** Aktuell als statisch (`Europe/Vienna`) gedacht — sollte langfristig aus Tenant-Konfiguration oder Site-Attribut kommen. Vorerst pro Site-Mapping setzbar.
- **`navName`-Konvention:** Welcher CK-Attribut wird zu `navName` (PH-Display-/Navigationsname)? Vorschlag: `System/Name` für Spatial-Entities, `Identifier` für TechnicalSystems/PV.
- **PH-Ref-ID-Schema:** Ableitung aus `rtId` oder aus `GlobalId`? Vorschlag: `GlobalId` wenn vorhanden, sonst `rtId`. Muss stabil und tenant-eindeutig sein.
