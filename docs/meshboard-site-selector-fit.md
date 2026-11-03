# MeshBoards: can one site selector drive every widget? (AB#7692)

Analysis of the five boards in `data/_general/rt-meshboards-energyiq.yaml` (since AB#7996 `src/blueprints/EnergyIQ.Base/seed-data/ui/rt-meshboards-energyiq.yaml`) against the
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
| Tagesprofil (AB#7944) | 5 SimpleSdQuery series (PV, grid, battery archives) + 3 formula series | PV, GridConnection, BatteryStorage rtIds | yes since AB#7951: each query series follows its own selector (`pv`, `netz`, `batterie`); see Part 5 |

A polymorphic scope (`childScope` target `EnergyIQ/Meter`, base of GridConnection, ChargingStation,
Appliance) would fan out rtIds that have no rows in the queried per-type archive and produce ghost
legend entries (see the header of the YAML); PhotovoltaicSystem and BatteryStorage are not Meters.

### Energiebilanz (6a4aaf1922664f2b26c67ecc)

| Widget | Query (family) | Needs | One selector? |
|--------|----------------|-------|---------------|
| PV-Produktion, Eigenverbrauch, Netzeinspeisung (3 KPIs) | AggregationSdQuery, PhotovoltaicSystemArchive | PhotovoltaicSystem rtIds | no: F1/F2, F4 (a site without PV would show Firmianstraße values) |
| Netzbezug | AggregationSdQuery, GridConnectionArchive | GridConnection rtIds | partly: scoped by hidden selector `netz` (this PR), not by a visible one |
| Batterie geladen / entladen (2 KPIs) | AggregationSdQuery, BatteryStorageArchive | BatteryStorage rtIds | no: two hops (F6) |

(State of AB#7692 part 1. Since AB#7925 the tiles are Integral queries — Einspeisung reads the GridConnectionArchive
via `netz`, Eigenverbrauch is a formula KPI; see "Energiebilanz kWh via Integral (AB#7925)".)

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

> Superseded by AB#7925 (Integral queries, section "Energiebilanz kWh via Integral (AB#7925)"). Kept as the record
> of why the factor was wrong.

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
| Energiebilanz | PV-Erzeugung | AggregationSdQuery PhotovoltaicSystemArchive Integral `TotalCurrentPower` (AB#7925) | `pv` | Loxone data |
| Energiebilanz | Eigenverbrauch | formula KPI `max(0, PV − Einspeisung)` (AB#7925) | — (board variables) | from PV and grid |
| Energiebilanz | Einspeisung | AggregationSdQuery GridConnectionArchive Integral `ExportPower` (AB#7925) | `netz` | SHRDZM data |
| Energiebilanz | Netzbezug | AggregationSdQuery GridConnectionArchive Integral `ImportPower` (AB#7925) | `netz` | SHRDZM data |
| Energiebilanz | Batterie geladen | AggregationSdQuery BatteryStorageArchive Integral `ChargePower` (AB#7925) | `batterie` | empty scope → "–" |
| Energiebilanz | Batterie entladen | AggregationSdQuery BatteryStorageArchive Integral `DischargePower` (AB#7925) | `batterie` | empty scope → "–" |

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

## Energiebilanz kWh via Integral (AB#7925, 2026-11-01)

The six kWh tiles are the **step integral** of a power column over the board window `[from, to)` (AggregationSdQuery
column with `AggregationType` 7 = Integral, System 2.8.0, AB#7918/AB#7919). Each row holds until the next row of the
same entity or the window end, the last row before `from` opens the window (at most 35 days back), several entities are
summed. kW × h = kWh, so every KPI reads `<column>_integral` with `valueMultiplier: 1`. The cadence of the source no
longer matters: 5-s grid rows, on-change PV and battery rows and 10-s simulator rows give the same energy for the same
power.

| Tile | Formula | Archive (selector) | Query / widget |
|------|---------|--------------------|----------------|
| PV-Erzeugung | Integral(`TotalCurrentPower`) | PhotovoltaicSystemArchive `…000c` (`pv`) | `…ec4` / `…ecd`, publishes `pvEnergyKwh` |
| Eigenverbrauch | `max(0, $pvEnergyKwh - $gridExportKwh)` (`$name` form since EnergyIQ.Base 1.0.2) | — (formula KPI, AB#7950) | — / `…ece` |
| Einspeisung | Integral(`ExportPower`) | GridConnectionArchive `…0007` (`netz`) | `…ec6` / `…ecf`, publishes `gridExportKwh` |
| Netzbezug | Integral(`ImportPower`) | GridConnectionArchive `…0007` (`netz`) | `…ec7` / `…ed0`, publishes `gridImportKwh` |
| Batterie geladen | Integral(`ChargePower`) | BatteryStorageArchive `…000d` (`batterie`) | `…ec8` / `…ed1`, publishes `batteryChargeKwh` |
| Batterie entladen | Integral(`DischargePower`) | BatteryStorageArchive `…000d` (`batterie`) | `…ec9` / `…ed2`, publishes `batteryDischargeKwh` |

The query `…ec5` (SUM of `SelfConsumption`) is deleted: Eigenverbrauch needs no query. No query or widget of the board
reads `SelfConsumption` or `GridFeedIn` (simulator-only columns, never written by a live site). The published variable
names are meant for the gauges of AB#7943 (Autarkie, Eigenverbrauchsquote) — do not rename them, nor the computed
columns, without updating AB#7943/AB#7944/AB#7948.

**Computed columns (AB#4189)** split the sign into non-negative power columns; formulas use the physical (lower-case)
column names:

| Archive | Column | Formula |
|---------|--------|---------|
| GridConnectionArchive `…0007` | `ImportPower` | `if(activepower > 0, activepower, 0)` |
| GridConnectionArchive `…0007` | `ExportPower` | `if(activepower < 0, -activepower, 0)` |
| BatteryStorageArchive `…000d` | `ChargePower` | `if(chargingpower > 0, chargingpower, 0)` |
| BatteryStorageArchive `…000d` | `DischargePower` | `if(coalesce(chargingpower, 0) < 0, -chargingpower, if(coalesce(dischargingpower, 0) > 0, dischargingpower, 0))` (`coalesce`: NULL operand = 0, AB#8060; AB#7943) |

Never a field filter for the sign: a filter drops rows, and the step integral would carry the last positive value
across an export period. A NULL power counts 0 for its interval; a window without rows yields null.

**Two battery conventions.** The simulator writes a signed `ChargingPower` (− = discharging). Loxone Firmianstraße
writes `ChargingPower` ≥ 0 and a separate `DischargingPower` ≥ 0 (`rt-pipelines-loxone.yaml`, `em-battery` /
`em-battery-discharge`). `DischargePower` covers both, so the seed adds the ingested column `DischargingPower` to the
BatteryStorageArchive. Until AB#8060 (NULL operands in computed-column formulas) an archive activated before that column
existed carried the simulator-only formula `if(chargingpower < 0, -chargingpower, 0)` and "Batterie entladen" showed 0 kWh
for Firmianstraße. Since AB#7943 the seed formula is the `coalesce` form above; on an already activated archive it is
switched with `octo-cli -c UpdateComputedColumnFormula -id 6a0e000000000000000a000d -n DischargePower -f '<formula>'`
(the old values stay readable while the background backfill runs, then the column switches atomically; ~950 000 rows take
about 30-50 minutes on test-2, the existing NULL cells are recomputed by the backfill).

**Empty state and clamp.** A query KPI whose Integral is null (empty window, empty scope — Fusch has no battery) shows
"–" (octo-meshboard AB#7921) and publishes no variable. The formula KPI is "–" while one of its variables is missing, so
Eigenverbrauch is "–" when PV or Einspeisung has no data; it is never NaN, and `max(0, …)` clamps a negative difference
(rounding, PV and grid meter not in sync) to 0. Eigenverbrauch ≤ PV and Eigenverbrauch + Einspeisung = PV hold by
construction whenever Einspeisung ≤ PV.

### Cross-check, full day 2026-10-31 (Europe/Vienna, `[2026-10-30T23:00Z, 2026-10-31T23:00Z)`)

"Platform" = the Integral query on the tenant; "rows" = an independent step integral (LOCF, same rules) computed from the
rows exported with `ExportArchiveData` (carry row from the hour before the window); "old" = the former SUM × 10/3600.

| Site (tenant) | Figure | Rows | Platform | Independent | Old |
|---------------|--------|------|----------|-------------|-----|
| Firmianstraße (test-2) | PV | 9 520 | 23.117 kWh | 23.117 kWh | 71.90 |
| Firmianstraße (test-2) | Netz netto (ActivePower) | 16 823 | 28.805 kWh | 28.805 kWh (29.015 import − 0.210 export) | — |
| Firmianstraße (test-2) | Batterie geladen (ChargingPower) | 5 506 | 7.266 kWh | 7.266 kWh | 13.96 |
| Fusch (prod-1, read-only) | PV | 3 044 | 23.948 kWh | 23.948 kWh | 22.86 |
| Fusch (prod-1, read-only) | Netz netto (ActivePower) | 17 268 | −18.423 kWh | −18.423 kWh (3.539 import − 21.962 export) | — |

Energy balance: Firmianstraße Eigenverbrauch = 23.117 − 0.210 = 22.907 kWh ≤ PV; Fusch 23.948 − 21.962 = 1.986 kWh ≤ PV.
No PV energy counter exists for either site (Fusch `TotalEnergyProduced` reads 0 on prod-1, Firmianstraße has none) and
the Firmianstraße GridConnection has no `ImportedEnergy`/`ExportedEnergy` register, so these figures are checked against
the independent integral of the exported rows (agreement to the last digit shown). Fusch had no grid and no PV rows on
test-2 (since 2026-09-01), so its tiles show "–" there, which is correct; Fusch is checked on prod-1 read-only.

**Fusch against the meter registers (prod-1, read-only, 2026-11-01).** `ImportedEnergy` / `ExportedEnergy` of
GridConnection `6a10f05c0000000000000a03` read at 09:31:34Z and 10:18:14Z (SHRDZM delivers in batches, so the window is
bounded by the last archived reading before each read: `[09:31:25.6Z, 10:18:10.6Z)`, 46.7 min, 554 rows):

| Figure | Register delta | Step integral of the rows | Deviation |
|--------|----------------|---------------------------|-----------|
| Einspeisung (`ExportedEnergy`) | 1.555 kWh | 1.544 kWh | −0.7 % |
| Netzbezug (`ImportedEnergy`) | 0.000 kWh | 0.000 kWh (site exported the whole window) | — |

The platform Integral of `ActivePower` over the read window gives −1.538 kWh, identical to the rows (−1.538). The
window fell in a PV-surplus hour, so it checks the export side; the import side over a 45-min window was measured on
the same meter for AB#7690/AB#7918 (0.929 vs 0.924 kWh, +0.5 %; 0.1976 vs 0.197 kWh, +0.3 %) and is an integration
test of the platform (`StreamDataIntegralIntegrationTests`).

### Rollout notes

- Computed columns on an **activated** archive are added with `octo-cli -c AddComputedColumn` (backfill), not by
  re-importing `rt-archives-energyiq.yaml`. Until AB#8002 the backfill runs inside the request and a large archive
  can be cancelled half-way (on test-2 energyiq the first attempt failed and was rolled back), so add the columns after
  AB#8002 is deployed, one at a time, and check `computedState`. Without the columns the tiles Netzbezug, Einspeisung
  and both battery tiles show "references unknown columns"; PV-Erzeugung works.
- The seed path (archive imported with the computed columns and `DischargingPower`, then `ActivateArchive`) was not run
  in this story: local services predated System 2.8.0 and the local octo-cli logins had expired. It is covered by the
  first EnergyIQ blueprint install on a fresh tenant (AB#7991).
- `ImportRt -r` keeps attributes a file omits — also an empty list: the former sign filters of `…ec7`–`…ec9` stay on a
  tenant that had them (Bug AB#8005; done by hand on test-2, prod-1 needs AB#8005 or the same step with Gerald's go).
  Clear them once:
  `mutation { runtime { systemAggregationSdQuerys { update(entities: [{rtId: "6a4aaddc22664f2b26c67ec7", item: {fieldFilter: []}}, {rtId: "6a4aaddc22664f2b26c67ec8", item: {fieldFilter: []}}, {rtId: "6a4aaddc22664f2b26c67ec9", item: {fieldFilter: []}}]) { rtId } } } }`
- Delete the orphaned query `…ec5` on tenants that have it.
- Staging/prod only through the EnergyIQ blueprint, each step with Gerald's go.

## Energiebilanz gauges: Autarkie and Eigenverbrauchsquote (AB#7943, 2026-11-03)

Two formula gauges (octo-meshboard AB#7950; widgets `…ed3` "Autarkie" and `…ed4` "Eigenverbrauchsquote", row 7, six
columns each, `arc`, scale 0-100, suffix " %") sit below the six kWh tiles of the Energiebilanz board. They are static
widgets with `valueMode: formula` and read the board variables the tiles publish (`pvEnergyKwh`, `gridExportKwh`,
`gridImportKwh`, `batteryChargeKwh`, `batteryDischargeKwh`) - there is **no second set of kWh queries**; window and site
come from the board (time filter, `standort`), so a change of either recomputes both gauges as soon as the tiles
reload. Do not rename the variables; the seed check (`scripts/check_energyiq_blueprint_seed.py`, 5a) fails a formula
widget that references an undefined variable.

| Gauge | Formula (board variables, kWh over the window) |
|-------|-----------------------------------------------|
| Verbrauch (not shown) | `C = Import − Export + PV + Entladen − Geladen` (energy balance of the site) |
| Autarkie | `if(C > 0, round(max(0, min(100, (1 − Import / C) * 100))), 1 / 0)` |
| Eigenverbrauchsquote | `if(PV > 0, round(max(0, min(100, (PV − Export) / PV * 100))), 1 / 0)` |

In the seed the operands are written `$gridImportKwh`, `$gridExportKwh`, `$pvEnergyKwh`, `$batteryDischargeKwh`,
`$batteryChargeKwh`. `round(...)` gives whole percent. `1 / 0` is `Infinity`; a gauge shows a non-finite formula result as
"-" with a tooltip (AB#7950), never 0 and never NaN.

**Edge cases** (evaluated with `expr-eval-fork`, the engine the board uses):

| Case | Operands | Autarkie | Eigenverbrauchsquote |
|------|----------|----------|----------------------|
| PV = 0 (night) | PV 0, Export 0 | by balance | "-" (`1 / 0`) |
| Consumption = 0 | all 0 | "-" | "-" |
| Consumption < 0 (export larger than supply, noise) | Import 1, Export 5 | "-" | — |
| Import > Consumption (measurement noise) | Import 10, others 0 + Export 0 | 0 (clamped, never negative) | — |
| Export > PV (meters out of sync) | PV 1, Export 1.2 | — | 0 (clamped) |
| A referenced variable has no value (empty window, no battery) | — | "-" (pending) | "-" if PV or Export is missing |

**Known gap: sites without battery.** A site without a BatteryStorage (Fusch) resolves an empty `batterie` scope; the two
battery tiles show "-" and publish no variable, and a formula with an unpublished variable is "pending" ("-"). So
*Autarkie* shows "-" for Fusch although the balance is `Import − Export + PV` there. A board-configured variable with
default 0 does not help (a configured variable of the same name wins over the widget variable). It needs a Studio
change (a configured default for an unpublished widget variable, or an option on the query KPI to publish 0 for an empty
scope); *Eigenverbrauchsquote* is not affected (PV and Export only).

### Measured, full day 2026-11-02 (Europe/Vienna, `[2026-11-01T23:00Z, 2026-11-02T23:00Z)`)

"Platform" = the persistent Integral queries `…ec4`/`…ec6`/`…ec7`/`…ec8`/`…ec9` on test-2/energyiq (the values the tiles
show); "rows" = an independent step integral (LOCF, NULL = 0, same sign rules) computed with a script from the rows
exported with `ExportArchiveData`. Neither site has a meter-register history in the archive (the GridConnectionArchive
stores `ActivePower` only, Firmianstraße has no register at all, Fusch `TotalEnergyProduced` reads 0), so a register
delta over a full past day cannot be reconstructed; the register check of AB#7925 (Fusch, 46.7 min, −0.7 %) still
stands, a full-day register check needs a register snapshot at the start of a day.

| Site | Figure | Platform | Rows | Gauge |
|------|--------|----------|------|-------|
| Firmianstraße (test-2) | PV | 11.389 kWh | 11.389 | |
| Firmianstraße (test-2) | Einspeisung | 0.054 kWh | 0.054 | |
| Firmianstraße (test-2) | Netzbezug | 51.658 kWh | 51.658 | |
| Firmianstraße (test-2) | Batterie geladen | 1.268 kWh | 1.268 | |
| Firmianstraße (test-2) | Batterie entladen | 2.388 kWh (`dischargepower__v1_integral`, see Bug AB#8236) | 2.388 | |
| Firmianstraße (test-2) | Verbrauch | 64.112 kWh | 64.112 | |
| Firmianstraße (test-2) | Autarkie | | 19.4 % | **19 %** |
| Firmianstraße (test-2) | Eigenverbrauchsquote | | 99.5 % | **100 %** |
| Fusch (prod-1, read-only) | PV / Einspeisung / Netzbezug | no Integral queries on prod-1 yet | 6.558 / 0.000 / 4.620 kWh | |
| Fusch (prod-1, read-only) | Verbrauch (no battery) | | 11.178 kWh | |
| Fusch (prod-1, read-only) | Autarkie / Eigenverbrauchsquote | | 58.7 % / 100.0 % | "-" (known gap) / **100 %** |

The gauge rows are the formulas above evaluated with `expr-eval-fork` on the platform values (19 / 100 for
Firmianstraße); they equal the hand calculation from the independent rows within 0.5 percentage points.

### DischargePower on test-2 (AB#7943)

`UpdateComputedColumnFormula` with the `coalesce` formula on BatteryStorageArchive `…000d` (backfill job
`6ae98b8db32755804fde7263`, 948 559 rows, 48 min, Completed). Firmianstraße discharged 2.388 kWh on 2026-11-02
(before: 0). Side effect: the changed column is returned as `dischargepower__v1_integral`, which the tile "Batterie
entladen" and the variable `batteryDischargeKwh` do not match - Bug AB#8236. A fresh tenant that installs the blueprint
gets the column with its logical name and is not affected; a second formula change on an activated archive is not
recommended before AB#8236 is fixed.

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

## Part 5: Tagesprofil – womit wird der Verbrauch gedeckt? (AB#7944, 2026-11-01)

Stacked area chart on the Energie board (widget `…e001`, full width at row 19, above the 30-day heatmap, which moved
to row 28): for the board window (pick one day in the time filter) it splits the site's consumption
per time bucket into **direkt aus PV**, **aus Batterie** and **aus Netz**, with **PV-Erzeugung** as a line over the
stack. Wireframe: artboard "Energie – Übersicht", section "Tagesprofil".

### Series and formulas (average kW per bucket)

The line chart computes formula series per time bucket from its other series (octo-meshboard AB#7951 multi-query,
AB#7952 formula series). Formula series cannot use the primary query's series as operands, so every operand is its
own query series; the primary query (PV) is hidden (`primarySeries.visible: false`).

| Key | Legend | Kind | Source (selector) | Shown |
|-----|--------|------|-------------------|-------|
| `PV` | PV-Erzeugung | query, `TotalCurrentPower` | PV-Produktion (Archiv) `…67e60`, PhotovoltaicSystemArchive `…000c` (`pv`) | line over the stack (`stacked: false`) |
| `Import` | Netzbezug | query, `ImportPower` | Netzbezug Leistung (Archiv) `…e002`, GridConnectionArchive `…0007` (`netz`) | hidden operand |
| `Export` | Einspeisung | query, `ExportPower` | Einspeisung Leistung (Archiv) `…e003`, GridConnectionArchive `…0007` (`netz`) | hidden operand |
| `Charge` | Batterie Laden | query, `ChargePower` | Batterie Ladeleistung (Archiv) `…e004`, BatteryStorageArchive `…000d` (`batterie`) | hidden operand |
| `Discharge` | Batterie Entladen | query, `DischargePower` | Batterie Entladeleistung (Archiv) `…e005`, BatteryStorageArchive `…000d` (`batterie`) | hidden operand |
| `PvDirect` | direkt aus PV | formula `max(0, $PV - $Export - $Charge)` | — | stacked (1st) |
| `Battery` | aus Batterie | formula `$Discharge` | — | stacked (2nd) |
| `Grid` | aus Netz | formula `$Import` | — | stacked (3rd) |

Consumption = Grid + Battery + PV direct = Import − Export + PV + Discharge − Charge (energy balance at the site's
grid connection; decided by Frigg on AB#7944, not `min(consumption, PV)`: with that the grid share would no longer be
the grid import when the battery charges from the grid). The four power columns are the computed archive columns of
AB#7925 (table in "Energiebilanz kWh via Integral" above); no second set of kWh queries.

**Clamp rule.** Grid and battery are non-negative by construction (computed columns). PV direct is clamped at 0:
in a bucket where Export + Charge exceed PV (the battery charges from the grid, or PV and grid meter are not in sync)
PV direct is 0 instead of negative, and the stacks then add up to `Import + Discharge`, which is more than the balance
by the clamped amount. The 1 % check counts such buckets separately.

**Gaps, not zeros.** A bucket in which an operand has no value, and a non-finite result, is a gap ("–" in the
tooltip); it never pulls the stack to 0. A query series whose selector scope is empty (no battery at the site) has
no values at all, so every formula that uses it is a gap and its legend entry reads "(no data)".

### Waits for AB#8046 (Studio, predecessor)

The merged line chart (octo-frontend-libraries `f94079d3`) does not yet offer three things the story and the
wireframe need. No workaround in this repo (Frigg, 2026-11-01):

1. **Fusch (no battery):** `Charge`/`Discharge` have an empty scope, so *aus Batterie* and *direkt aus PV* are gaps
   at Fusch instead of 0 and PV direct. Needs a per-series "missing source = 0" option (`missingAsZero`); then
   `Charge`/`Discharge` get it and the AC "battery 0, PV direct + grid add up" holds.
2. **Fixed 15-minute buckets:** the bucket count follows the widget width (about one bucket per 2 px, at least 50),
   so the buckets of one day are narrower than 15 min. 96 (92/100 on DST days) buckets need a fixed, DST-correct
   bucket size.
3. **Colours and the dashed PV line:** colours come from the chart palette and the PV line is solid; the wireframe
   colours (PV #F2C46D, battery #0F766E, grid #2563EB, PV line #8A5300 dashed) need per-series colour and line style.

### Prerequisites and checks

- The grid and battery shares need the computed columns `ImportPower`/`ExportPower` and `ChargePower`/`DischargePower`
  on the active archives of the tenant (AB#8002, AB#7997). Until a column exists its query series fails and every
  formula that uses it is a gap.
- *aus Batterie* for a live Loxone battery (Firmianstraße) needs `DischargePower` with the `DischargingPower` branch
  (AB#7997). With the simulator-only fallback formula `if(chargingpower < 0, -chargingpower, 0)` (see "Two battery
  conventions" above) the battery share shows 0 for Firmianstraße and its discharge lands in no stack.
- Checks per change: `scripts/check_energyiq_blueprint_seed.py`, `octo-bpm -c validate` and `-c validateVersion`.
  The per-bucket 1 % check (stacks vs Import − Export + PV + Discharge − Charge, two sample days) and the test-2
  evidence are attached to the pull request of AB#7944.
