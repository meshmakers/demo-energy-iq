# Haystack Adapter Konzept

## 1. Überblick

### Ziel
Ein Adapter, der EnergyIQ/OctoMesh-Daten über die standardisierte **Project Haystack REST API** bereitstellt. Dadurch können Haystack-kompatible Tools wie SkySpark, FIN Framework, Widesky oder andere Clients direkt auf die Gebäudedaten zugreifen.

### Referenzen
- [Project Haystack REST API Spec](https://project-haystack.org/doc/docHaystack/HttpApi)
- [Haystack Filter Syntax](https://project-haystack.org/doc/docHaystack/Filters)
- [Haystack JSON Encoding](https://project-haystack.org/doc/docHaystack/Json)

---

## 2. Haystack API Operationen

### 2.1 Basis-Operationen (Phase 1)

| Operation | HTTP | Beschreibung | Implementierung |
|-----------|------|--------------|-----------------|
| `about` | GET | Server-Info, Version, Vendor | Statisch + OctoMesh Tenant Info |
| `ops` | GET | Liste verfügbarer Operationen | Statische Liste |
| `formats` | GET | Unterstützte Formate | `["application/json", "text/zinc"]` |
| `read` | POST | Entities lesen mit Filter | GraphQL Query → Haystack Grid |
| `nav` | POST | Hierarchie-Navigation | ParentChild Traversal |

### 2.2 TimeSeries-Operationen (Phase 2)

| Operation | HTTP | Beschreibung | Implementierung |
|-----------|------|--------------|-----------------|
| `hisRead` | POST | Historische Daten lesen | OctoMesh TimeSeries API |
| `hisWrite` | POST | Historische Daten schreiben | OctoMesh TimeSeries API |

### 2.3 Echtzeit-Operationen (Phase 3)

| Operation | HTTP | Beschreibung | Implementierung |
|-----------|------|--------------|-----------------|
| `pointWrite` | POST | Schreibbare Punkte setzen | OctoMesh Mutation |
| `watchSub` | POST | Watch subscription starten | WebSocket/SignalR |
| `watchUnsub` | POST | Watch beenden | - |
| `watchPoll` | POST | Watch Änderungen abfragen | - |

---

## 3. Datenformat-Mapping

### 3.1 Haystack Grid Format (JSON)

```json
{
  "meta": {"ver": "3.0"},
  "cols": [
    {"name": "id"},
    {"name": "dis"},
    {"name": "site"},
    {"name": "space"},
    {"name": "temp"},
    {"name": "unit"}
  ],
  "rows": [
    {
      "id": {"_kind": "ref", "val": "6789a00000000000000011d1", "dis": "Wohnbereich"},
      "dis": {"_kind": "str", "val": "Wohnbereich"},
      "site": {"_kind": "marker"},
      "space": {"_kind": "marker"},
      "temp": {"_kind": "number", "val": 21.5, "unit": "°C"},
      "unit": {"_kind": "str", "val": "°C"}
    }
  ]
}
```

### 3.2 EnergyIQ → Haystack Type Mapping

| EnergyIQ Type | Haystack Tags | Haystack Markers |
|---------------|---------------|------------------|
| Site | `site` | `geoAddr`, `geoCity`, `geoCountry` |
| Building | `site`, `building` | `geoAddr` |
| Space | `space`, `room` | `hvacZone`, `area` |
| AirHandlingUnit | `ahu`, `hvac`, `equip` | `airHandling` |
| Boiler | `boiler`, `hvac`, `equip` | `hot`, `water`, `heating` |
| Chiller | `chiller`, `hvac`, `equip` | `cool`, `water` |
| Pump | `pump`, `equip` | `water`, `motor` |
| PhotovoltaicSystem | `solar`, `pv`, `elec`, `equip` | `meter` |
| PVString | `solar`, `pv`, `equip` | `dc` |
| Inverter | `inverter`, `equip` | `dc`, `ac`, `elec` |
| BatteryStorage | `battery`, `storage`, `equip` | `elec` |

### 3.3 Attribut → Haystack Point Mapping

| EnergyIQ Attribut | Haystack Point Tags |
|-------------------|---------------------|
| `temperature` | `temp`, `sensor`, `air`, `zone` |
| `humidity` | `humidity`, `sensor`, `air`, `zone` |
| `co2Level` | `co2`, `sensor`, `air`, `zone` |
| `temperatureSetpointHeating` | `temp`, `sp`, `heating`, `zone` |
| `temperatureSetpointCooling` | `temp`, `sp`, `cooling`, `zone` |
| `heatingValvePosition` | `valve`, `cmd`, `heating`, `hot`, `water` |
| `coolingValvePosition` | `valve`, `cmd`, `cooling`, `chilled`, `water` |
| `currentPower` (PV) | `power`, `sensor`, `dc`, `solar` |
| `stateOfCharge` | `soc`, `sensor`, `battery` |

---

## 4. Architektur

### 4.1 Komponenten-Diagramm

```
┌─────────────────────────────────────────────────────────────────┐
│                     Haystack Clients                            │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────────┐ │
│  │ SkySpark │  │   FIN    │  │ Widesky  │  │ Custom Haystack  │ │
│  │ Framework│  │Framework │  │  Client  │  │     Client       │ │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────────┬─────────┘ │
└───────┼─────────────┼─────────────┼─────────────────┼───────────┘
        │             │             │                 │
        └─────────────┴──────┬──────┴─────────────────┘
                             │ HTTPS (Haystack REST API)
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Haystack Adapter Service                     │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │                    ASP.NET Core Web API                    │ │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐  │ │
│  │  │   Haystack   │  │   Haystack   │  │     Haystack     │  │ │
│  │  │  Controller  │  │   Filter     │  │   Grid Builder   │  │ │
│  │  │  (REST API)  │  │   Parser     │  │   (JSON/Zinc)    │  │ │
│  │  └──────┬───────┘  └──────┬───────┘  └────────┬─────────┘  │ │
│  │         │                 │                   │            │ │
│  │         └─────────────────┼───────────────────┘            │ │
│  │                           │                                │ │
│  │  ┌────────────────────────▼────────────────────────────┐   │ │
│  │  │              EnergyIQ Mapping Service               │   │ │
│  │  │  - Type → Tags Mapping                              │   │ │
│  │  │  - Attribute → Point Mapping                        │   │ │
│  │  │  - Ref Resolution (ParentChild → siteRef, equipRef) │   │ │
│  │  └────────────────────────┬────────────────────────────┘   │ │
│  └───────────────────────────┼────────────────────────────────┘ │
│                              │                                  │
└──────────────────────────────┼──────────────────────────────────┘
                               │ GraphQL / gRPC
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                        OctoMesh Server                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐   │
│  │   GraphQL    │  │  TimeSeries  │  │   Runtime Model      │   │
│  │     API      │  │     API      │  │   (EnergyIQ CK)      │   │
│  └──────────────┘  └──────────────┘  └──────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 Deployment-Optionen

**Option A: Standalone Service**
```
docker-compose:
  - octomesh-server
  - haystack-adapter (Port 8080)
  - nginx (Reverse Proxy)
```

**Option B: OctoMesh Plugin/Extension**
- Direkt in OctoMesh integriert
- Zusätzlicher `/haystack/*` Endpoint

---

## 5. API Implementierung

### 5.1 Endpoint-Struktur

```
/haystack/about      GET   → Server-Info
/haystack/ops        GET   → Verfügbare Operationen
/haystack/formats    GET   → Unterstützte Formate
/haystack/read       POST  → Entities lesen
/haystack/nav        POST  → Navigation
/haystack/hisRead    POST  → TimeSeries lesen
/haystack/hisWrite   POST  → TimeSeries schreiben
/haystack/pointWrite POST  → Punkte schreiben
```

### 5.2 Beispiel: `/haystack/about`

**Request:**
```http
GET /haystack/about HTTP/1.1
Accept: application/json
```

**Response:**
```json
{
  "meta": {"ver": "3.0"},
  "cols": [{"name": "name"}, {"name": "val"}],
  "rows": [
    {"name": "haystackVersion", "val": "3.0"},
    {"name": "serverName", "val": "EnergyIQ Haystack Adapter"},
    {"name": "serverVersion", "val": "1.0.0"},
    {"name": "vendorName", "val": "Meshmakers"},
    {"name": "vendorUri", "val": {"_kind": "uri", "val": "https://meshmakers.cloud"}},
    {"name": "productName", "val": "EnergyIQ"},
    {"name": "productUri", "val": {"_kind": "uri", "val": "https://meshmakers.cloud/energyiq"}},
    {"name": "tz", "val": "Europe/Vienna"}
  ]
}
```

### 5.3 Beispiel: `/haystack/read` mit Filter

**Request:**
```http
POST /haystack/read HTTP/1.1
Content-Type: text/zinc
Accept: application/json

ver:"3.0"
filter:"space and hvacZone"
```

**GraphQL Query (intern generiert):**
```graphql
query {
  spaces(filter: { haystackTags: { contains: ["space", "hvacZone"] } }) {
    rtId
    name
    description
    temperature
    humidity
    co2Level
    haystackTags
    haystackRefs {
      refName
      targetId
    }
  }
}
```

**Response:**
```json
{
  "meta": {"ver": "3.0"},
  "cols": [
    {"name": "id"},
    {"name": "dis"},
    {"name": "space"},
    {"name": "hvacZone"},
    {"name": "temp"},
    {"name": "humidity"},
    {"name": "co2"},
    {"name": "siteRef"},
    {"name": "equipRef"}
  ],
  "rows": [
    {
      "id": {"_kind": "ref", "val": "6789a00000000000000011d1", "dis": "Wohnbereich"},
      "dis": "Wohnbereich",
      "space": {"_kind": "marker"},
      "hvacZone": {"_kind": "marker"},
      "temp": {"_kind": "number", "val": 21.5, "unit": "°C"},
      "humidity": {"_kind": "number", "val": 48.0, "unit": "%"},
      "co2": {"_kind": "number", "val": 650, "unit": "ppm"},
      "siteRef": {"_kind": "ref", "val": "6789a00000000000000000a1", "dis": "Firmianstraße 31A"},
      "equipRef": {"_kind": "ref", "val": "6789a00000000000000061f2", "dis": "KWL"}
    }
  ]
}
```

### 5.4 Beispiel: `/haystack/hisRead`

**Request:**
```http
POST /haystack/hisRead HTTP/1.1
Content-Type: application/json
Accept: application/json

{
  "meta": {"ver": "3.0"},
  "cols": [{"name": "id"}, {"name": "range"}],
  "rows": [
    {
      "id": {"_kind": "ref", "val": "6789a00000000000000011d1"},
      "range": "2024-01-01,2024-01-31"
    }
  ]
}
```

**Response:**
```json
{
  "meta": {
    "ver": "3.0",
    "id": {"_kind": "ref", "val": "6789a00000000000000011d1"},
    "hisStart": {"_kind": "dateTime", "val": "2024-01-01T00:00:00Z"},
    "hisEnd": {"_kind": "dateTime", "val": "2024-01-31T23:59:59Z"}
  },
  "cols": [{"name": "ts"}, {"name": "val"}],
  "rows": [
    {"ts": {"_kind": "dateTime", "val": "2024-01-01T00:00:00Z"}, "val": {"_kind": "number", "val": 21.2, "unit": "°C"}},
    {"ts": {"_kind": "dateTime", "val": "2024-01-01T00:15:00Z"}, "val": {"_kind": "number", "val": 21.3, "unit": "°C"}},
    {"ts": {"_kind": "dateTime", "val": "2024-01-01T00:30:00Z"}, "val": {"_kind": "number", "val": 21.4, "unit": "°C"}}
  ]
}
```

---

## 6. Filter-Syntax Mapping

### 6.1 Haystack Filter → GraphQL

| Haystack Filter | GraphQL Filter |
|-----------------|----------------|
| `site` | `{ ckTypeId: { eq: "EnergyIQ/Site" } }` |
| `space and hvacZone` | `{ haystackTags: { containsAll: ["space", "hvacZone"] } }` |
| `equip and solar` | `{ haystackTags: { containsAll: ["equip", "solar"] } }` |
| `temp and sensor` | Attribute-basierte Filterung |
| `siteRef == @abc` | `{ associations: { targetRtId: "abc" } }` |

### 6.2 Filter Parser

```csharp
public class HaystackFilterParser
{
    public GraphQLFilter Parse(string haystackFilter)
    {
        // Tokenize: "space and hvacZone and temp > 20"
        // → tokens: ["space", "and", "hvacZone", "and", "temp", ">", "20"]

        // Build AST
        // → AndExpr(MarkerExpr("space"), AndExpr(MarkerExpr("hvacZone"), CompareExpr("temp", ">", 20)))

        // Generate GraphQL filter
        // → { haystackTags: { containsAll: ["space", "hvacZone"] }, temperature: { gt: 20 } }
    }
}
```

---

## 7. Authentifizierung

### 7.1 Haystack SCRAM Authentication

Haystack definiert SCRAM-basierte Authentifizierung:

```
1. Client → Server: GET /haystack/about (mit Authorization: HELLO)
2. Server → Client: 401 + WWW-Authenticate: SCRAM hash=SHA-256, handshakeToken=xxx
3. Client → Server: GET /haystack/about (mit Authorization: SCRAM data=base64...)
4. Server → Client: Authentication-Info: hash=xxx, authToken=yyy
5. Client → Server: Alle weiteren Requests mit Authorization: BEARER authToken
```

### 7.2 Alternative: OAuth2/OpenID Connect

Für OctoMesh-Integration könnte auch OAuth2 verwendet werden:

```
Authorization: Bearer <octomesh-access-token>
```

---

## 8. Implementierungsplan

### Phase 1: Basis (MVP)
- [ ] ASP.NET Core Projekt Setup
- [ ] `about`, `ops`, `formats` Endpoints
- [ ] `read` Endpoint mit einfachen Filtern
- [ ] JSON Grid Builder
- [ ] EnergyIQ Type → Haystack Tag Mapping

### Phase 2: Navigation & History
- [ ] `nav` Endpoint (ParentChild Traversal)
- [ ] `hisRead` Endpoint (OctoMesh TimeSeries)
- [ ] Haystack Filter Parser (vollständig)
- [ ] Zinc Format Support

### Phase 3: Schreiben & Echtzeit
- [ ] `hisWrite` Endpoint
- [ ] `pointWrite` Endpoint
- [ ] `watchSub/watchUnsub/watchPoll` (WebSocket)
- [ ] SCRAM Authentication

### Phase 4: Produktion
- [ ] Performance-Optimierung (Caching, Batching)
- [ ] Monitoring & Logging
- [ ] Docker Image
- [ ] Dokumentation

---

## 9. Projektstruktur

```
src/
├── Meshmakers.EnergyIQ.HaystackAdapter/
│   ├── Controllers/
│   │   ├── AboutController.cs
│   │   ├── OpsController.cs
│   │   ├── ReadController.cs
│   │   ├── NavController.cs
│   │   └── HisController.cs
│   ├── Services/
│   │   ├── IHaystackGridBuilder.cs
│   │   ├── HaystackJsonGridBuilder.cs
│   │   ├── HaystackZincGridBuilder.cs
│   │   ├── IFilterParser.cs
│   │   ├── HaystackFilterParser.cs
│   │   ├── IEnergyIQMappingService.cs
│   │   └── EnergyIQMappingService.cs
│   ├── Models/
│   │   ├── HaystackGrid.cs
│   │   ├── HaystackColumn.cs
│   │   ├── HaystackRow.cs
│   │   └── HaystackValue.cs
│   ├── OctoMesh/
│   │   ├── IOctoMeshClient.cs
│   │   ├── OctoMeshGraphQLClient.cs
│   │   └── OctoMeshTimeSeriesClient.cs
│   └── Program.cs
├── Meshmakers.EnergyIQ.HaystackAdapter.Tests/
│   ├── FilterParserTests.cs
│   ├── GridBuilderTests.cs
│   └── MappingServiceTests.cs
└── docker/
    ├── Dockerfile
    └── docker-compose.yml
```

---

## 10. Beispiel-Konfiguration

### appsettings.json

```json
{
  "OctoMesh": {
    "GraphQLEndpoint": "https://octomesh.local/graphql",
    "TimeSeriesEndpoint": "https://octomesh.local/timeseries",
    "TenantId": "energyiq-demo",
    "ClientId": "haystack-adapter",
    "ClientSecret": "***"
  },
  "Haystack": {
    "ServerName": "EnergyIQ Haystack Adapter",
    "ServerVersion": "1.0.0",
    "VendorName": "Meshmakers",
    "Timezone": "Europe/Vienna",
    "DefaultLimit": 1000,
    "MaxLimit": 10000
  },
  "Authentication": {
    "Mode": "OAuth2",
    "Authority": "https://auth.octomesh.local",
    "Audience": "haystack-api"
  }
}
```

---

## 11. Zusammenfassung

Der Haystack Adapter ermöglicht:

1. **Standardisierter Zugriff** auf EnergyIQ-Daten über das Haystack-Protokoll
2. **Tool-Integration** mit SkySpark, FIN Framework, Widesky, etc.
3. **Bidirektionaler Datenaustausch** (lesen + schreiben)
4. **Real-time Updates** über Watch-Mechanismus
5. **TimeSeries-Zugriff** für historische Analysen

Der Adapter fungiert als **Brücke** zwischen dem objektorientierten EnergyIQ-Modell und der Tag-basierten Haystack-Welt.
