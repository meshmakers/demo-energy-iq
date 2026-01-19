# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

EnergyIQ is an OctoMesh Construction Kit (CK) model for intelligent building energy optimization. It defines a domain model based on ISO 16739-1:2024 (IFC 4.3) for spatial structure and VDI 3814 for building automation, with optional Project Haystack compatibility.

**Key Principle:** Object-oriented data modeling where measurements are attributes ON objects (e.g., `Space.temperature`), not separate DataPoint entities.

## Build Commands

```bash
# Build the project (compiles CK YAML definitions)
dotnet build

# Build release configuration
dotnet build -c Release

# Clean and rebuild
dotnet clean && dotnet build
```

The build automatically processes YAML files in `src/EnergyIqCkModel/ConstructionKit/` via OctoMesh MSBuild tasks.

## Architecture

### Project Structure

```
src/EnergyIqCkModel/
├── ConstructionKit/          # CK model definitions
│   ├── ckModel.yaml          # Model metadata (modelId: EnergyIQ-1.0.0)
│   ├── types/                # Entity type definitions (14 types)
│   ├── attributes/           # Attribute definitions (30 files)
│   ├── enums/                # Enumeration definitions (6 enums)
│   ├── records/              # Complex value type definitions (3 records)
│   └── associations/         # Relationship definitions (7 associations)
└── Samples/                  # (legacy location)
data/bim/                         # RT (Runtime) model examples
└── rt-firmianstrasse.yaml        # Demo property in Salzburg
```

### Type Hierarchy

Based on OctoMesh Basic package for tree structures (IFC IfcRelAggregates):

```
NamedEntity (Basic)
├── Tree (Basic)
│   └── Site                          # Root of spatial hierarchy
├── TreeNode (Basic)                  # Inherits ParentChild association
│   ├── Building → Site
│   ├── BuildingStorey → Building
│   └── Space → BuildingStorey        # Central object (28 attributes)
└── NamedEntity (Basic)
    ├── BuildingElement (abstract) + Haystack
    │   ├── Wall, Door, Window, ShadingDevice, Luminaire
    └── TechnicalSystem (abstract) + Haystack
        ├── AirHandlingUnit, Boiler, Chiller, Pump
```

### Key Relationships

- **Spatial hierarchy (ParentChild):** Site ← Building ← BuildingStorey ← Space (IFC: IfcRelAggregates)
- **Element containment:** Space (1:N) ↔ BuildingElement
- **System supply:** Space (N:M) ↔ TechnicalSystem
- **Building systems:** Building (1:N) → TechnicalSystem

**Note:** Name and Description are inherited from Basic/NamedEntity, not defined in EnergyIQ.

## CK YAML Conventions

### Schema Reference
All CK YAML files must include:
```yaml
$schema: https://schemas.meshmakers.cloud/construction-kit-elements.schema.json
```

Model metadata uses:
```yaml
$schema: https://schemas.meshmakers.cloud/construction-kit-meta.schema.json
```

### Reference Syntax
- `${this}` - Current model (EnergyIQ)
- `${Basic}` - Basic package (NamedEntity, Tree, TreeNode)
- `${System}` - System model (base types from OctoMesh)

### Value Types
Valid valueType values: `String`, `Boolean`, `DateTime`, `Int`, `Double`, `StringArray`, `IntArray`, `Record`, `RecordArray`, `TimeSpan`, `Enum`, `Int64`, `DateTimeOffset`, `Binary`, `BinaryLinked`, `GeospatialPoint`

### Example Patterns

**Enum:**
```yaml
enums:
- enumId: SpaceType
  values:
  - key: 0
    name: Office
  - key: 1
    name: MeetingRoom
```

**Attribute (simple):**
```yaml
attributes:
- id: Temperature
  valueType: Double
```

**Attribute (enum):**
```yaml
attributes:
- id: SpaceTypeValue
  valueType: Enum
  valueCkEnumId: ${this}/SpaceType
```

**Attribute (record array):**
```yaml
attributes:
- id: ScheduleEntries
  valueType: RecordArray
  valueCkRecordId: ${this}/ScheduleEntry
```

**Record:**
```yaml
records:
- recordId: Address
  attributes:
  - id: ${this}/Street
    name: street
  - id: ${this}/City
    name: city
```

**Type (spatial, inheriting from TreeNode):**
```yaml
types:
- typeId: Space
  derivedFromCkTypeId: ${Basic}/TreeNode
  attributes:
  - id: ${this}/Temperature
    name: temperature
    isOptional: true
  associations:
  # ParentChild inherited from TreeNode for spatial hierarchy
  - id: ${this}/SpaceElements
    targetCkTypeId: ${this}/BuildingElement
```

**Association:**
```yaml
associationRoles:
- id: SpaceElements
  inboundName: containedElements
  outboundName: containedInSpace
  inboundMultiplicity: N
  outboundMultiplicity: ZeroOrOne
```

### Implementation Order
Create CK elements in this order: Enums → Records → Attributes → Types → Associations

## Sample Data

The `data/bim/rt-firmianstrasse.yaml` file demonstrates a complete RT model for a residential property:
- **Site:** Firmianstraße 31A, 5020 Salzburg, Austria
- **Buildings:** Hauptgebäude (3 storeys: EG, OG, DG) + Nebengebäude (Garage, Werkstatt, Waschküche)
- **Systems:** Wärmepumpe (heat pump), KWL (ventilation with heat recovery), Heizkreispumpe
- **Elements:** Windows with shading devices, luminaires
- **Full VDI 3814:** Actual values, setpoints, control signals, operating modes

## Documentation

Detailed specifications are in `docs/`:
- `developer-guide.md` - English developer guide explaining design philosophy and standards integration
- `construction-kit.md` - Complete CK specification with all types, attributes, and associations
- `implementation-plan.md` - Phased implementation roadmap with checklists (Phase 1-6)
- `standards-reference.md` - IFC 4.3 and VDI 3814 mapping reference

**IMPORTANT:** After any structural changes to the CK model (types, attributes, associations, inheritance), update `docs/developer-guide.md` to reflect the changes. Keep the documentation in sync with the actual implementation.
