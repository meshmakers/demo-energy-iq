# Standards-Referenz

## Übersicht: Standards-Landschaft

```
                    BIM / Planung                    Betrieb / IoT
                         │                                │
              ┌──────────┴──────────┐         ┌──────────┴──────────┐
              │                     │         │                     │
         ISO 16739              VDI 3814              Project
           (IFC)                  (GA)               Haystack
              │                     │                    │
         "Struktur"           "Funktionen"          "Semantik"
         Was ist wo?          Wie wird geregelt?    Was bedeutet
         (Räume, Wände)       (Regler, Zeitprog.)   der Datenpunkt?
              │                     │                    │
              └──────────┬──────────┴────────────────────┘
                         │
                         ▼
                    ┌─────────┐
                    │EnergyIQ │  ← Kombiniert alle drei
                    └─────────┘
```

---

## ISO 16739-1:2024 (IFC 4.3)

### Übersicht

**Industry Foundation Classes (IFC)** ist ein offener internationaler Standard für Building Information Modeling (BIM). Die aktuelle Version IFC 4.3 (ISO 16739-1:2024) erweitert den Scope auf Infrastruktur (Brücken, Straßen, Schienen).

**Herkunft:** Architektur/Bauwesen (buildingSMART)  
**Fokus:** Planung & Bau ("As-Built")  
**Daten:** Statisch (Geometrie, Struktur, Eigenschaften)

### Relevante Konzepte für EnergyIQ

#### Räumliche Struktur (Spatial Structure)

IFC definiert eine hierarchische räumliche Gliederung:

```
IfcProject
└── IfcSite                    → Site
    └── IfcBuilding            → Building
        └── IfcBuildingStorey  → BuildingStorey
            └── IfcSpace       → Space
```

Die Beziehungen werden über `IfcRelAggregates` hergestellt.

#### Spatial Containment

Bauelemente werden Räumen zugeordnet über `IfcRelContainedInSpatialStructure`:

```
IfcSpace ◄── IfcRelContainedInSpatialStructure ──► IfcWall, IfcDoor, IfcWindow, ...
```

#### Property Sets

IFC verwendet PropertySets für zusätzliche Eigenschaften:
- `Pset_SpaceCommon` – allgemeine Raumeigenschaften
- `Pset_SpaceThermalRequirements` – thermische Anforderungen
- `Pset_SpaceOccupancyRequirements` – Belegungsanforderungen

**Mapping zu OctoMesh:** PropertySets → Records oder direkte Attribute

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

### Quellen

- [buildingSMART IFC Specification](https://technical.buildingsmart.org/standards/ifc/)
- [IFC 4.3 Documentation](https://ifc43-docs.standards.buildingsmart.org/)
- [ISO 16739-1:2024](https://www.iso.org/standard/84123.html)

---

## VDI 3814 (Gebäudeautomation)

### Übersicht

Die **VDI-Richtlinie 3814** beschreibt den Stand der Technik bei Planung und Errichtung von Gebäudeautomation (GA). Sie wurde 2019 grundlegend überarbeitet und integriert die frühere VDI 3813.

**Herkunft:** TGA-Planung Deutschland (VDI)  
**Fokus:** Planung & Dokumentation von GA-Systemen  
**Daten:** Funktionsbeschreibungen, Datenpunktlisten

### Struktur der Richtlinienreihe

| Blatt | Inhalt |
|-------|--------|
| 1 | Grundlagen |
| 2.1 | Bedarfsplanung |
| 2.2 | Planungsinhalte, Systemintegration |
| 3.1 | GA-Funktionen (Automationsfunktionen) |
| 3.2 | Makros aus Grundfunktionen |
| 4.1 | Kennzeichnungssysteme |
| 4.2 | Checklisten |
| 4.3 | GA-Automationsschema, Funktionsliste |

### Gliederung der Gebäudeautomation

```
Gebäudeautomation (GA)
├── Raumautomation (RA)
│   ├── Temperaturregelung
│   ├── Beleuchtungssteuerung
│   ├── Sonnenschutzsteuerung
│   └── Präsenzerfassung
├── Anlagenautomation (AA)
│   ├── HLK-Anlagen (Heizung, Lüftung, Klima)
│   ├── Sanitäranlagen
│   └── Elektrotechnik
└── GA-Management
    ├── Überwachung
    ├── Bedienung
    └── Optimierung
```

### GA-Funktionen (Blatt 3.1)

Grundfunktionen der Gebäudeautomation als Funktionsblöcke:

#### Allgemeine Funktionen
- **Schalten** – Ein/Aus-Steuerung
- **Grenzwertüberwachung** – Alarm bei Über-/Unterschreitung
- **Zeitschaltprogramm** – Zeitgesteuerte Aktionen
- **Zähler** – Betriebsstunden, Energie

#### Raumautomation
- **Temperaturregelung** – PI/PID-Regelung für Heizen/Kühlen
- **Beleuchtungssteuerung** – Schalt-/Dimmfunktion
- **Sonnenschutzsteuerung** – Position und Lamellen
- **Präsenzerfassung** – Bewegungsmelder-Logik

#### Anlagenautomation
- **PID-Regler** – Universeller Regler
- **Sequenzsteuerung** – Ablaufsteuerung
- **Pumpensteuerung** – Ein/Aus mit Verriegelung
- **Ventilsteuerung** – Auf/Zu/Modulierend

### Datenpunkttypen

| Typ | Richtung | Signal | Beispiel |
|-----|----------|--------|----------|
| Binäreingang | Input | Binary | Fensterkontakt |
| Binärausgang | Output | Binary | Pumpe Ein/Aus |
| Analogeingang | Input | Analog | Temperatur |
| Analogausgang | Output | Analog | Ventilstellung |
| Zählereingang | Input | Counter | Energiezähler |

**Mapping zu OctoMesh:** 
- In VDI 3814 sind Datenpunkte eigenständige Objekte
- In EnergyIQ/OctoMesh: Datenpunkte = Attribute am Objekt (OO-Ansatz)

### GA-Kennzeichnung (Blatt 4.1)

Schema für Anlagenkennzeichen:
```
+Standort=Gebäude-Geschoss-Raum.Anlage:Bauteil%Signal
```

Beispiel:
```
+Wien=GebA-EG-B001.HZG:VL%Temp
```

### Quellen

- [VDI 3814 Übersicht](https://www.vdi.de/richtlinien/unsere-richtlinien-highlights/vdi-3814)
- [VDI 3814 Blatt 1 – Grundlagen](https://www.vdi.de/richtlinien/details/vdi-3814-blatt-1-gebaeudeautomation-ga-grundlagen)
- [VDI 3814 Blatt 3.1 – GA-Funktionen](https://www.vdi.de/richtlinien/details/vdi-3814-blatt-31-gebaeudeautomation-ga-ga-funktionen-automationsfunktionen)

---

## Project Haystack

### Übersicht

**Project Haystack** ist eine Open-Source-Initiative (seit 2014) zur Standardisierung von semantischem Tagging für IoT- und Gebäudedaten. Es löst das Problem, dass GA-Datenpunkte oft kryptische Namen haben und Maschinen deren Bedeutung nicht verstehen.

**Herkunft:** GA-Betrieb USA (Industrie-Konsortium)  
**Fokus:** Runtime-Daten, Interoperabilität  
**Daten:** Semantische Tags für Datenpunkte

**Gründungsmitglieder:** Siemens, Intel, J2 Innovations, SkyFoundry, Lynxspring, Legrand

### Das Problem

```
BACnet-Datenpunkt:
  Name: "AHU1.SF.SPD"
  Value: 75.0
  
→ Was bedeutet das? Mensch weiß es, Maschine nicht.
```

### Die Lösung: Semantisches Tagging

```
Mit Haystack-Tags:
  Name: "AHU1.SF.SPD"
  Value: 75.0
  Tags: { ahu, supply, fan, speed, sensor, unit:"%" }
          │     │      │    │      │
          │     │      │    │      └── Typ: Messwert
          │     │      │    └── Was: Drehzahl
          │     │      └── Komponente: Ventilator
          │     └── Luftseite: Zuluft
          └── Equipment: Lüftungsgerät
```

### Kernkonzepte

#### 1. Tags (Vokabular)
Standardisierte Begriffe wie `temp`, `humidity`, `ahu`, `vav`, `pump`, `sensor`, `cmd`, `sp` (setpoint).

#### 2. Marker Tags vs. Value Tags
```
Marker:  { hot, water, pump }           ← Nur Präsenz
Value:   { unit: "°C", maxVal: 100 }    ← Mit Wert
```

#### 3. Conjuncts (Zusammengesetzte Tags)
```
chilled-water    ← chilled + water
hot-water-plant  ← hot + water + plant
```

#### 4. Taxonomie (Vererbung)
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

#### 5. Referenzen (Beziehungen)
```
VAV-01:
  tags: { vav, hvac, equip }
  equipRef: @ahu-01           ← Gehört zu AHU-01
  spaceRef: @room-101         ← Versorgt Raum 101
```

### Haystack 5 + Xeto (2024/25)

Die aktuelle Version erweitert Haystack von "flachem Tagging" zu einer vollständigen Ontologie:

| Version | Konzept |
|---------|---------|
| Haystack 1-4 | Flache Tags, lose Konventionen |
| Haystack 5 | Formale Ontologie mit Typ-Hierarchie |
| Xeto | Schema-Sprache für Validierung |

```
Haystack 5 = Semantik ("Was bedeutet es?")
Xeto       = Struktur ("Wie muss es aussehen?")
```

### Haystack vs. OctoMesh CK

| Aspekt | Haystack | OctoMesh CK |
|--------|----------|-------------|
| Modell | Tag-basiert (flach) | Objektorientiert |
| Typisierung | Implizit durch Tags | Explizite Klassen |
| Vererbung | Taxonomie | Echte Klassenhierarchie |
| Beziehungen | Referenz-Tags | Typisierte Associations |
| Validierung | Xeto (neu) | Schema-basiert |
| Zeitreihen | Extern (SkySpark etc.) | Integriert |

**OctoMesh CK ist ausdrucksstärker**, aber Haystack hat breite Industrie-Adoption.

### Integration in EnergyIQ

#### Option 1: Haystack-Tags als Attribut (empfohlen)

```yaml
Space:
  name: "Besprechung 1"
  temperature: 22.3
  haystackTags: ["space", "room", "meetingRoom", "hvacZone"]
  
AirHandlingUnit:
  name: "RLT-01"
  supplyAirTemp: 18.5
  haystackTags: ["ahu", "hvac", "equip"]
  haystackRefs:
    siteRef: "@site-001"
    spaceRef: ["@space-001", "@space-002"]
```

#### Option 2: Automatisches Tag-Mapping

```
EnergyIQ Type    →  Haystack Tags
─────────────────────────────────
Space            →  space, hvacZone
AirHandlingUnit  →  ahu, hvac, equip
Boiler           →  boiler, hvac, equip, hot, water
Temperature      →  temp, sensor, point
```

#### Option 3: Haystack-Export

EnergyIQ-Modell → Haystack JSON/Zinc für externe Tools (SkySpark, FIN Framework).

### Verwandte Standards & Konvergenz

```
┌─────────────────────────────────────────────────────┐
│              ASHRAE 223P (in Entwicklung)           │
│                                                     │
│     Haystack + Brick Schema + BACnet = Unified      │
└─────────────────────────────────────────────────────┘
```

| Standard | Fokus | Status |
|----------|-------|--------|
| **Haystack** | Tagging für GA/IoT | Aktiv, Version 5 |
| **Brick Schema** | Ontologie für Gebäude | Akademisch, UC Berkeley |
| **ASHRAE 223P** | Vereinheitlichung | In Entwicklung |
| **SAREF4BLDG** | EU Smart Appliances | EU-Standard |

### Quellen

- [Project Haystack](https://project-haystack.org/)
- [Haystack Documentation](https://project-haystack.org/doc)
- [Haystack 5 Announcement](https://marketing.project-haystack.org/)
- [Xeto Schema Language](https://project-haystack.org/doc/docHaystack/Xeto)

---

## Vergleich der Standards

| Aspekt | IFC | VDI 3814 | Haystack | EnergyIQ |
|--------|-----|----------|----------|----------|
| **Herkunft** | BIM/Architektur | TGA Deutschland | GA/IoT USA | OctoMesh |
| **Fokus** | Planung & Bau | Planung & Doku | Betrieb & Runtime | Energie & Optimierung |
| **Datenmodell** | OO (EXPRESS) | Funktionsblöcke | Tags (flach→Ontologie) | OO (CK) |
| **Struktur** | Räumlich | Funktional | Semantisch | Kombiniert |
| **Zeitreihen** | Nein | Teilweise | Extern | Integriert |
| **Beziehungen** | Explizit | Implizit | Referenz-Tags | Associations |
| **Format** | STEP/XML | Proprietär | JSON/Zinc | GraphQL/YAML |

---

## Integration in EnergyIQ

### Von IFC übernommen
- Räumliche Hierarchie (Site → Building → Storey → Space)
- Building Elements (Door, Window, etc.)
- Eindeutige GlobalIds
- PropertySet-Konzept → Records

### Von VDI 3814 übernommen
- Gliederung in Raum-/Anlagenautomation
- Funktionale Beschreibung (Soll/Ist/Stell)
- Kennzeichnungsschemata
- Betriebsmodi

### Von Haystack übernommen (optional)
- Semantische Tags für Interoperabilität
- Referenz-Konzept für Equipment-Beziehungen
- Industrie-Vokabular für Analytics-Tools

### EnergyIQ-Erweiterungen
- TimeSeries als First-Class-Citizen
- KI-Optimierungsschicht
- Energieaggregation
- OO-Modellierung (Attribute statt Datenpunkte)
- Validierung über CK-Schema
