# MeshBoards: can one site selector drive every widget? (AB#7692)

Analysis of the five boards in `data/_general/rt-meshboards-energyiq.yaml` against the
octo-meshboard entity selector (`EntitySelectorConfig`, `EntitySelectorChildScope`,
`PersistentQueryDataSource.entitySelectorId`). Result on 2026-10-28: **not with data configuration alone**
(platform gaps below). The gaps were closed in octo-frontend-libraries by Feature AB#7719 (AB#7721-AB#7725,
merged 2026-10-30); Energie and Energiebilanz now have one visible site selector — see
[Part 2: implemented design](#part-2-implemented-design-2026-10-30). The three room boards followed in AB#7693 — see
[Part 3: room boards](#part-3-room-boards-ab7693-2026-10-30).

## Platform facts the analysis rests on

| # | Fact | Source |
|---|------|--------|
| F1 | A selector has exactly one `childScope` (one association hop, one target type). | `meshboard.models.ts` `EntitySelectorChildScope` |
| F2 | A widget binds exactly one selector (`entitySelectorId`); a selector cannot follow another selector. A hidden selector keeps its `defaultRtId`. | `meshboard-view.component.ts` |
| F3 | `entitySelectorId` is ignored for runtime queries (SimpleRtQuery); only stream-data queries are scoped. | model doc of `entitySelectorId` |
| F4 | An empty or failed scope resolution falls back to the query's persisted scope, i.e. the whole archive (`resolveStreamDataRtIds` returns `undefined`). A site without e.g. PV therefore shows foreign rows instead of an empty state. | `meshboard-state.service.ts` |
| F5 | The selector picker lists every entity of `ckTypeId` (no filter): a `DistributionSystem` selector offers the heating and cooling circuits as well as the electrical system. | `EntitySelectorConfig` has no filter |
| F6 | There is no Site -> DistributionSystem association. The electrical `DistributionSystem` is the de-facto site of the energy boards; the BatteryStorage is a member of the PhotovoltaicSystem, not of the DistributionSystem (two hops). | `rt-firmianstrasse.yaml` |

## Widget table

Scope = what the widget needs from the picked site. "One selector?" = can one visible site
selector drive it with data configuration only.

### Energie (6a4aa4fa22664f2b26c67e7c)

| Widget | Query (family) | Needs | One selector? |
|--------|----------------|-------|---------------|
| Netzbezug (Hausanschluss) | SimpleSdQuery, GridConnectionArchive (stream) | GridConnection rtIds of the site | no: DS -> GridConnection is one hop, but see F2/F4 |
| PV-Produktion | SimpleSdQuery, PhotovoltaicSystemArchive (stream) | PhotovoltaicSystem rtIds | no: other target type than the grid widget (F1/F2) |
| Wallboxen | SimpleSdQuery, ChargingStationArchive (stream) | ChargingStation rtIds | no: F1/F2 |
| Geräte | SimpleSdQuery, ApplianceArchive (stream) | Appliance rtIds | no: F1/F2 |
| Batteriespeicher | SimpleSdQuery, BatteryStorageArchive (stream) | BatteryStorage rtIds | no: two hops DS -> PV -> Battery (F6) |
| Aktuelle Leistungen | SimpleRtQuery EnergyIQ/Meter (runtime) | Meters of the site | no: runtime query (F3) |

A polymorphic scope (`childScope` target `EnergyIQ/Meter`, base of GridConnection, ChargingStation,
Appliance) would fan out rtIds that have no rows in the queried per-type archive and produce ghost
legend entries (see the header of the YAML); PhotovoltaicSystem and BatteryStorage are not Meters.

### Energiebilanz (6a4aaf1922664f2b26c67ecc)

| Widget | Query (family) | Needs | One selector? |
|--------|----------------|-------|---------------|
| PV-Produktion, Eigenverbrauch, Netzeinspeisung (3 KPIs) | AggregationSdQuery, PhotovoltaicSystemArchive | PhotovoltaicSystem rtIds | no: F1/F2, F4 (a site without PV would show Firmianstraße values) |
| Netzbezug | AggregationSdQuery, GridConnectionArchive | GridConnection rtIds | partly: scoped by hidden selector `netz` (this PR), not by a visible one |
| Batterie geladen / entladen (2 KPIs) | AggregationSdQuery, BatteryStorageArchive | BatteryStorage rtIds | no: two hops (F6) |

### Raumtemperaturen (6a4a9f277e487162fbe3860a)

| Widget | Query | One selector? |
|--------|-------|---------------|
| Raumtemperatur - Verlauf | SimpleSdQuery, TemperatureSensor; room selector (Space -> sensors) | yes, but the selector picks a room, not a site |
| Aktuelle Raumtemperaturen | SimpleRtQuery | no (F3) |
| Außentemperatur - Verlauf | SimpleSdQuery, no selector | no: needs its own scope (outdoor sensor of the site), F1/F2 |

### Heizung & Kühlung (6a4b4000000000000000e100)

No selector. Stream-data widgets (Vorlauf, Rücklauf, Sole, Warmwasserspeicher, WP Leistungsaufnahme,
Zählerstand, three KPIs) need HeatPump / ThermalEnergyStorage / Meter scopes: one selector cannot
provide several target types (F1/F2). The three tables are runtime queries (F3).

### Beschattung & Beleuchtung (6a4d5000000000000000b100)

Two room selectors (Licht, Beschattung), no site selector. Line charts follow their room selector;
the three Luminaire grouping bar charts, the ShadingDevice bar chart and both tables are unscoped
(grouping queries are not scoped by selectors in the view; tables are runtime queries, F3).

## Platform gaps (proposal for Studio & UX)

1. **Master/follower selectors.** A hidden selector must be able to follow a visible one
   (`followsSelectorId` or per-widget `childScope` override), so that one site pick resolves
   different scopes (grid, PV, wallbox, appliance) per widget. Without it every scope needs its
   own visible pick (F2).
2. **Multi-hop scope.** `childScope` with a path of roles (DS -> PV -> Battery) or a transitive
   member traversal (F6).
3. **Explicit empty scope.** A resolved empty scope must make the widget show "no data" instead
   of falling back to the persisted (whole archive) scope (F4). Prerequisite for any second site.
4. **Selector filter** (`ckTypeId` + attribute filter, e.g. `DistributionSystemType = Electrical`)
   so that the site picker lists sites and not heating circuits (F5).
5. **Runtime queries and grouping queries bound to a selector** (F3), e.g. a `childScope`-derived
   `rtId in (...)` filter, for the "Aktuelle ..." tables and the bar charts.

No workaround (app code, custom widget, per-site copy pipeline, second board) was built.

## Energiebilanz kWh = SUM(kW) x 10/3600 (2026-10-28; measured in Part 2 below)

The factor assumes exactly one row every 10 s per entity. It holds only for uniformly sampled
archives.

- Firmianstraße Loxone archives are written on change (state-change websocket, rt-pipelines-loxone.yaml):
  the sum of change-driven rows is not a time integral; the factor is **not valid** in general
  (only approximately where the source emits a steady 10 s cadence).
- Fusch SHRDZM rows (rt-pipelines-fusch-mqtt.yaml): one row per reading, sent in 30 s batches; the
  cadence is the meter's publishing cadence, not recorded in the repo (the sample is constructed).
  The factor is **not verified** for Fusch grid rows. The story's check against the register delta
  (ImportedEnergy / ExportedEnergy of GridConnection 6a10f05c0000000000000a03) needs live Fusch rows
  and was not possible here.
- Correct alternatives need a platform capability: a time-weighted integral in aggregation queries
  (the Beschattung board already uses a time-weighted rollup) or the register delta of the meter.

## Done in part 1 (PR #11)

The Energiebilanz KPI "Netzbezug" summed the shared GridConnectionArchive including the Fusch rows
(AB#7511). It is now scoped by a hidden selector `netz` (DistributionSystem, default Firmianstraße,
childScope GridConnection) like the Energie line chart. Other KPIs are unaffected (their archives
hold only Firmianstraße rows).

## Part 2: implemented design (2026-10-30)

With AB#7719 in octo-frontend-libraries (follower selectors AB#7722, multi-hop path AB#7723, picker filter AB#7724,
empty scope AB#7721, runtime/grouping queries bound to a selector AB#7725) both boards are configured with data only.

### Leader: the electrical DistributionSystem

| Field | Value |
|-------|-------|
| id / label | `standort` / "Standort", `showInToolbar: true` |
| ckTypeId | `EnergyIQ/DistributionSystem` |
| filters | `systemType EQUALS ELECTRICAL` (lists exactly one system per site; heating, cooling and ventilation circuits are hidden) |
| defaultRtId | `6789a00000000000006000d3` (Firmianstraße "Elektrisch (PV + Eigenverbrauch)") — the only site rtId on the boards |

Why not `EnergyIQ/Site`: neither site has an association from the Site to its electrical entities (F6; the
Firmianstraße GridConnection has no spatial association at all), so every follower would need a path the model does
not have. The electrical DistributionSystem already groups everything the boards show over one association
(`EnergyIQ/SystemMembers`, outbound) at both sites. Consequence: the picker shows the system names
("Elektrisch (PV + Eigenverbrauch)", "Elektrisch Fusch"), not the site names (open point: rename in the model stories).

### Followers

All followers: `followsSelectorId: standort`, `showInToolbar: false`, no default, role `EnergyIQ/SystemMembers`
direction `out`.

| Follower | Scope | Firmianstraße | Fusch |
|----------|-------|---------------|-------|
| `netz` | DS → GridConnection | `…3000a1` | `…0a03` |
| `pv` | DS → PhotovoltaicSystem | `…0080a1` | `…0a12` |
| `wallboxen` | DS → ChargingStation | `…3010b1`, `…3011b2` | `…0a16` |
| `geraete` | DS → Appliance | `…3020c1`, `…3021c2` | empty |
| `batterie` | path DS → PhotovoltaicSystem → BatteryStorage | `…0087d1` | empty |
| `zaehler` | DS → Meter (polymorphic: also GridConnection, ChargingStation, Appliance) | 12 meters | `…0a03`, `…0a16` |

The polymorphic `zaehler` scope is used only by the runtime table (`rtId IN` filter), never by an archive query, so it
produces no ghost series (header of the YAML).

### Widgets

| Board | Widget | Query (family) | Follower | Fusch |
|-------|--------|----------------|----------|-------|
| Energie | Netzbezug (Hausanschluss) | SimpleSdQuery GridConnectionArchive (stream) | `netz` | SHRDZM data |
| Energie | PV-Produktion | SimpleSdQuery PhotovoltaicSystemArchive (stream) | `pv` | Loxone data (since AB#7696) |
| Energie | Wallboxen | SimpleSdQuery ChargingStationArchive (stream) | `wallboxen` | no rows → empty chart (on prod-1 the ChargingStationArchive and ApplianceArchive had no rows for either site on 2026-10-29/30) |
| Energie | Geräte | SimpleSdQuery ApplianceArchive (stream) | `geraete` | empty scope → no data |
| Energie | Batteriespeicher (Ladeleistung) | SimpleSdQuery BatteryStorageArchive (stream) | `batterie` | empty scope → no data |
| Energie | Aktuelle Leistungen | SimpleRtQuery EnergyIQ/Meter (runtime) | `zaehler` | 2 rows |
| Energiebilanz | PV-Produktion | AggregationSdQuery PhotovoltaicSystemArchive SUM | `pv` | see kWh |
| Energiebilanz | Eigenverbrauch (PV) | AggregationSdQuery SUM SelfConsumption | `pv` | column not written for Fusch → no value |
| Energiebilanz | Netzeinspeisung | AggregationSdQuery SUM GridFeedIn | `pv` | column not written for Fusch → no value |
| Energiebilanz | Netzbezug | AggregationSdQuery GridConnectionArchive SUM ActivePower > 0 | `netz` | see kWh |
| Energiebilanz | Batterie geladen | AggregationSdQuery BatteryStorageArchive SUM > 0 | `batterie` | empty scope → no data |
| Energiebilanz | Batterie entladen | AggregationSdQuery BatteryStorageArchive SUM < 0 | `batterie` | empty scope → no data |

Validation of the configuration (without a Studio run): the selectors of both boards were run
through the library's own pure helpers at octo-frontend-libraries `origin/main` (`resolveFollowLeader`,
`followValidationError`, `scopePlan`, `resolveScopePath`, `hasPickerFilter`) with a hop fetcher that sends the
library's `getAssociationTargetRtIds` query read-only to prod-1/energyiq: one visible selector per board, all followers
valid, no invalid plan, scopes as in the table above. The picker filter was checked with the same GraphQL
`fieldFilter` read-only on prod-1: it returns exactly the two electrical systems.

Fixed Firmianstraße rtIds (AC 4): in the 26 entities of the two boards (2 dashboards, 12 widgets, 12 queries) `6789a0`
occurs only as the leader's `defaultRtId` (once per board).

What changes for Firmianstraße compared with before (data, not layout):
- "Aktuelle Leistungen" listed every Meter of the tenant, on prod-1 including the Fusch grid connection and wall
  connector; it now lists the 12 Firmianstraße meters.
- Energiebilanz "PV-Produktion" summed the whole PhotovoltaicSystemArchive, which on prod-1 contains the Fusch PV rows
  since AB#7696 (2026-10-30 06:06Z); scoped now. Example window 2026-10-29T10:00Z-2026-10-30T10:00Z: before
  92.21 + 2.16 = 94.37 "kWh", after 92.21.
- Energiebilanz "Netzbezug" was already scoped by AB#7692 part 1. All other widgets read the same rtIds as before.

## Energiebilanz kWh: measured per KPI and site (AC 7)

Read-only exports from prod-1/energyiq (`octo-cli -c ExportArchiveData`), window 2026-10-29T00:00Z-2026-10-30T00:00Z
(Fusch PV: 2026-10-30T06:06Z-10:00Z, first live day). "Factor" = what the KPI shows (SUM × 10/3600); "integral" =
time-weighted (last value held until the next row) energy from the same rows.

| Archive | Site | Rows / cadence | KPI | Factor | Integral | Factor holds? |
|---------|------|----------------|-----|--------|----------|---------------|
| GridConnection | Firmianstraße (Loxone) | 16 797, median 5.0 s (p5 4.7, p95 5.6) | Netzbezug (> 0) | 33.47 kWh | 17.28 kWh | no, about 2x |
| GridConnection | Fusch (SHRDZM) | 17 276, median 5.0 s (p5 4.95, p95 5.05) | Netzbezug (> 0) | 20.11 kWh | 10.06 kWh | no, 2x |
| PhotovoltaicSystem | Firmianstraße (Loxone, on change) | 11 539, median 2.8 s, gaps up to 218 s | PV-Produktion | 87.20 kWh | 23.58 kWh | no, 3.7x |
| PhotovoltaicSystem | Fusch (Loxone, on change) | 1 150 in 3.9 h, median 10.1 s, p95 20 s, gaps up to 561 s | PV-Produktion | 2.16 kWh | 2.47 kWh | no, -12 % |
| PhotovoltaicSystem | both | SelfConsumption / GridFeedIn not written | Eigenverbrauch, Netzeinspeisung | no value | — | — |
| BatteryStorage | Firmianstraße (Loxone, on change) | 24 177, many rows with the same timestamp | Batterie geladen | 56.98 kWh | 7.80 kWh | no, 7x |
| BatteryStorage | Fusch | no battery | Batterie geladen / entladen | empty | — | — |

**Fusch grid against the meter registers (AC 7):** the registers `ImportedEnergy` / `ExportedEnergy` of GridConnection
`6a10f05c0000000000000a03` were read read-only once a minute on prod-1, and the GridConnectionArchive rows of the same
readings exported. Window 2026-10-30T10:30:10.7Z-11:15:45.8Z (45.6 min, 547 rows, median 4.99 s):

| Figure | Import | vs register |
|--------|--------|-------------|
| Register delta `ImportedEnergy` | 0.924 kWh | — |
| KPI "Netzbezug" (SUM of ActivePower > 0 × 10/3600) | 1.857 kWh | **+101 %** |
| Time-weighted integral of the same rows | 0.929 kWh | +0.5 % |

(Export: register delta 0.068 kWh, time-weighted integral 0.072 kWh.) The 1 % criterion fails for the KPI; the rows
themselves are good enough — a time-weighted integral matches the meter within 0.5 %. The register cannot replace the
KPI by configuration: the GridConnectionArchive stores only `ActivePower`, so no register value is available for an
arbitrary window.

Conclusion: on the live tenant prod-1 the factor holds for **no** KPI at **any** site; it holds only for the demo
simulator (`rt-simulation-adapters.yaml`, rows exactly every 10 s). A per-site empty state cannot be configured — it
would have to depend on the cadence, not on the scope — and a cadence-specific factor (e.g. 5/3600 for the grid) would
be wrong on simulator tenants that import the same file. The KPIs are therefore unchanged in value, the board
description and the YAML state the limit, and the correct figure is a platform gap: an energy aggregation for
AggregationSdQuery (time-weighted integral ∫P dt over the query window, LOCF — `TimeWeightedAverage` exists but needs
a rollup and the KPI cannot multiply it by the window length). Reported to Odin on AB#7692. Whether the KPIs should
show nothing until then is open for the developer.

## Part 3: room boards (AB#7693, 2026-10-30)

Raumtemperaturen (`6a4a9f277e487162fbe3860a`), Heizung & Kühlung (`6a4b4000000000000000e100`) and Beschattung &
Beleuchtung (`6a4d5000000000000000b100`) get the same visible selector "Standort", configured with data only.

### Leader: the Site, not the electrical DistributionSystem

| Field | Value |
|-------|-------|
| id / label | `standort` / "Standort", `showInToolbar: true` |
| ckTypeId | `EnergyIQ/Site` (no picker filter: the type holds only sites) |
| defaultRtId | `6789a00000000000000000a1` (Firmianstraße 31A) — the only site rtId of these selectors |

Why a second leader type: rooms, sensors and plant hang off the spatial tree (Site → Building → BuildingStorey →
Space). The electrical DistributionSystem reaches it only where it models `SystemSpaces` (Firmianstraße: the four
storeys; Fusch: none), or through PV → PVString → Building, which with the storey level makes six hops (maximum five).
Checked read-only on prod-1 with the library's hop query:

| Candidate path (to TemperatureSensor) | Firmianstraße | Fusch (today) |
|----------------------------------------|---------------|---------------|
| DS → SystemSpaces → Space → sensor | 0 | 0 |
| DS → SystemSpaces → Storey → Space → sensor | 18 | 0 (no SystemSpaces) |
| DS → PV → PVString → Building → Space → sensor | 0 | 5 (no storeys yet) |
| Site → Building → Space → sensor | 0 | 5 |
| **Site → Building → Storey → Space → sensor** (chosen) | **18** | **0 (5 with the storeys of AB#7889)** |

Trade-off: the energy boards and the room boards have different leader types under the same label. A pick does not
carry over from one kind of board to the other (it does not today either — each board keeps its own URL state), and the
room boards list site names ("Firmianstraße 31A", "Fusch") while the energy boards list system names. In return the room
boards depend on the spatial tree only, which every site has, and on no modelling choice of the electrical system.

Precondition per site: storeys. A fixed path cannot serve a room directly under a Building and a room under a storey at
the same time. Fusch had rooms directly under its Building; Gerald decided that Fusch gets storeys (UG, EG, OG; story
AB#7889), so both sites have Building → BuildingStorey → Space. A tenant whose Fusch data still has rooms directly
under the Building resolves an empty room scope for Fusch (empty state, never another site's rows).

### Followers

All followers: `followsSelectorId: standort`, `showInToolbar: false`, no default; `childScope.path` starts with
`System/ParentChild` in → Building, → BuildingStorey, → Space (3 hops) and adds one hop:

| Follower | Last hop | Firmianstraße (prod-1) | Fusch (with AB#7889 storeys) |
|----------|----------|------------------------|---------------------|
| `raumsensoren` | `EnergyIQ/SpaceSensors` in → TemperatureSensor | 18 room sensors | 5 room sensors |
| `aussensensor` | path Site → `System/ParentChild` in → ExternalSpace → `EnergyIQ/SpaceSensors` in → TemperatureSensor (2 hops) | `…10051a1` | empty (no outdoor sensor) |
| `waermepumpe` | `System/ParentChild` in → HeatPump | `…60f1` | empty |
| `speicher` | `System/ParentChild` in → ThermalEnergyStorage | `…63f4` (Heizpuffer), `…64f5` | empty |
| `pumpe` | `System/ParentChild` in → Pump | `…62f3` | empty |
| `leuchten` | `EnergyIQ/SpaceElements` in → Luminaire | 18 luminaires | 10 luminaires |
| `raffstores` | `EnergyIQ/SpaceElements` in → ShadingDevice | 8 blinds | 6 blinds |

The Fusch heat mixer (Valve `…0a40`) and its flow sensor (`…0a41`, `EquipmentSensors`) are not reached: the boards have
no valve widget, and `raumsensoren` follows `SpaceSensors` only.

### Widgets

| Board | Widget | Query (family) | Selector | Fusch |
|-------|--------|----------------|----------|-------|
| Raumtemperaturen | Raumtemperatur – Verlauf | SimpleSdQuery TemperatureSensorArchive | `raum` (room picker, unchanged) | the picked room |
| Raumtemperaturen | Aktuelle Raumtemperaturen | SimpleRtQuery TemperatureSensor (runtime) | `raumsensoren` | room sensors |
| Raumtemperaturen | Außentemperatur – Verlauf | SimpleSdQuery TemperatureSensorArchive, pin removed | `aussensensor` | empty |
| Heizung & Kühlung | Wärmepumpe – aktuell | SimpleRtQuery HeatPump (runtime) | `waermepumpe` | empty |
| Heizung & Kühlung | Heizkreis Vorlauf / Rücklauf, Sole Eintritt / Austritt | SimpleSdQuery HeatPumpArchive | `waermepumpe` | empty |
| Heizung & Kühlung | Warmwasserspeicher | SimpleSdQuery ThermalEnergyStorageArchive, pin removed | `speicher` | empty |
| Heizung & Kühlung | Speicher – aktuell | SimpleRtQuery ThermalEnergyStorage (runtime) | `speicher` | empty |
| Heizung & Kühlung | Heizkreispumpe – aktuell | SimpleRtQuery Pump (runtime) | `pumpe` | empty |
| Heizung & Kühlung | Wärmepumpe Firmianstraße – Ø-Leistung, Zählerstand Fensterbeginn, Zählerstand Fensterende, Leistungsaufnahme, Energie kumuliert (5) | AggregationSdQuery / SimpleSdQuery MeterArchive, MeterEnergyArchive, pin `…3007a8` | none (open, AB#7888) | **Firmianstraße values** (exception, title names the site) |
| Beschattung & Beleuchtung | Aktueller Lichtstatus | SimpleRtQuery Luminaire (runtime) | `leuchten` | luminaires |
| Beschattung & Beleuchtung | Aktuelle Beschattung | SimpleRtQuery ShadingDevice (runtime) | `raffstores` | blinds |
| Beschattung & Beleuchtung | Licht-Einschaltquote, Licht-Schaltvorgänge, Brenndauer je Leuchte | GroupingAggregationSdQuery LuminaireStatusArchive | `leuchten` | luminaires |
| Beschattung & Beleuchtung | Ø Beschattung je Raffstore | GroupingAggregationSdQuery ShadingDeviceStatusArchive | `raffstores` | blinds |
| Beschattung & Beleuchtung | Licht – Einschaltgrad Verlauf | SimpleSdQuery LuminaireArchive | `lichtraum` (room picker, unchanged) | the picked room |
| Beschattung & Beleuchtung | Beschattung – Position Verlauf | SimpleSdQuery ShadingDeviceArchive | `beschattungsraum` (room picker, unchanged) | the picked room |

### Open points and gaps

1. **Heat-pump meter (AB#7888).** The five meter tiles need exactly the heat-pump meter `6789a00000000000003007a8`. No
   association or attribute tells it apart from the other Technikraum meters (`…3005a6` IT-Schrank, `…3006a7`
   Technikraum), and all three write rows (prod-1, 2026-10-29: 85k / 15k / 116k rows in MeterArchive). A room-based scope
   would average or chart all three. Decision (Odin, 2026-10-30): keep the pin, title the tiles "Wärmepumpe Firmianstraße
   – …", say it in the board description, no name filter. AC 3 stays open for these five tiles and AC 5 has this
   documented exception. AB#7888 adds the association Meter → TechnicalSystem (EnergyIQ 2.14.0); the follower then ends
   … → Space → HeatPump → Meter (5 hops).
2. **Heizpuffer in the Warmwasserspeicher chart.** `speicher` yields both tanks; the Heizpuffer `…63f4` has no live feed,
   so the chart shows it as an empty legend entry (accepted). The storage table lists both tanks, as before.
3. **Room pickers are not site-scoped (platform gap: dependent picker).** A follower has no picker, and a picker filter
   compares the listed entity's own attributes only and does not reset a pick that no longer matches (AB#7724 contract).
   So "Raum", "Raum (Licht)" and "Raum (Beschattung)" stay independent pickers over all rooms of the tenant, and the three
   per-room line charts show the picked room whatever the site. Each board therefore has two or three visible selectors.
   A picker that lists the rooms of the leader's scope and resets on a leader change would close this.
4. **ImportRt keeps attributes that a file leaves out.** Upsert (`-r`) merges attributes; the removed
   `StreamDataQuery.RtIds` pins of `…38618` and `…e205` stay in a tenant that had them, and an empty list in the file is
   ignored too. The widgets are not affected (a scoped widget overrides the persisted rtIds; an empty scope sends no
   query), but the stale pin is cleared by one GraphQL update per tenant (`systemSimpleSdQuerys.update` with
   `rtIds: []`), done on test-2 and listed in the prod-1 steps of the PR.

### What changes for Firmianstraße (data, not layout)

- "Aktuelle Raumtemperaturen" listed every TemperatureSensor of the tenant (Firmianstraße: 18 rooms + outdoor + two
  heat-pump equipment sensors; on prod-1 also the 6 Fusch sensors); it now lists the 18 room sensors of the site.
- "Aktueller Lichtstatus" / "Aktuelle Beschattung" and the four bar charts no longer include Fusch luminaires and blinds
  (prod-1: 28 → 18 luminaires, 14 → 8 blinds); the Firmianstraße values are the same.
- Warmwasserspeicher chart: one extra empty legend entry (Heizpuffer). All other widgets read the same rtIds as before.

## Part 4: grid heatmap "Netzbezug – Stunde × Tag" (AB#7942, 2026-10-31)

The Energie board gets a heatmap of the grid power of the picked site: one cell per Europe/Vienna hour and day over
the **last 30 days**, independent of the board's Zeitraum (widget `…67e83`, query `…67e6a`, rollup `…67e6b`).

### What a cell shows and why

- **Value: the mean net grid power of that hour in kW** (sample-weighted mean of the raw rows; it estimates the net kWh
  of that hour only if the samples cover the hour evenly, which gaps or irregular sampling break). The column is
  `ActivePower` of the GridConnectionArchive, which is **signed**: import > 0, feed-in < 0. An hour with more feed-in
  than draw shows **≤ 0**. The import-only column `ImportPower` (AB#7925) is not live yet; when it is, the query switches
  to it and the cell becomes pure grid draw.
- **No field filter `ActivePower > 0`.** Averaging only the positive rows would overstate the hour: a half-hour of
  1 kW draw plus a half-hour of feed-in would show 1 kW instead of the real mean.
- **Data path: the 1-hour rollup `GridConnectionRollup1h`** (AVG → `activepower_avg_sum` + `activepower_avg_count` per
  GridConnection and UTC hour). The widget uses `aggregation: weightedAvg` (Σ sum / Σ count per cell), so the cell is the
  **exact** mean of the raw rows, not a mean of means. Europe/Vienna offsets are whole hours, so every UTC hour bucket
  lies inside one Vienna hour, DST days included.
- **Rollup rows are stamped at the bucket end** (`timestamp` = `window_end`); `timestampIsBucketEnd: true` +
  `bucketWidthMinutes: 60` shift each row back to its bucket start before the hour/day bucketing. The server-side time
  filter and the query sort use `window_start` (a rollup has no `timestamp` column; sorting on it fails).
- **Own window:** `timeWindow: {value: 30, unit: days}`; the widget shows the badge "Last 30 days".
- **Unit:** the heatmap legend has no unit option, so the unit sits in the widget title
  ("Ø Netzleistung kW, Einspeisung ≤ 0") — accepted by Frigg (2026-10-31).
- **Orientation:** the widget puts days on the x axis and hours on the y axis (fixed by the widget); it still reads as
  hour × day — accepted by Frigg.
- **Site:** bound to the follower `netz` (DS → GridConnection); no rtId pin. A site without a grid connection resolves an
  empty scope and shows the empty state.

### Why not the 5-minute rollup

The first build read `GridConnectionRollup5m` (8,640 rows / 30 days). Measured on test-2 (Firmianstraße, Studio,
3 runs each): the stream-data request took **5.3 / 6.3 / 4.9 s** when replayed alone (median 5.3 s; server first byte
0.7–1.3 s) and **5.8–7.0 s** inside "Refresh All". The response was **3.5 MB of uncompressed JSON** for 7,487 rows
(~486 B/row: rtId, ckTypeId, timestamp, creation/change dates and a cells connection); the download, not the server,
was the time. An hour × day heatmap needs one row per hour, so it reads the 1-hour rollup (720 rows / 30 days).

### Measured on test-2 (2026-10-31, Firmianstraße, 1-hour rollup)

Studio in Chrome, timing fields only, 3 runs each:

| Measurement | Run 1 | Run 2 | Run 3 | Median |
|-------------|-------|-------|-------|--------|
| "Refresh All Widgets" click → heatmap rendered | 0.80 s | 1.11 s | 1.30 s | **1.11 s** |
| its stream-data request (inside the refresh) | 0.70 s | 1.01 s | 1.22 s | 1.01 s |
| same request replayed alone | 0.76 s | 0.62 s | 0.65 s | **0.65 s** |

Response 303 KB, 636 hour rows (the archive has gaps), client render after the response 77–87 ms. Picking the site in
the `standort` picker re-renders the heatmap with all 636 cells in 2.8 s, while all seven widgets reload.

Spot checks (cell = Σ sum / Σ count of the 1-h rollup; reference = AVG(ActivePower) of the raw archive over the exact
hour [from, to), same rtId):

| Cell (Europe/Vienna) | UTC hour | Raw AVG (rows) | Rollup Σsum/Σcount | Heatmap label |
|----------------------|----------|----------------|--------------------|---------------|
| 28 Oct 03:00 (night) | 02:00Z | 1.249690 (697) | 871.034 / 697 = 1.249690 | 1,25 |
| 28 Oct 12:00 (midday) | 11:00Z | 0.291289 (707) | 205.941 / 707 = 0.291289 | 0,29 |
| 28 Oct 19:00 (evening) | 18:00Z | 1.165856 (701) | 817.265 / 701 = 1.165856 | 1,17 |
| 25 Oct 02:00 (CEST) | 00:00Z | 1.224624 (700) | 857.237 / 700 = 1.224624 | 1,22 |

All values match exactly (0 % deviation). The labels are rounded to `decimalPlaces: 2`.

### DST day 2026-10-25 (25-hour day)

The widget gives the repeated wall-clock hour its own row **"02:00 (2)"** (AB#7956, verified on the Beschattung board).
On test-2 the Firmianstraße grid archive has **no rows from 2026-10-25T00:59:58Z to 02:00:03Z** — exactly the repeated
hour 02:00–03:00 CET — while the hour before (02:00–03:00 CEST, 00:00–01:00Z) has the normal 700 rows. So the source
lost that hour; the heatmap shows the normal 02:00 cell for 25 Oct and no "02:00 (2)" row (the row appears only when
data falls into it). Nothing is dropped or moved to another day by the widget. Source-data gap: Bug AB#7968. Accepted as
the documented behaviour of this AC (Frigg, 2026-10-31).

### Fusch on test-2

The Fusch GridConnection `…0a03` has **0 rows** in the GridConnectionArchive on test-2 (the Fusch live flow runs on
prod-1 only), so on test-2 the heatmap shows the empty state for Fusch; the cells for Fusch come from prod-1 data.

### Rollout

New entities in `rt-meshboards-energyiq.yaml`: rollup `…67e6b` (`GridConnectionRollup1h`), query `…67e6a`, widget
`…67e83`; the Energie board description mentions the heatmap. After the import: `ActivateArchive -id
6a4aa4cc22664f2b26c67e6b` (also in `scripts/om_importrt.ps1`) and `BackfillRollup -id 6a4aa4cc22664f2b26c67e6b` for
the existing history (otherwise the heatmap fills up hour by hour from the activation on).
