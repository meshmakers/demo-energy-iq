# EnergyIQ - Intelligent Building Energy Optimization

**EnergyIQ** is an OctoMesh-based solution for building energy optimization. It combines standardized building data models (IFC, VDI 3814) with AI-powered optimization.

## Getting Started

### Prerequisites

1. **OctoMesh Platform** - Follow the installation guide:
   [OctoMesh Getting Started Locally](https://docs.meshmakers.cloud/docs/technologyGuide/gettingStartedLocally/prerequisites)

2. **OctoMesh CLI** (`octo-cli`) - Installed with the platform

3. **.NET SDK 10.0** - For building the Construction Kit

4. **PowerShell** - For running the setup scripts

### Installation

#### 1. Build the project

```bash
cd demo-energy-iq
dotnet build -c Release
```

#### 2. Log in to OctoMesh

```powershell
cd scripts
./om_login_local.ps1
```

The script configures the local OctoMesh instance and opens the browser for authentication.

#### 3. Create tenant

```powershell
./om_create_tenants.ps1
```

Creates the tenant `energyiqdemo` with its own database.

#### 4. Import Construction Kit

```powershell
./om_importck.ps1
```

Imports the following Construction Kits:
- **Basic** - Base types (NamedEntity, Tree, TreeNode)
- **EnergyIQ** - Domain model (Space, Building, TechnicalSystem, etc.)

#### 5. Import runtime data

```powershell
./om_importrt.ps1
```

Imports:
- **Adapter** - Mesh Adapter configuration
- **Pipelines** - Simulation pipeline for demo data
- **Queries** - Predefined queries
- **BIM Data** - Demo building "Firmianstraße 31A" with:
  - 2 buildings (main building + annex)
  - 3 storeys with 12 rooms
  - PV system (4 strings, 2 inverters, battery storage)
  - HVAC systems (heat pump, air handling unit)

### Starting the Simulation

After import, the simulation pipeline runs automatically, generating realistic sensor data every 10 seconds:

| Data Type | Range | Description |
|-----------|-------|-------------|
| Temperature | 18-24°C | Diurnal cycle (sine) |
| Humidity | 35-65% | Phase-shifted |
| CO2 Level | 500-900 ppm | Triangle wave |
| Illuminance | 100-700 lux | Daylight progression |
| PV Power | 0-18.4 kW | Solar progression |
| Battery Charge | 30-90% | Charge/discharge cycle |

### Accessing the Data

After installation, the data is available via the OctoMesh GraphQL API:
- **GraphQL Playground**: `https://localhost:5001/graphql`
- **Tenant**: `energyiqdemo`

Example query for all rooms with current measurements:
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

A platform that:
- Represents buildings as a **Digital Twin** (rooms, building services, sensors)
- Captures, aggregates, and analyzes **energy data**
- Provides **AI optimization** for heating, cooling, ventilation, and lighting
- Is **standards-compliant** (ISO 16739-1 IFC, VDI 3814)

## Architecture

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

## Standards Reference

### ISO 16739-1:2024 (IFC 4.3)
Industry Foundation Classes for BIM data exchange. Provides the **spatial structure**:
- Site → Building → BuildingStorey → Space
- BuildingElements (Wall, Door, Window, etc.)

### VDI 3814 (Building Automation)
German guideline for building automation systems. Provides the **automation model**:
- Room automation (RA) / Plant automation (AA)
- BA functions (controllers, schedulers, etc.)
- Data point structure

## Construction Kit Model

See [docs/construction-kit.md](docs/construction-kit.md) for the complete CK specification.

### Core Concept: OO Instead of Data-Point-Centric

Measurements are **attributes on the object**, not separate entities:

```
Space
├── Temperature: number        ← TimeSeries (actual value)
├── TemperatureSetpoint: number ← Setpoint
├── HeatingValvePosition: number ← Control output
└── ...
```

Not:
```
Space ──► DataPoint("Temperature")  ← Avoid indirection
```

## Project Structure

```
demo-energy-iq/
├── docs/
│   ├── developer-guide.md     # Developer Guide (EN)
│   ├── construction-kit.md    # CK Specification
│   └── standards-reference.md # IFC & VDI 3814 Details
├── src/
│   └── EnergyIqCkModel/
│       └── ConstructionKit/   # CK definitions (YAML)
│           ├── ckModel.yaml   # Model metadata
│           ├── types/         # 18 entity types
│           ├── attributes/    # 30 attribute definitions
│           ├── associations/  # 7 associations
│           ├── records/       # 3 record types
│           └── enums/         # 6 enumerations
├── data/
│   ├── bim/                   # RT model examples
│   │   └── rt-firmianstrasse.yaml
│   ├── _pipelines/            # Simulation adapters
│   │   └── rt-simulation-adapters.yaml
│   ├── _general/              # General adapters
│   └── _queries/              # Predefined queries
├── scripts/                   # Setup scripts
│   ├── om_login_local.ps1     # OctoMesh login
│   ├── om_create_tenants.ps1  # Create tenant
│   ├── om_importck.ps1        # Import CK
│   ├── om_importrt.ps1        # Import RT data
│   └── om_delete_tenants.ps1  # Delete tenant
└── README.md
```

## Features

- [x] **Spatial Structure** - Site, Building, BuildingStorey, Space
- [x] **Building Elements** - Wall, Door, Window, ShadingDevice, Luminaire
- [x] **Technical Systems** - Boiler, AirHandlingUnit, Chiller, Pump
- [x] **PV System** - PhotovoltaicSystem, PVString, Inverter, BatteryStorage
- [x] **VDI 3814 Attributes** - Actual values, setpoints, control outputs, operating modes
- [x] **Simulation Pipeline** - Realistic sensor data simulation
- [ ] **Energy Aggregation** - Consumption calculation per room/building
- [ ] **AI Optimization** - AI-powered optimization

## License

See [LICENSE](LICENSE)
