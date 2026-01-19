# EnergyIQ - Intelligente Gebäude-Energieoptimierung

**EnergyIQ** ist eine OctoMesh-basierte Lösung zur Energieoptimierung von Gebäuden. Sie kombiniert standardisierte Gebäudedatenmodelle (IFC, VDI 3814) mit KI-gestützter Optimierung.

## Getting Started

### Voraussetzungen

1. **OctoMesh Platform** - Folge der Installationsanleitung:
   [OctoMesh Getting Started Locally](https://docs.meshmakers.cloud/docs/technologyGuide/gettingStartedLocally/prerequisites)

2. **OctoMesh CLI** (`octo-cli`) - Wird mit der Platform installiert

3. **.NET SDK 10.0** - Für das Bauen des Construction Kit

4. **PowerShell** - Für die Ausführung der Setup-Skripte

### Installation

#### 1. Projekt bauen

```bash
cd demo-energy-iq
dotnet build -c Release
```

#### 2. Bei OctoMesh anmelden

```powershell
cd scripts
./om_login_local.ps1
```

Das Skript konfiguriert die lokale OctoMesh-Instanz und öffnet den Browser zur Anmeldung.

#### 3. Tenant erstellen

```powershell
./om_create_tenants.ps1
```

Erstellt den Tenant `energyiqdemo` mit einer eigenen Datenbank.

#### 4. Construction Kit importieren

```powershell
./om_importck.ps1
```

Importiert die folgenden Construction Kits:
- **Basic** - Basis-Typen (NamedEntity, Tree, TreeNode)
- **EnergyIQ** - Domänenmodell (Space, Building, TechnicalSystem, etc.)

#### 5. Runtime-Daten importieren

```powershell
./om_importrt.ps1
```

Importiert:
- **Adapter** - Mesh Adapter Konfiguration
- **Pipelines** - Simulations-Pipeline für Demo-Daten
- **Queries** - Vordefinierte Abfragen
- **BIM-Daten** - Demo-Gebäude "Firmianstraße 31A" mit:
  - 2 Gebäude (Hauptgebäude + Nebengebäude)
  - 3 Stockwerke mit 12 Räumen
  - PV-Anlage (4 Strings, 2 Wechselrichter, Batteriespeicher)
  - HVAC-Systeme (Wärmepumpe, Lüftungsanlage)

### Simulation starten

Nach dem Import läuft automatisch die Simulations-Pipeline, die alle 10 Sekunden realistische Sensordaten generiert:

| Datentyp | Bereich | Beschreibung |
|----------|---------|--------------|
| Temperatur | 18-24°C | Tagesgang (Sinus) |
| Luftfeuchtigkeit | 35-65% | Phasenversetzt |
| CO2-Level | 500-900 ppm | Dreieckskurve |
| Beleuchtung | 100-700 lux | Tageslichtverlauf |
| PV-Leistung | 0-18.4 kW | Sonnenverlauf |
| Batterieladung | 30-90% | Lade-/Entladezyklus |

### Zugriff auf die Daten

Nach der Installation sind die Daten über die OctoMesh GraphQL API verfügbar:
- **GraphQL Playground**: `https://localhost:5001/graphql`
- **Tenant**: `energyiqdemo`

Beispiel-Query für alle Räume mit aktuellen Messwerten:
```graphql
query {
  spaces: rtEntitiesByCkTypeId(ckTypeId: "EnergyIQ/Space") {
    rtId
    attributes {
      Temperature
      Humidity
      CO2Level
    }
  }
}
```

## Vision

Eine Plattform, die:
- Gebäude als **Digital Twin** abbildet (Räume, TGA, Sensoren)
- **Energiedaten** erfasst, aggregiert und analysiert
- **KI-Optimierung** für Heizung, Kühlung, Lüftung, Beleuchtung bietet
- **Standards-konform** ist (ISO 16739-1 IFC, VDI 3814)

## Architektur

```
┌─────────────────────────────────────────────────────────────┐
│                     EnergyIQ Platform                       │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │  Building   │  │   Energy    │  │    Optimization     │  │
│  │  Model      │  │   Data      │  │    Engine           │  │
│  │  (CK)       │  │  (TimeSeries│  │    (AI/ML)          │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│                    OctoMesh Platform                        │
│         Construction Kit │ Data Pipeline │ GraphQL API      │
└─────────────────────────────────────────────────────────────┘
```

## Standards-Referenz

### ISO 16739-1:2024 (IFC 4.3)
Industry Foundation Classes für BIM-Datenaustausch. Liefert die **räumliche Struktur**:
- Site → Building → BuildingStorey → Space
- BuildingElements (Wall, Door, Window, etc.)

### VDI 3814 (Gebäudeautomation)
Deutsche Richtlinie für GA-Systeme. Liefert das **Automations-Modell**:
- Raumautomation (RA) / Anlagenautomation (AA)
- GA-Funktionen (Regler, Scheduler, etc.)
- Datenpunkt-Struktur

## Construction Kit Modell

Siehe [docs/construction-kit.md](docs/construction-kit.md) für die vollständige CK-Spezifikation.

### Kernkonzept: OO statt Datenpunkt-zentriert

Messwerte sind **Attribute am Objekt**, nicht separate Entitäten:

```
Space
├── Temperature: number        ← TimeSeries (Istwert)
├── TemperatureSetpoint: number ← Sollwert
├── HeatingValvePosition: number ← Stellgröße
└── ...
```

Nicht:
```
Space ──► DataPoint("Temperature")  ← Indirektion vermeiden
```

## Projektstruktur

```
demo-energy-iq/
├── docs/
│   ├── developer-guide.md     # Developer Guide (EN)
│   ├── construction-kit.md    # CK Spezifikation
│   └── standards-reference.md # IFC & VDI 3814 Details
├── src/
│   └── EnergyIqCkModel/
│       └── ConstructionKit/   # CK-Definitionen (YAML)
│           ├── ckModel.yaml   # Modell-Metadaten
│           ├── types/         # 18 Entity-Typen
│           ├── attributes/    # 30 Attribut-Definitionen
│           ├── associations/  # 7 Assoziationen
│           ├── records/       # 3 Record-Typen
│           └── enums/         # 6 Enumerationen
├── data/
│   ├── bim/                   # RT-Modell Beispiele
│   │   └── rt-firmianstrasse.yaml
│   ├── _pipelines/            # Simulations-Adapter
│   │   └── rt-simulation-adapters.yaml
│   ├── _general/              # Allgemeine Adapter
│   └── _queries/              # Vordefinierte Abfragen
├── scripts/                   # Setup-Skripte
│   ├── om_login_local.ps1     # OctoMesh Login
│   ├── om_create_tenants.ps1  # Tenant erstellen
│   ├── om_importck.ps1        # CK importieren
│   ├── om_importrt.ps1        # RT-Daten importieren
│   └── om_delete_tenants.ps1  # Tenant löschen
└── README.md
```

## Features

- [x] **Spatial Structure** - Site, Building, BuildingStorey, Space
- [x] **Building Elements** - Wall, Door, Window, ShadingDevice, Luminaire
- [x] **Technical Systems** - Boiler, AirHandlingUnit, Chiller, Pump
- [x] **PV System** - PhotovoltaicSystem, PVString, Inverter, BatteryStorage
- [x] **VDI 3814 Attributes** - Istwerte, Sollwerte, Stellgrößen, Betriebsmodi
- [x] **Simulation Pipeline** - Realistische Sensordaten-Simulation
- [ ] **Energy Aggregation** - Verbrauchsberechnung pro Raum/Gebäude
- [ ] **AI Optimization** - KI-gestützte Optimierung

## Lizenz

Siehe [LICENSE](LICENSE)
