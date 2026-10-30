# MeshBoards: can one site selector drive every widget? (AB#7692)

Analysis of the five boards in `data/_general/rt-meshboards-energyiq.yaml` against the
octo-meshboard entity selector (`EntitySelectorConfig`, `EntitySelectorChildScope`,
`PersistentQueryDataSource.entitySelectorId`). Result on 2026-10-28: **not with data configuration alone**
(platform gaps below). The gaps were closed in octo-frontend-libraries by Feature AB#7719 (AB#7721-AB#7725,
merged 2026-10-30); Energie and Energiebilanz now have one visible site selector — see
[Part 2: implemented design](#part-2-implemented-design-2026-10-30). The three other boards follow in AB#7693.

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

Validation without a running Studio (test-2 down, local stack not running): the selectors of both boards were run
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
