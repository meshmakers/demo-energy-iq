# EnergyIQ Firmianstraße – Lösungsüberblick & Datenzugriff

**Zielpublikum:** FH Salzburg – Projektpartner, die Mess- und Zustandsdaten aus dem
System abrufen möchten.
**Stand:** Juli 2026 · Tenant `energyiq` auf der OctoMesh-Umgebung **prod-1**.

---

## 1. Was ist das System?

Ein Wohngebäude in Salzburg (Firmianstraße 31A, Haupt- + Nebengebäude) ist vollständig
mit einem **Loxone-Miniserver** automatisiert (Raumklima, Heizung/Kühlung über eine
reversible Sole-Wasser-Wärmepumpe, kontrollierte Wohnraumlüftung, PV-Anlage mit
Batteriespeicher, Wallboxen, Beleuchtung, Beschattung).

Die Loxone-Daten werden live in die IoT-Datenplattform **OctoMesh**
([meshmakers.io](https://www.meshmakers.io)) gespiegelt:

```
Loxone Miniserver ──WebSocket──▶ Edge-Adapter (vor Ort) ──MQ──▶ OctoMesh prod-1
                                                                  │
                                          EnergyIQ-Datenmodell (IFC-basiert)
                                          ├── Runtime-Entities   = aktuelle Werte
                                          └── Stream-Data-Archive = Zeitreihen (CrateDB)
```

Zwei Dinge machen die Daten für Forschung/Analytik brauchbar:

1. **Semantisches Modell statt roher Datenpunkte.** Die Loxone-Rohdaten werden auf das
   **EnergyIQ-Construction-Kit** gemappt – ein Domänenmodell nach ISO 16739 (IFC 4.3)
   und VDI 3814. Ein Messwert hängt also nicht an `18a29f4b-0234-…`, sondern an
   `TemperatureSensor "Temp-Buero-DG1"` im Space `Büro DG 1` im Geschoss `DG` des
   Gebäudes. Räume, Geschosse, Anlagen, Verteilsysteme und ihre Beziehungen sind
   navigierbar (Details: [`developer-guide.md`](developer-guide.md),
   [`construction-kit.md`](construction-kit.md)).
2. **Getrennte Lese-Pfade für "jetzt" und "Verlauf".** Aktuelle Werte stehen als
   Attribute auf den Entities (Millisekunden-Zugriff), Zeitreihen liegen in
   dedizierten Archiven (CrateDB) mit optionalen 5-Minuten-Rollups.

Die Daten sind **ereignisbasiert**: Der Loxone-Miniserver sendet bei Wertänderung
(plus ein vollständiger Snapshot bei jedem Verbindungsaufbau des Adapters). Häufig
tickende Größen (Leistungen, Temperaturen) liefern quasi-kontinuierliche Reihen;
seltene Ereignisse (Lichtschalter) erzeugen dünn besetzte Reihen.
Erfassungsbeginn auf prod-1: **Juli 2026**.

## 2. Welche Daten gibt es?

### Aktuelle Werte (Runtime-Entities, Auszug)

| Entity-Typ | Instanzen | Wichtigste Attribute |
|---|---|---|
| `EnergyIQ/TemperatureSensor` | 21 (Räume, außen, WP-Kreis) | `CurrentValue` (°C) |
| `EnergyIQ/HumiditySensor` / `CO2Sensor` / `PresenceSensor` | je Raum, wo vorhanden | `CurrentValue` |
| `EnergyIQ/Luminaire` | 18 (je Raum) | `IsOn`, `DimmingLevel` (0/100) |
| `EnergyIQ/ShadingDevice` | 8 (Raffstores) | `Position` (%), `SlatAngle` |
| `EnergyIQ/Meter` | Etagen- + Geräte-Unterzähler (EG/OG/DG, IT-Schrank, Technikraum, Wärmepumpe) | `ActivePower` (kW), `ImportedEnergy` (kWh) |
| `EnergyIQ/GridConnection` | Hausanschluss | `ActivePower` (kW, signiert: <0 = Einspeisung) |
| `EnergyIQ/PhotovoltaicSystem` | 1 (18,4 kWp) | `TotalCurrentPower`, `GridFeedIn`, `SelfConsumption` |
| `EnergyIQ/BatteryStorage` | 1 (15 kWh) | `StateOfCharge` (%), `ChargingPower`, `IsCharging`/`IsDischarging` |
| `EnergyIQ/ChargingStation` | 2 Wallboxen | `ActivePower`, `ImportedEnergy` |
| `EnergyIQ/HeatPump` | 1 (reversibel) | `SupplyTemp`, `ReturnTemp`, `SourceInlet/OutletTemp` (Sole), `HotGasTemp`, `PowerConsumption`, … |
| `EnergyIQ/AirHandlingUnit` | 1 (KWL) | `SupplyAirTemp`, `ReturnAirTemp`, `ExhaustAirTemp`, `OutdoorAirTemp` |
| `EnergyIQ/ThermalEnergyStorage` | Puffer + WW-Speicher | `StorageTempTop` |
| `EnergyIQ/Pump` | Heizkreispumpe | `Speed`, `FlowRate` |

Dazu die Struktur: `Site → Building → BuildingStorey → Space` (18 Innenräume,
5 Außenbereiche), `DistributionSystem` (Heizkreis, Kühlkreis, Lüftung, Elektrisch)
mit Mitgliedschafts- und Versorgungs-Beziehungen.

### Zeitreihen (Stream-Data-Archive)

Jedes Archiv gehört zu einem Entity-Typ; jede Zeile trägt `rtid` (Entity), `timestamp`
und die Wertspalten. Zu den meisten Archiven existiert ein **5-Minuten-Rollup**
(AVG/MIN/MAX bzw. AVG/MAX), der für Chart-Abfragen über längere Zeiträume die
passende Auflösung liefert.

| Archiv (rtId) | Ziel-Typ | Spalten |
|---|---|---|
| `6a0e000000000000000a0001` TemperatureSensorArchive | TemperatureSensor | CurrentValue |
| `6a0e000000000000000a0002` HumiditySensorArchive | HumiditySensor | CurrentValue |
| `6a0e000000000000000a0003` CO2SensorArchive | CO2Sensor | CurrentValue |
| `6a0e000000000000000a0004` LuminaireArchive | Luminaire | IsOn, DimmingLevel |
| `6a0e000000000000000a0005` ShadingDeviceArchive | ShadingDevice | Position, SlatAngle |
| `6a0e000000000000000a0006` MeterArchive | Meter | ActivePower |
| `6a0e000000000000000a0007` GridConnectionArchive | GridConnection | ActivePower |
| `6a0e000000000000000a0008` ChargingStationArchive | ChargingStation | ActivePower |
| `6a0e000000000000000a0009` ApplianceArchive | Appliance | ActivePower |
| `6a0e000000000000000a000a` PVStringArchive | PVString | CurrentPower |
| `6a0e000000000000000a000b` InverterArchive | Inverter | DcPower, AcPower |
| `6a0e000000000000000a000c` PhotovoltaicSystemArchive | PhotovoltaicSystem | TotalCurrentPower, GridFeedIn, SelfConsumption |
| `6a0e000000000000000a000d` BatteryStorageArchive | BatteryStorage | StateOfCharge, ChargingPower |
| `6a0e000000000000000a000e` HeatPumpArchive | HeatPump | SupplyTemp, ReturnTemp, CoolingSupplyTemp, SourceInletTemp, SourceOutletTemp, HotGasTemp, PowerConsumption |
| `6a0e000000000000000a000f` ThermalEnergyStorageArchive | ThermalEnergyStorage | StorageTempTop |
| `6a0e000000000000000a0010` PumpArchive | Pump | Speed, FlowRate |
| `6a0e000000000000000a0011` MeterEnergyArchive | Meter | ImportedEnergy (Zählerstand) |
| `6a0e000000000000000a0012` LuminaireStatusArchive | Luminaire | Name, IsOn, DimmingLevel |
| `6a0e000000000000000a0013` ShadingDeviceStatusArchive | ShadingDevice | Name, Position |

Einheiten: Leistungen kW, Energie kWh (Zählerstände monoton steigend), Temperaturen °C,
Positionen/SoC/DimmingLevel %.

## 3. Zugang & Authentifizierung

| Dienst | URL |
|---|---|
| Identity (OAuth2/OIDC) | `https://connect.prod-1.octo-mesh.com/` |
| Asset-Repository (GraphQL – **der Daten-Endpunkt**) | `https://assets.prod-1.octo-mesh.com/tenants/energyiq/graphql` |
| Studio (Web-UI, MeshBoards) | `https://studio.prod-1.octo-mesh.com/` |
| MCP-Server (KI-Agenten-Zugriff) | `https://mcp.prod-1.octo-mesh.com/` |

Alle APIs verlangen ein **OAuth2-Bearer-Token** vom Identity-Dienst. Für
Maschinenzugriff bekommt ihr von uns einen **Service-Account (Client-Credentials)**
mit Leserechten auf den Tenant `energyiq`; für interaktive Nutzung (Studio, octo-cli)
persönliche Logins. → Zugangsdaten bitte bei Meshmakers (gerald.lochner@meshmakers.io)
anfragen.

Token holen (Client-Credentials):

```bash
curl -s https://connect.prod-1.octo-mesh.com/connect/token \
  -d grant_type=client_credentials \
  -d client_id=<eure-client-id> \
  -d client_secret=<euer-secret> \
  -d scope=assetService
# → {"access_token":"eyJ...", "expires_in":3600, ...}
```

## 4. Der empfohlene Weg: GraphQL

Ein Endpunkt für alles – aktuelle Werte, Struktur-Navigation und Zeitreihen.
Interaktiv erkundbar per Schema-Introspection (z. B. mit Banana Cake Pop, Insomnia,
Postman oder `curl`).

```
POST https://assets.prod-1.octo-mesh.com/tenants/energyiq/graphql
Authorization: Bearer <token>
Content-Type: application/json
```

### 4.1 Aktuelle Werte

Alle Raumtemperaturen mit Zeitstempel der letzten Änderung:

```graphql
{
  runtime {
    energyIQTemperatureSensor(first: 50) {
      items { rtId name currentValue rtChangedDateTime }
    }
  }
}
```

Analog für jeden Typ: `energyIQLuminaire`, `energyIQShadingDevice`, `energyIQMeter`,
`energyIQBatteryStorage`, `energyIQHeatPump`, … (Feldnamen = camelCase des Typnamens).
Assoziationen sind mitnavigierbar, z. B. Sensoren eines Raums über die
`SpaceSensors`-Beziehung.

### 4.2 Zeitreihen – gespeicherte Abfragen

Im Tenant sind **persistierte Queries** hinterlegt (auch die MeshBoards nutzen sie).
Ausführen per `streamData.streamDataQuery(rtId: …)`:

```graphql
{
  streamData {
    streamDataQuery(rtId: "6a4a9ca67e487162fbe38608", first: 10) {
      items {
        columns { attributePath }
        rows(first: 1000) {
          items { rtId timestamp cells(first: 10) { items { attributePath value } } }
        }
      }
    }
  }
}
```

Nützliche vorhandene Query-Typen:

- `System/SimpleSdQuery` – Rohzeitreihe eines Archivs (Spaltenauswahl, Sortierung).
- `System/AggregationSdQuery` – ein Aggregat (SUM/AVG/MIN/MAX/COUNT) über das Zeitfenster.
- `System/GroupingAggregationSdQuery` – Aggregat **gruppiert nach Archivspalte**, z. B.
  Ø-Einschaltquote je Leuchte (`GROUP BY Name` auf dem LuminaireStatusArchive).

### 4.3 Zeitreihen – ad hoc (transient)

`streamData.transientStreamDataQuery` nimmt dieselben Parameter (Archiv, Spalten,
Zeitfenster, rtIds, Aggregation) direkt im Request entgegen – kein vorab angelegtes
Query-Objekt nötig. Für Chart-artige Abfragen über lange Zeiträume wählt
`streamData.resolveSeriesQuery` automatisch die passende Auflösung
(Roh-Archiv vs. 5-Minuten-Rollup) zu einer gewünschten Punktezahl.

## 5. Alternative Zugriffswege

| Weg | Wofür |
|---|---|
| **Studio / MeshBoards** | Visuelle Exploration ohne Code: Boards *Raumtemperaturen*, *Energie*, *Energiebilanz*, *Heizung & Kühlung*, *Beschattung & Beleuchtung* (Zeitfilter, Raum-Auswahl, CSV-Export aus Tabellen). |
| **octo-cli** | Skriptbarer Export: `ExportRtByQuery` (Entities als ZIP), Kontext-/Login-Handling. Gut für Batch-Abzüge. |
| **Power BI** | Meshmakers stellt einen Power-Query-Connector bereit (Asset-Repo-URL + Login genügen). |
| **MCP-Server** | Für LLM-/Agenten-Integration: ~180 Tools (Entity-Queries, Assoziations-Bäume, Stream-Data-Aggregationen) über das Model Context Protocol – damit kann z. B. ein Claude-/GPT-Agent direkt gegen den Tenant arbeiten. |

## 6. Hinweise & Einschränkungen

- **Ereignisbasierte Reihen:** Zwischen zwei Zeilen gilt der letzte Wert fort
  (last observation carried forward ist Sache der Auswertung). Stichproben-Mittelwerte
  über dünn besetzte Signale (z. B. Licht an/aus) sind *nicht* zeitgewichtet – eine
  zeitgewichtete Dauer-Aggregation ist als Erweiterung geplant (AB#4336).
- **Vorzeichen-Konventionen:** `GridConnection.ActivePower` < 0 = Einspeisung;
  Batterie: `ChargingPower`/`DischargingPower` getrennt ausgewiesen, dazu die
  Flags `IsCharging`/`IsDischarging`.
- **Historie** beginnt mit der prod-1-Inbetriebnahme im Juli 2026 und wächst seither
  kontinuierlich.
- Einige Sensoren sind bewusst ohne Datenquelle (Technikraum/Werkstatt/Waschküche
  haben keinen Loxone-Raumregler; die WP-Vor-/Rücklauftemperaturen liegen als
  `HeatPump`-Attribute vor, nicht auf den gleichnamigen Sensoren).

## 7. Kontakt

Meshmakers GmbH · Gerald Lochner · gerald.lochner@meshmakers.io
(Zugänge, zusätzliche Archive/Spalten, Fragen zum Datenmodell.)
