# Haystack Adapter Concept

## 1. Overview

### Goal
An adapter that exposes EnergyIQ/OctoMesh data via the standardized **Project Haystack REST API**. This enables Haystack-compatible tools such as SkySpark, FIN Framework, Widesky, or other clients to directly access the building data.

### References
- [Project Haystack REST API Spec](https://project-haystack.org/doc/docHaystack/HttpApi)
- [Haystack Filter Syntax](https://project-haystack.org/doc/docHaystack/Filters)
- [Haystack JSON Encoding](https://project-haystack.org/doc/docHaystack/Json)

---

## 2. Haystack API Operations

### 2.1 Basic Operations (Phase 1)

| Operation | HTTP | Description | Implementation |
|-----------|------|-------------|----------------|
| `about` | GET | Server info, version, vendor | Static + OctoMesh tenant info |
| `ops` | GET | List of available operations | Static list |
| `formats` | GET | Supported formats | `["application/json", "text/zinc"]` |
| `read` | POST | Read entities with filter | GraphQL query → Haystack Grid |
| `nav` | POST | Hierarchy navigation | ParentChild traversal |

### 2.2 TimeSeries Operations (Phase 2)

| Operation | HTTP | Description | Implementation |
|-----------|------|-------------|----------------|
| `hisRead` | POST | Read historical data | OctoMesh TimeSeries API |
| `hisWrite` | POST | Write historical data | OctoMesh TimeSeries API |

### 2.3 Real-Time Operations (Phase 3)

| Operation | HTTP | Description | Implementation |
|-----------|------|-------------|----------------|
| `pointWrite` | POST | Set writable points | OctoMesh mutation |
| `watchSub` | POST | Start watch subscription | WebSocket/SignalR |
| `watchUnsub` | POST | End watch | - |
| `watchPoll` | POST | Poll watch changes | - |

---

## 3. Data Format Mapping

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
      "id": {"_kind": "ref", "val": "6789a00000000000000011d1", "dis": "Living Area"},
      "dis": {"_kind": "str", "val": "Living Area"},
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

### 3.3 Attribute → Haystack Point Mapping

| EnergyIQ Attribute | Haystack Point Tags |
|--------------------|---------------------|
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

## 4. Architecture

### 4.1 Component Diagram

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

### 4.2 Deployment Options

**Option A: Standalone Service**
```
docker-compose:
  - octomesh-server
  - haystack-adapter (Port 8080)
  - nginx (Reverse Proxy)
```

**Option B: OctoMesh Plugin/Extension**
- Directly integrated into OctoMesh
- Additional `/haystack/*` endpoint

---

## 5. API Implementation

### 5.1 Endpoint Structure

```
/haystack/about      GET   → Server info
/haystack/ops        GET   → Available operations
/haystack/formats    GET   → Supported formats
/haystack/read       POST  → Read entities
/haystack/nav        POST  → Navigation
/haystack/hisRead    POST  → Read TimeSeries
/haystack/hisWrite   POST  → Write TimeSeries
/haystack/pointWrite POST  → Write points
```

### 5.2 Example: `/haystack/about`

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

### 5.3 Example: `/haystack/read` with Filter

**Request:**
```http
POST /haystack/read HTTP/1.1
Content-Type: text/zinc
Accept: application/json

ver:"3.0"
filter:"space and hvacZone"
```

**GraphQL Query (internally generated):**
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
      "id": {"_kind": "ref", "val": "6789a00000000000000011d1", "dis": "Living Area"},
      "dis": "Living Area",
      "space": {"_kind": "marker"},
      "hvacZone": {"_kind": "marker"},
      "temp": {"_kind": "number", "val": 21.5, "unit": "°C"},
      "humidity": {"_kind": "number", "val": 48.0, "unit": "%"},
      "co2": {"_kind": "number", "val": 650, "unit": "ppm"},
      "siteRef": {"_kind": "ref", "val": "6789a00000000000000000a1", "dis": "Firmianstraße 31A"},
      "equipRef": {"_kind": "ref", "val": "6789a00000000000000061f2", "dis": "HRV"}
    }
  ]
}
```

### 5.4 Example: `/haystack/hisRead`

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

## 6. Filter Syntax Mapping

### 6.1 Haystack Filter → GraphQL

| Haystack Filter | GraphQL Filter |
|-----------------|----------------|
| `site` | `{ ckTypeId: { eq: "EnergyIQ/Site" } }` |
| `space and hvacZone` | `{ haystackTags: { containsAll: ["space", "hvacZone"] } }` |
| `equip and solar` | `{ haystackTags: { containsAll: ["equip", "solar"] } }` |
| `temp and sensor` | Attribute-based filtering |
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

## 7. Authentication

### 7.1 Haystack SCRAM Authentication

Haystack defines SCRAM-based authentication:

```
1. Client → Server: GET /haystack/about (with Authorization: HELLO)
2. Server → Client: 401 + WWW-Authenticate: SCRAM hash=SHA-256, handshakeToken=xxx
3. Client → Server: GET /haystack/about (with Authorization: SCRAM data=base64...)
4. Server → Client: Authentication-Info: hash=xxx, authToken=yyy
5. Client → Server: All subsequent requests with Authorization: BEARER authToken
```

### 7.2 Alternative: OAuth2/OpenID Connect

For OctoMesh integration, OAuth2 could also be used:

```
Authorization: Bearer <octomesh-access-token>
```

---

## 8. Implementation Plan

### Phase 1: Basics (MVP)
- [ ] ASP.NET Core project setup
- [ ] `about`, `ops`, `formats` endpoints
- [ ] `read` endpoint with simple filters
- [ ] JSON Grid Builder
- [ ] EnergyIQ Type → Haystack tag mapping

### Phase 2: Navigation & History
- [ ] `nav` endpoint (ParentChild traversal)
- [ ] `hisRead` endpoint (OctoMesh TimeSeries)
- [ ] Haystack filter parser (complete)
- [ ] Zinc format support

### Phase 3: Write & Real-Time
- [ ] `hisWrite` endpoint
- [ ] `pointWrite` endpoint
- [ ] `watchSub/watchUnsub/watchPoll` (WebSocket)
- [ ] SCRAM authentication

### Phase 4: Production
- [ ] Performance optimization (caching, batching)
- [ ] Monitoring & logging
- [ ] Docker image
- [ ] Documentation

---

## 9. Project Structure

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

## 10. Example Configuration

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

## 11. Summary

The Haystack Adapter enables:

1. **Standardized access** to EnergyIQ data via the Haystack protocol
2. **Tool integration** with SkySpark, FIN Framework, Widesky, etc.
3. **Bidirectional data exchange** (read + write)
4. **Real-time updates** via watch mechanism
5. **TimeSeries access** for historical analysis

The adapter acts as a **bridge** between the object-oriented EnergyIQ model and the tag-based Haystack world.
