# Changelog

## Unreleased

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
