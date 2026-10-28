# MeshBoards: can one site selector drive every widget? (AB#7692)

Analysis of the five boards in `data/_general/rt-meshboards-energyiq.yaml` against the
octo-meshboard entity selector (`EntitySelectorConfig`, `EntitySelectorChildScope`,
`PersistentQueryDataSource.entitySelectorId`). Result: **not with data configuration alone**.
The boards therefore keep their current selectors; see "Platform gaps" for what is missing.

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

## Energiebilanz kWh = SUM(kW) x 10/3600

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

## Done in this change

The Energiebilanz KPI "Netzbezug" summed the shared GridConnectionArchive including the Fusch rows
(AB#7511). It is now scoped by a hidden selector `netz` (DistributionSystem, default Firmianstraße,
childScope GridConnection) like the Energie line chart. Other KPIs are unaffected (their archives
hold only Firmianstraße rows).
