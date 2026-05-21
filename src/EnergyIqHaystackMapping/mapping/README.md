# EnergyIQ Haystack Mapping

Declarative mapping config from EnergyIQ-2.0.0 to Project Haystack 4. Consumed by an export renderer, a PH4-`lib` generator, and a future PH adapter.

For the architecture decision and rationale see `../../docs/haystack-integration-concept.md`. For the conceptual fit between PH4 specs and OctoMesh CK see `../../docs/standards-reference.md` (Haystack section).

## File layout

```
mapping/
├── _index.yaml              # lib metadata, defaults, conventions
├── README.md                # this file
└── <typeName>.yaml          # one file per non-abstract EnergyIQ type
```

Abstract CK types (`Sensor`, `Actuator`, `RoomTerminal`, `HydronicTerminal`, `BuildingElement`, `TechnicalSystem`) have no mapping file — they exist only in the inheritance chain. Concrete subtypes carry the full mapping.

## Mapping schema

Each type file is a single YAML document with the following keys.

### Required

```yaml
ckTypeId: EnergyIQ/Space                    # the CK type this mapping is for
phSpec: ph::Space                           # the PH4 spec the emitted dict claims
markers: [space, room]                      # marker tags always set
```

### Optional dict-level extras

```yaml
tags:                                       # static value tags on every emitted dict
  - { name: navName, sourceAttribute: RoomNumber }   # from CK attribute
  - { name: dis, sourceAttribute: System/Name }
```

`navName`, `dis`, etc. are commonly populated from a CK attribute via `sourceAttribute`. A literal value uses `value:` instead.

### Refs (relationships expressed as PH refs)

```yaml
refs:
  - phRef: spaceRef                         # PH ref slot
    sourceRole: System/ParentChild          # CK association
    direction: parent                       # parent | ancestor | children
    targetCkTypeId: EnergyIQ/BuildingStorey # filter target type (optional but recommended)
  - phRef: siteRef
    sourceRole: System/ParentChild
    direction: ancestor                     # walk up until matching type
    targetCkTypeId: EnergyIQ/Site
```

Direction semantics:
- `parent` — one step up via `ParentChild` (inbound, child→parent)
- `ancestor` — walk up `ParentChild` repeatedly until the target type matches
- `children` — outbound (rarely needed for refs; PH usually refs the other way)

### Attributes (dict-level value tags)

```yaml
attributes:
  - ckAttribute: NetFloorArea
    phTag: area
    kind: Number
    unit: "m²"
  - ckAttribute: SpaceTypeValue
    phTag: spaceType
    kind: Str
    enumMapping:                            # CK enum key → PH string
      0: office
      1: meetingRoom
      15: livingRoom
```

`kind` follows PH4: `Marker`, `Bool`, `Number`, `Str`, `Ref`, `Date`, `Time`, `DateTime`, `Coord`.

### Points (synthetic Point entities derived from CK attributes)

Used when a single CK entity carries multiple measurement/setpoint attributes that should explode into separate PH `Point` entities. The most common case in v2 is actuators (Valve.Position + Valve.PositionSetpoint → two Points).

```yaml
points:
  - ckAttribute: Position
    markers: [point, sensor, valve, position]
    kind: Number
    unit: "%"
    refTo: { phRef: equipRef, target: self }    # ref back to the source equipment
    navName: pos
  - ckAttribute: PositionSetpoint
    markers: [point, sp, cmd, valve, position]
    kind: Number
    unit: "%"
    refTo: { phRef: equipRef, target: self }
    navName: posSp
    writable: true
```

When the mapping target IS a sensor entity (e.g. `TemperatureSensor`), the entity itself becomes a Point and no `points:` section is needed — `markers` carry the point markers directly on the dict and `attributes` exposes `CurrentValue` as the point value (`kind: Number` / `Bool`).

## Identity & refs

PH `id` is derived per `_index.yaml` `identityStrategy`:
- prefer `GlobalId` where the source CK type carries it
- fall back to `rtId` (24-hex MongoDB ObjectId)
- the renderer prefixes both with `@energyiq:`

Refs (`siteRef`, `spaceRef`, `equipRef`, …) emit the target's id following the same strategy.

## Conventions

- **Marker order**: most general first, more specific last (e.g. `[point, sensor, temp, air, zone]`).
- **PH-conformant units**: use the strings in `_index.yaml/unitConventions`. Avoid free-form unit strings.
- **Tag values vs markers**: a Marker has no value (just a tag name). A value tag has `kind: Number/Str/Bool/...` and a value source.
- **No PH tags on EnergyIQ entities**: v2 EnergyIQ types carry no Haystack mixin attributes. All PH semantics live in this mapping layer.

## What's not mapped

These are intentionally left without mapping files:

- **Abstract types** (Sensor, Actuator, RoomTerminal, HydronicTerminal, BuildingElement, TechnicalSystem): only concrete subtypes get mapped.
- **Wall**: PH does not model walls — purely structural BIM data, not relevant to control/energy.
- **Schedule / DistributionSystem**: logical aggregation entities. PH has `weeklySchedule` and grouping conventions, but they vary by toolchain. Defer until a concrete consumer requests them.
