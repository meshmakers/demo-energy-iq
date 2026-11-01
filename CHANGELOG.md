# Changelog

## Unreleased

- AB#7996: **EnergyIQ.Base 1.0.1** fixes the install of 1.0.0, which failed with "RtCkTypeId 'System/Query' not found"
  on a fresh test-2 tenant: the `_trees` query used the former abstract `System/Query` type, which no longer exists in
  System 2.8.0 (the entity was missing on energyiq for the same reason). It is now a `System/SimpleRtQuery` with
  the same rtId (`seed-data/queries/_trees.yaml`); the seed check no longer allows `System/Query`. Do not install 1.0.0.

- AB#7996: **Blueprint `EnergyIQ.Base-1.0.0`.** The product content - 19 RawArchives, 14 RollupArchives, 5 MeshBoards
  (37 widgets, 33 queries), the `_trees` query and the tree navigation - ships as a blueprint
  (`src/blueprints/EnergyIQ.Base/`), published by CI after the CK step (`validate-and-publish-blueprints`: main ->
  private catalog, `r*` -> private + public, feature branches validate only). The seed files moved with `git mv`
  from `data/_general/` and `data/_queries/` into `seed-data/{archives,ui,queries}/` - one copy, rtIds unchanged, so an
  install adopts existing ImportRt content; `scripts/om_importrt.ps1` imports them from there. Dependencies:
  `EnergyIQ-[2.15.0,3.0)`, `Basic-[2.5.0,3.0)`, `Basic.Energy-[1.10.0,2.0)`, System/StreamData/UI 2.3. No pipelines,
  no site data, no adapters/configurations/secrets; the new CI check `scripts/check_energyiq_blueprint_seed.py`
  enforces it. Archive activation stays manual until AB#7991; column/rollup changes through the blueprint wait for
  AB#7997/AB#8002 and AB#7992/AB#7993. Do not install on a tenant whose active archives lack the AB#7925 computed columns (energyiq on test-2/prod-1)
  until AB#7997 + AB#8002.
  staging/prod only from an `r*` tag with Gerald's go per cluster.

- AB#7925: **Energiebilanz kWh tiles show the real energy for every site.** The six tiles are stream-data **Integral**
  queries (AggregationType 7, kW × h = kWh, `valueMultiplier: 1`) instead of SUM × 10/3600, which was right only for
  10-s simulator rows (live: grid about 2x, PV up to 3.7x, battery 7x too high). New computed archive columns (AB#4189):
  `ImportPower`/`ExportPower` on GridConnectionArchive, `ChargePower`/`DischargePower` on BatteryStorageArchive, plus
  the ingested column `DischargingPower` (live Loxone batteries write discharge separately). Einspeisung now reads the
  grid meter (selector `netz`) instead of the simulator-only PV column `GridFeedIn`; Eigenverbrauch is a formula KPI
  `max(0, PV − Einspeisung)`; query `…ec5` (SelfConsumption) is removed. KPIs publish `pvEnergyKwh`, `gridExportKwh`,
  `gridImportKwh`, `batteryChargeKwh`, `batteryDischargeKwh` for the AB#7943 gauges. Tile labels follow the wireframe
  (PV-Erzeugung, Einspeisung). `SelfConsumption`/`GridFeedIn` stay, documented as simulator-only. Activated archives
  get the columns with `AddComputedColumn` (not by re-import); "Batterie entladen" stays 0 for Firmianstraße on
  existing tenants until an ingested column can be added to an active archive (AB#7997). Existing tenants also need the
  old sign filters of `…ec7`–`…ec9` cleared once (AB#8005). Formulas, cross-check and rollout:
  `docs/meshboard-site-selector-fit.md`, "Energiebilanz kWh via Integral (AB#7925)".
- AB#7947: **EnergyIQ 2.15.0** adds two optional `Site` attributes for the site comparison (cost and CO2):
  `EnergyPrice` (Double, EUR/kWh net, energy component only — no VAT, grid fees, taxes or feed-in revenue) and
  `Co2Factor` (Double, kg CO2-eq/kWh), in the new `attributes/tariff.yaml`, both `ownership: TenantOwned` (a seed re-import or blueprint re-apply keeps
  the site admin's value; the seed value is the initial value only). No `defaultValues`: an empty attribute
  means the documented default (0.1600 EUR/kWh net = Eurostat household band DC `nrg_pc_204_c`, Austria 2025, energy and supply component, a demo reference;
  0.152 kg/kWh = Umweltbundesamt REP-0989, Austrian supply mix), see `docs/construction-kit.md`. Additive minor
  bump, no migration; dependencies resolve to `Basic-2.5.0`, `Basic.Energy-1.10.0`, `System-2.7.0` as for 2.14.0.
  RT dependency headers of both seed files now `EnergyIQ-[2.15,3.0)`.
  Demo seed values on the two Sites (Firmianstraße 0.2345, Fusch 0.1987 EUR/kWh net, both 0.152 kg/kWh).
  Rollout order: CK 2.15.0 first (catalog via CI, then update the tenant's EnergyIQ), then import
  `rt-firmianstrasse.yaml` / `rt-fusch.yaml`. test-2 via CI; staging/prod only via blueprint with Gerald's go.

- AB#7942: Energie board gets the heatmap **"Netzbezug – Stunde × Tag"**: the mean net grid power (kW) of the picked
  site per Europe/Vienna hour × day over its own window of the last 30 days (badge "Last 30 days", independent of the
  board Zeitraum), bound to the follower `netz`. The cell is `ActivePower` (signed: feed-in hours show ≤ 0), because
  `ImportPower` (AB#7925) is not live; no `ActivePower > 0` filter, because averaging only positive rows overstates the
  hour. New 1-hour rollup **`GridConnectionRollup1h`** (`…67e6b`, AVG → sum/count on GridConnectionArchive) read with
  `weightedAvg` (Σ sum / Σ count = exact hourly mean), `timestampIsBucketEnd` + `bucketWidthMinutes: 60`; query `…67e6a`
  (sorted on `window_start`), widget `…67e83`. The 5-min rollup took 5–6 s on test-2 (3.5 MB uncompressed). Needs the
  octo-meshboard heatmap options of AB#7956. Rollout: import, `ActivateArchive` + `BackfillRollup` of `…67e6b`. Unit in
  the title (the legend has no unit option). Details, DST day and the Fusch test-2 gap:
  `docs/meshboard-site-selector-fit.md` Part 4.
- AB#7693: Beschattung & Beleuchtung drops the room pickers "Raum (Licht)" / "Raum (Beschattung)" (they did not
  follow the site and showed one room). The two per-room line charts become site-wide heatmaps: light switches and
  blind movements per day x hour (count of change rows), scoped by the `leuchten` / `raffstores` followers.
- AB#7888: **EnergyIQ 2.14.0** adds the association `EquipmentMeters`: `Meter` → `TechnicalSystem`, outbound
  `MeteredEquipment` (ZeroOrOne), inbound `MeteredBy` (N) — the IFC assignment of a meter to the product it measures,
  declared once on `Meter` (2.8.0 rule). `rt-firmianstrasse.yaml` links the Stromzähler Wärmepumpe (`…3007a8`) to the
  HeatPump (`…60f1`). On **Heizung & Kühlung** the five heat-pump meter widgets (queries `…e206`–`…e20a`) drop the pin
  `…3007a8` and bind the new follower `wpzaehler` (Site → Building → BuildingStorey → Space → HeatPump → Meter via
  `MeteredBy`, 5 hops); titles back to "Wärmepumpe – …". Fusch has no heat pump: those widgets show the empty state.
  Rollout order: CK 2.14.0 first, then the RT files. `ImportRt -r` keeps omitted attributes, so the removed
  `RtIds` pins must be cleared once by GraphQL on existing tenants (steps in the PR).
- AB#7693: Beschattung & Beleuchtung, the four bar charts (Einschaltquote, Schaltvorgänge, Ø Beschattung, Brenndauer)
  showed "Widget not configured": their configs had only `valueField`, which the bar chart does not accept. Three use a
  static `series` entry now. Brenndauer uses dynamic mode (`seriesGroupField: name`, `stackedBar`), because `valueScale`
  (ms → h) applies only there (platform bug AB#7923).
- AB#7692: Energie board, table "Aktuelle Leistungen": column title "Wirkleistung (kW)" instead of "(W)".
  `ActivePower` is kW in EnergyIQ; the values were right, only the label was wrong.
- AB#7693: MeshBoards **Raumtemperaturen**, **Heizung & Kühlung** and **Beschattung & Beleuchtung** get the visible
  site selector "Standort" on `EnergyIQ/Site` (default Firmianstraße). Hidden followers walk Site → Building →
  BuildingStorey → Space to the room sensors, heat pump, storage tanks, heating pump, luminaires and blinds, and Site →
  ExternalSpace to the outdoor sensor; the tables, the outdoor and plant charts and the four light/blind bar charts
  (grouping queries) are bound to them. Pins removed: outdoor sensor `…10051a1`, Warmwasserspeicher `…64f5`. The site-scoped
  widgets need storeys at every site (Fusch: AB#7889); Fusch has no heat pump, tanks, pump or outdoor sensor, so those
  widgets show the empty state there. Open: the five heat-pump meter tiles keep the Firmianstraße
  meter pin `…3007a8` and are titled "Wärmepumpe Firmianstraße – …" until AB#7888; the room pickers are not limited to
  the picked site (platform gap). Firmianstraße changes: "Aktuelle Raumtemperaturen" lists the 18 room sensors only, the
  Warmwasserspeicher chart shows the Heizpuffer as an empty legend entry.
- AB#7889: Fusch rooms sit in the storeys UG/EG/OG (`…0a70`-`…0a72`, elevations approximate, room assignment confirmed
  by Gerald), like Firmianstraße, so both sites have the same depth Building → BuildingStorey → Space for the room
  boards (AB#7693). Runbook section 8.3.
- AB#7692: MeshBoards **Energie** and **Energiebilanz** get one visible site selector "Standort" (electrical
  DistributionSystems, picker filter `systemType = ELECTRICAL`, default Firmianstraße). Hidden followers resolve the pick
  to the grid connection, PV system, battery (two-hop path through the PV system), wallboxes, appliances and meters; every
  widget, including the runtime table "Aktuelle Leistungen" and all six Energiebilanz KPIs, is bound to its follower. A
  site without e.g. a battery shows the empty state. Firmianstraße figures no longer include Fusch rows (PV KPI, meter
  table). Requires a Studio whose octo-frontend-libraries support follower selectors, multi-hop scopes, picker
  filters and selector-bound runtime queries (AB#7719). The Energiebilanz kWh factor 10/3600 was measured against prod-1
  rows: it holds for no live site (grid rows every 5 s, PV/battery on change; kWh mostly too high) — documented in
  `docs/meshboard-site-selector-fit.md`, board description and YAML header; a correct figure needs a platform energy
  aggregation.
- AB#7697: Fusch rooms and devices in `data/bim/rt-fusch.yaml` (rtIds `6a10f05c0000000000000a20` to `…a69`), only what
  the Miniserver inventory lists: 11 Spaces (Loxone rooms) under the Fusch building, 5 room TemperatureSensors, the heat
  mixer as Valve (Mixing) with its flow TemperatureSensor, 6 ShadingDevices (blinds) and 10 Luminaires (light
  controllers); same types and roles as Firmianstraße, names end in "Fusch", EnergyIQ stays 2.13.0. The Fusch live data
  flow (`…0d03`) now also maps 22 Miniserver states read only onto them (room and mixer `tempActual`, blind `position`
  × 100, light `activeMoodsNum` → IsOn/DimmingLevel by the Firmianstraße rules) and into the TemperatureSensor,
  Luminaire and ShadingDevice archives; watch list 6 → 28 states, the 6 PV mappings unchanged. Sample extended to 29
  states. Runbook section 8: prod-1 import, redeploy, checks and rollback.
- AB#7696: Fusch Loxone live data flow rolled out on prod-1 (2026-10-30); runbook warning about `Configure State Sync`
  removed, as AB#7741 fixed it.
- AB#7741: Firmianstraße `Configure State Sync` (`67f0b00ec4ff02221e0b6009`) now loads its LoxoneConfiguration by
  well-known name `LoxoneMiniserverConfig` instead of the first configuration with a host. With the Fusch
  configuration in the same tenant (AB#7695) it could otherwise overwrite the Fusch watch list. No other pipeline used
  the "first of type" pattern.
- AB#7696: DataFlow `Fusch Loxone PV live` (`…0d03`-`…0d06` in `data/_pipelines/rt-pipelines-loxone-fusch.yaml`): PV
  string power/energy and the PV system total from the Fusch Miniserver (WebSocket, read only) onto PVString `…0a13` /
  `…0a14` and PhotovoltaicSystem `…0a12`, power rows into `PVStringArchive` and `PhotovoltaicSystemArchive`. Fixed state
  UUIDs and target rtIds (site-safe, no DataPointMappings); grid power not written (SHRDZM stays its only writer).
  Runbook section 7. Nothing is deployed by this change.
- AB#7695: Fusch Loxone inventory `docs/fusch-loxone-inventory.md` from the first discovery run on prod-1 (23 rooms,
  46 categories, 68 controls: PV, grid, wall connector, battery, rooms, heating/cooling, shading, lighting, proposed
  EnergyIQ targets; Loxone grid power = SHRDZM quantity, so not mapped) and the constructed PV state sample
  `data/samples/loxone-fusch-states.json`. Miniserver host corrected to `192.168.20.4`. Runbook: credentials are set in
  Refinery Studio on the configuration entity (no Vault), host changes need `DeployDataFlow`, reading the execution
  output via debug capture.
- AB#7695: Fusch Loxone edge adapter `Fusch Loxone Edge Adapter` (`6a10f05c0000000000000b04`, chart `octo-loxone-adapter`, hosted by
  the existing site `Fusch Edge`), LoxoneConfiguration `FuschLoxoneMiniserverConfig` (`…0b03`, host `192.168.20.10` (corrected to `192.168.20.4` later), user and
  password set at rollout, placeholders only in the repository) and the read-only DataFlow `Fusch Loxone discovery`
  (`…0d01`/`…0d02`: Miniserver structure without the identity block as execution result). Runbook for prod-1:
  `docs/fusch-loxone-runbook.md`. Nothing is deployed by this change.
- AB#7694: Fusch has the same electrical model as Firmianstraße in `data/bim/rt-fusch.yaml`: Building, DistributionSystem
  `Elektrisch Fusch`, PhotovoltaicSystem `PV-Anlage Fusch` with the strings `PV Dach String Süd` / `PV Dach String Nord`,
  ExternalSpace `Stellplatz Fusch` and ChargingStation `Tesla Wall Connector Fusch` (rtIds `6a10f05c0000000000000a10` to
  `…a16`). Existing Fusch entities unchanged; EnergyIQ stays 2.13.0.
