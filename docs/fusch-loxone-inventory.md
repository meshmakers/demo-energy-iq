# Fusch Loxone inventory

Work item: AB#7695 (Feature AB#7690). Source: one read-only run of the discovery pipeline `Browse Fusch Miniserver`
(`6a10f05c0000000000000d02`, DataFlow `6a10f05c0000000000000d01`) on prod-1, tenant `energyiq`, on 2026-10-29
(execution `609f821f-8ec8-438f-8962-7001c2edb9b6`). The pipeline fetches the structure file `LoxAPP3.json` of the
Fusch Loxone Miniserver Gen 2 through the Fusch Loxone edge adapter on `mm-octo-mesh-edge-0003` (see
`docs/fusch-loxone-runbook.md`) and removes the Miniserver identity block before the result is stored.

**Structure: 23 rooms, 46 categories, 68 controls.**

What this inventory contains and what it leaves out (the repository is public):

- Per control: room, category, name, control type, the state names (with the unit where the Loxone structure
  documentation defines it) and the proposed EnergyIQ target or "not mapped" with the reason.
- **No** serial number, MAC address, firmware, project name, user list, credentials or addresses of other devices.
- **Security and access controls** (7: alarm system, smoke/water alarm, door locks, NFC keypads) are counted but
  not listed by name or state; they are out of scope and never mapped.
- **Example values are not part of the structure file** (it holds names and state UUIDs only). The column "example
  value" of the work item is therefore empty here. Live values are **pending**: they will come from the live pipelines
  of AB#7696 once deployed; `data/samples/loxone-fusch-states.json` is a constructed sample of the event shape, not a
  recording. State UUIDs are not listed here; the live pipelines
  take the ones they need from the structure and name them in their own file.
- **Units**: the browse result carries no format strings (`actualFormat`, `totalFormat`), so units are those of the
  Loxone structure documentation for the control type (Meter `actual` kW, `total` kWh; Fronius `prodCurr` kW,
  `prodTotal` kWh; room controller temperatures °C). They match the Firmianstraße Miniserver, where the format strings
  were checked (`actualFormat="%.3fkW"`, `totalFormat="%.1fkWh"`). Confirmation by live Fusch values is **pending**
  (first live run of AB#7696).
  `InfoOnlyAnalog` controls have no unit by type; their unit is marked unknown.
- Configuration states (`jLocked` and the long state lists of room controllers, blinds, lighting, time switches) are
  shortened in the tables; the full state list per control type is in the appendix.

## Answers the follow-up stories depend on

**Rooms and sensors (AB#7697):**

- **Raumtemperaturen: yes.** Five rooms have a room controller (`IRoomControllerV2`) with `tempActual`/`tempTarget`:
  Bad OG, Flur EG, Küche OG, Schlafzimmer, Wohnzimmer. One outdoor temperature (`InfoOnlyAnalog` "Temperatur",
  Außenbereich). The room controller type also exposes `humidityActual` and `co2`; whether a physical sensor delivers
  them is not visible in the structure — the rooms story maps them only if live values arrive.
- **Heizung: yes. Kühlung: no.** Heating: heating mixer (`Heatmixer` "Heizungsmischer", Heizraum), boiler, flow,
  return and water temperatures, burner and heating-circuit pump status, two central climate controllers
  (`ClimateControllerUS` "Zentralheizung", "Strom") and the heating part of the five room controllers. There is a
  category "Kühlung", but no control in it; no cooling device.
- **Beschattung: yes** — 6 blinds (`Jalousie`): Bad OG, Küche OG (Balkontür, Fenster), Schlafzimmer, Wohnzimmer
  (Balkontüre, Fenster), plus a central shading block.
- **Beleuchtung: yes** — 10 lighting controllers (`LightControllerV2`): Bad OG, Flur EG, Flur OG, Flur UG, Garage,
  Küche OG, Schlafzimmer, Stiegenhaus, Vorratskeller, Wohnzimmer, plus a central lighting block.

**Grid power vs. SHRDZM (AB#7696, AB#7511):** the Loxone grid power — Fronius `gridCurr` and Energieflussmonitor
`Gpwr` — is **the same quantity** as the SHRDZM smart meter reading on GridConnection `…0a03` (active power at the
grid connection point). The Loxone pipelines therefore **do not write grid power**; the SHRDZM pipeline stays the only
writer of `…0a03`. "MB-EG-Hausverbrauch" is a different quantity (house consumption) and has no Fusch entity yet.

## Summary per section

| Section | Controls | Proposed for AB#7696 (not deployed) |
|---------|----------|-------------------|
| PV | 8 | 2 string meters → PVString `CurrentPower`/`EnergyProduced`; Fronius → PhotovoltaicSystem `TotalCurrentPower`/`TotalEnergyProduced` |
| Grid / energy manager | 2 | none (grid = SHRDZM; no house-consumption entity) |
| Wall connector | 5 | none (no numeric power; evcc text states and commands only) |
| Battery | 0 | none — **no battery** |
| Rooms with temperature | 7 | none (rooms story AB#7697) |
| Heating / cooling | 16 | none (no Fusch heating entities yet) |
| Shading | 7 | none (rooms story AB#7697) |
| Lighting | 11 | none (rooms story AB#7697) |
| Other (scenes, operating switches) | 5 | none |
| Security / access (not listed) | 7 | never |
| **Total** | **68** | |

## PV

Two string meters ("PV Dach String Süd", "PV Dach String Nord", category Energie) and the Fronius inverter block
("Energiemonitor"). The two `InfoOnlyAnalog` "String Dach … Leistung" duplicate the meters' `actual` without a unit;
the meters are used.

Rule for the PV system total (AB#7696): `PhotovoltaicSystem.TotalCurrentPower` = Fronius `prodCurr` (the inverter's
production power, kW). It is one Loxone total, so no sum over two separately arriving events is needed. It is not
expected to equal the sum of the two string meters exactly (inverter vs. string measurement).

| Room | Category | Control | Type | States [unit] | Proposed EnergyIQ target |
|------|----------|---------|------|---------------|--------------------------|
| Heizraum | Energie | Energiemonitor | Fronius | `prodCurr` [kW], `prodCurrDay` [kWh], `prodCurrMonth` [kWh], `prodCurrYear` [kWh], `prodTotal` [kWh], `consCurr` [kW], `consCurrDay` [kWh], `consTotal` [kWh], `deliveryDay` [kWh], `earningsDay`, `earningsMonth`, `earningsYear`, `earningsTotal`, `prodStatus`, `prodError`, `error`, `gridCurr` [kW], `batteryCurr` [kW], `stateOfCharge` [%], `priceDelivery`, `priceConsumption`, `co2Factor`, `generatorType`, `mode`, `online` | PhotovoltaicSystem `…0a12`: `prodCurr` → `TotalCurrentPower` [kW], `prodTotal` → `TotalEnergyProduced` [kWh] (AB#7696). `gridCurr`: not mapped (same quantity as SHRDZM, see grid section). `batteryCurr`/`stateOfCharge`: not mapped (no battery, see battery section). Day/month/year counters, earnings, prices: not mapped (no EnergyIQ attribute) |
| Heizraum | Energie | PV Dach String Nord | Meter | `actual` [kW], `total` [kWh], `totalDay`, `totalWeek`, `totalMonth`, `totalYear` | PVString `…0a14` "PV Dach String Nord": `actual` → `CurrentPower` [kW], `total` → `EnergyProduced` [kWh] (AB#7696) |
| Heizraum | Energie | PV Dach String Süd | Meter | `actual` [kW], `total` [kWh], `totalDay`, `totalWeek`, `totalMonth`, `totalYear` | PVString `…0a13` "PV Dach String Süd": `actual` → `CurrentPower` [kW], `total` → `EnergyProduced` [kWh] (AB#7696) |
| Heizraum | Photovoltaik | String Dach Süd Leistung | InfoOnlyAnalog | `value`, `error` | not mapped: duplicates the string Meter `actual`; unit unknown (the browse result has no format strings) |
| Heizraum | Photovoltaik | String Dach Nord Leistung | InfoOnlyAnalog | `value`, `error` | not mapped: duplicates the string Meter `actual`; unit unknown (the browse result has no format strings) |
| Heizraum | Photovoltaik | String Dach Süd Spannung | InfoOnlyAnalog | `value`, `error` | candidate: PVString `DcVoltage` [V] — not mapped yet, unit to be confirmed from a live value |
| Heizraum | Photovoltaik | String Dach Nord Spannung | InfoOnlyAnalog | `value`, `error` | candidate: PVString `DcVoltage` [V] — not mapped yet, unit to be confirmed from a live value |
| Heizraum | Photovoltaik | Reset PV Zähler | Pushbutton | `active` | not mapped: a command (push button); never written |

## Grid / energy manager

There is no `EnergyManager2` (the Firmianstraße type). The energy flow is shown by an `EFM` block and the Fronius
block above.

| Room | Category | Control | Type | States [unit] | Proposed EnergyIQ target |
|------|----------|---------|------|---------------|--------------------------|
| Heizraum | Energie | Energieflussmonitor | EFM | `Ppwr` [kW], `Gpwr` [kW], `Spwr` [kW], `selfConsumption`, `storageChargeFromProduction`, `Pre`, `Pri`, `CO2`, `totalSelfConsumption`, `totalStorageChargeFromProduction`, `actual0`, `actual1`, `actual2` | not mapped: `Gpwr` = grid power (same quantity as SHRDZM); `Ppwr` = PV power (same as Fronius `prodCurr`, which is used instead); `Spwr` = storage power (no battery); sub-meter "Rest" (Meter, `actual`/`total`/`totalNeg`): not mapped, no matching Fusch entity |
| Stiegenhaus | Energie | MB-EG-Hausverbrauch | Meter | `actual` [kW], `total` [kWh], `totalNeg` [kWh], `totalDay`, `totalWeek`, `totalMonth`, `totalYear`, `totalNegDay`, `totalNegWeek`, `totalNegMonth`, `totalNegYear` | not mapped: house consumption meter (bidirectional, `totalNeg`); no Fusch Meter entity for house consumption exists — candidate for a later story (EnergyIQ `Meter` `ActivePower`/`ImportedEnergy`/`ExportedEnergy`) |

## Wall connector

There is **no `Wallbox2` control** and no numeric charging power in the structure. The Tesla Wall Connector is
represented only through evcc text states and evcc command controls (category Energie, Garage). ChargingStation
`…0a16` stays without live data; a numeric source (evcc API, wall connector API) is a separate decision.

| Room | Category | Control | Type | States [unit] | Proposed EnergyIQ target |
|------|----------|---------|------|---------------|--------------------------|
| Garage | Energie | Ladestation | TextState | `textAndIcon`, `iconAndColor` | not mapped: text state (no numeric power); ChargingStation `…0a16` stays without live data |
| Garage | Energie | Ladeziel | Slider | `value`, `error` | not mapped: a command control (evcc charge mode/target); never written |
| Garage | Energie | Sofort laden | Switch | `active`, `lockedOn` | not mapped: a command control (evcc charge mode/target); never written |
| Garage | Energie | evcc Ladestrom | TextState | `textAndIcon`, `iconAndColor` | not mapped: text state (no numeric power); ChargingStation `…0a16` stays without live data |
| Garage | Energie | evcc Status | TextState | `textAndIcon`, `iconAndColor` | not mapped: text state (no numeric power); ChargingStation `…0a16` stays without live data |

## Battery

**No.** There is no battery control. The Fronius and EFM blocks expose battery states (`batteryCurr`,
`stateOfCharge`, `Spwr`) by type, independent of installed hardware; the Fusch model has no `BatteryStorage` and none
is mapped.

## Rooms with temperature

| Room | Category | Control | Type | States [unit] | Proposed EnergyIQ target |
|------|----------|---------|------|---------------|--------------------------|
| Außenbereich | Wetter | Temperatur | InfoOnlyAnalog | `value` | rooms story: outdoor `TemperatureSensor.CurrentValue` on an ExternalSpace; unit to be confirmed from a live value |
| Außenbereich | Wetter | Windgeschwindigkeit | InfoOnlyAnalog | `value` | not mapped: no EnergyIQ type for wind speed |
| Bad OG | Klima | Bad OG | IRoomControllerV2 | `tempActual` [°C], `tempTarget` [°C], `humidityActual` [%], `co2` [ppm], `openWindow` (+28 more, see appendix) | rooms story AB#7697: Space + `TemperatureSensor.CurrentValue` (`tempActual`), Space `thermalRequirements.spaceTemperature` (`tempTarget`); `humidityActual`/`co2` only if a physical sensor delivers values (the block exposes them by type) |
| Flur EG | Klima | Flur EG | IRoomControllerV2 | `tempActual` [°C], `tempTarget` [°C], `humidityActual` [%], `co2` [ppm], `openWindow` (+28 more, see appendix) | rooms story AB#7697: Space + `TemperatureSensor.CurrentValue` (`tempActual`), Space `thermalRequirements.spaceTemperature` (`tempTarget`); `humidityActual`/`co2` only if a physical sensor delivers values (the block exposes them by type) |
| Küche OG | Klima | Küche OG | IRoomControllerV2 | `tempActual` [°C], `tempTarget` [°C], `humidityActual` [%], `co2` [ppm], `openWindow` (+28 more, see appendix) | rooms story AB#7697: Space + `TemperatureSensor.CurrentValue` (`tempActual`), Space `thermalRequirements.spaceTemperature` (`tempTarget`); `humidityActual`/`co2` only if a physical sensor delivers values (the block exposes them by type) |
| Schlafzimmer | Klima | Schlafzimmer | IRoomControllerV2 | `tempActual` [°C], `tempTarget` [°C], `humidityActual` [%], `co2` [ppm], `openWindow` (+28 more, see appendix) | rooms story AB#7697: Space + `TemperatureSensor.CurrentValue` (`tempActual`), Space `thermalRequirements.spaceTemperature` (`tempTarget`); `humidityActual`/`co2` only if a physical sensor delivers values (the block exposes them by type) |
| Wohnzimmer | Klima | Wohnzimmer | IRoomControllerV2 | `tempActual` [°C], `tempTarget` [°C], `humidityActual` [%], `co2` [ppm], `openWindow` (+28 more, see appendix) | rooms story AB#7697: Space + `TemperatureSensor.CurrentValue` (`tempActual`), Space `thermalRequirements.spaceTemperature` (`tempTarget`); `humidityActual`/`co2` only if a physical sensor delivers values (the block exposes them by type) |

## Heating / cooling

| Room | Category | Control | Type | States [unit] | Proposed EnergyIQ target |
|------|----------|---------|------|---------------|--------------------------|
| Bad OG | Heizung | Schaltuhr | Daytimer | `value`, `mode` (+5 more, see appendix) | not mapped: time switch (schedule), a setting |
| Bad OG | Lüftung | Schaltuhr | Daytimer | `value`, `mode` (+5 more, see appendix) | not mapped: time switch (schedule), a setting |
| Heizraum | Heizung | Boost | Switch | `active`, `lockedOn` | heating/cooling story: not mapped now (no Fusch heating plant entity); switches/time switches/value selectors are commands or settings, never written |
| Heizraum | Heizung | Brenner aktiv | InfoOnlyDigital | `active` | heating/cooling story: not mapped now (no Fusch heating plant entity); switches/time switches/value selectors are commands or settings, never written |
| Heizraum | Heizung | Brenner verwenden | Switch | `active`, `lockedOn` | heating/cooling story: not mapped now (no Fusch heating plant entity); switches/time switches/value selectors are commands or settings, never written |
| Heizraum | Heizung | Wasserpumpe Heizkreislauf | InfoOnlyDigital | `active` | heating/cooling story: not mapped now (no Fusch heating plant entity); switches/time switches/value selectors are commands or settings, never written |
| Heizraum | Heizung | Tiefschlaf Zeitschaltuhr Heizung | Daytimer | `value`, `mode` (+5 more, see appendix) | heating/cooling story: not mapped now (no Fusch heating plant entity); switches/time switches/value selectors are commands or settings, never written |
| Heizraum | Heizung | Wasser | Daytimer | `value`, `mode` (+5 more, see appendix) | heating/cooling story: not mapped now (no Fusch heating plant entity); switches/time switches/value selectors are commands or settings, never written |
| Heizraum | Heizung | Wassertemperatur | ValueSelector | `min`, `max`, `step`, `value` | heating/cooling story: not mapped now (no Fusch heating plant entity); switches/time switches/value selectors are commands or settings, never written |
| Heizraum | Temperatur | Heizungsmischer | Heatmixer | `tempActual` [°C], `tempTarget` [°C] | heating/cooling story: no EnergyIQ heating plant for Fusch yet (candidate `Boiler`/`Pump` + temperature sensors); not mapped now |
| Heizraum | Temperatur | Kesseltemperatur | InfoOnlyAnalog | `value`, `error` | heating/cooling story: not mapped now (no Fusch heating plant entity); switches/time switches/value selectors are commands or settings, never written |
| Heizraum | Temperatur | Rücklauftemperatur | InfoOnlyAnalog | `value`, `error` | heating/cooling story: not mapped now (no Fusch heating plant entity); switches/time switches/value selectors are commands or settings, never written |
| Heizraum | Temperatur | Vorlauftemperatur | InfoOnlyAnalog | `value`, `error` | heating/cooling story: not mapped now (no Fusch heating plant entity); switches/time switches/value selectors are commands or settings, never written |
| Heizraum | Temperatur | Wassertemperatur | InfoOnlyAnalog | `value`, `error` | heating/cooling story: not mapped now (no Fusch heating plant entity); switches/time switches/value selectors are commands or settings, never written |
| Zentral | Klima | Strom | ClimateControllerUS | `mode`, `currentStatus`, `demandHeat`, `demandCool`, `actualOutdoorTemp` (+17 more, see appendix) | not mapped: central climate controller (mode/demand); heating/cooling story decides |
| Zentral | Klima | Zentralheizung | ClimateControllerUS | `mode`, `currentStatus`, `demandHeat`, `demandCool`, `actualOutdoorTemp` (+17 more, see appendix) | not mapped: central climate controller (mode/demand); heating/cooling story decides |

## Shading

| Room | Category | Control | Type | States [unit] | Proposed EnergyIQ target |
|------|----------|---------|------|---------------|--------------------------|
| Bad OG | Beschattung | Automatikjalousie | Jalousie | `position` [0..1], `shadePosition` [0..1], `targetPosition`, `autoActive` (+11 more, see appendix) | rooms story AB#7697: `ShadingDevice.Position` = `position` × 100 [%] (Firmianstraße rule `jalousie-shading`) |
| Küche OG | Beschattung | Balkontür | Jalousie | `position` [0..1], `shadePosition` [0..1], `targetPosition`, `autoActive` (+11 more, see appendix) | rooms story AB#7697: `ShadingDevice.Position` = `position` × 100 [%] (Firmianstraße rule `jalousie-shading`) |
| Küche OG | Beschattung | Fenster | Jalousie | `position` [0..1], `shadePosition` [0..1], `targetPosition`, `autoActive` (+11 more, see appendix) | rooms story AB#7697: `ShadingDevice.Position` = `position` × 100 [%] (Firmianstraße rule `jalousie-shading`) |
| Schlafzimmer | Beschattung | Automatikjalousie | Jalousie | `position` [0..1], `shadePosition` [0..1], `targetPosition`, `autoActive` (+11 more, see appendix) | rooms story AB#7697: `ShadingDevice.Position` = `position` × 100 [%] (Firmianstraße rule `jalousie-shading`) |
| Wohnzimmer | Beschattung | Balkontüre | Jalousie | `position` [0..1], `shadePosition` [0..1], `targetPosition`, `autoActive` (+11 more, see appendix) | rooms story AB#7697: `ShadingDevice.Position` = `position` × 100 [%] (Firmianstraße rule `jalousie-shading`) |
| Wohnzimmer | Beschattung | Fenster | Jalousie | `position` [0..1], `shadePosition` [0..1], `targetPosition`, `autoActive` (+11 more, see appendix) | rooms story AB#7697: `ShadingDevice.Position` = `position` × 100 [%] (Firmianstraße rule `jalousie-shading`) |
| Zentral | Beschattung | Automatikbeschattung Zentral | CentralJalousie | `events`, `safetyActive` | not mapped: central command block |

## Lighting

| Room | Category | Control | Type | States [unit] | Proposed EnergyIQ target |
|------|----------|---------|------|---------------|--------------------------|
| Bad OG | Beleuchtung | Bad OG | LightControllerV2 | `activeMoodsNum` [mood id], `presence` (+6 more, see appendix) | rooms story AB#7697: `Luminaire.IsOn` = `activeMoodsNum` <> 778, `DimmingLevel` (Firmianstraße rules `lightcontroller-ison`/`-level`) |
| Flur EG | Beleuchtung | Flur EG | LightControllerV2 | `activeMoodsNum` [mood id], `presence` (+6 more, see appendix) | rooms story AB#7697: `Luminaire.IsOn` = `activeMoodsNum` <> 778, `DimmingLevel` (Firmianstraße rules `lightcontroller-ison`/`-level`) |
| Flur OG | Beleuchtung | Flur OG | LightControllerV2 | `activeMoodsNum` [mood id], `presence` (+6 more, see appendix) | rooms story AB#7697: `Luminaire.IsOn` = `activeMoodsNum` <> 778, `DimmingLevel` (Firmianstraße rules `lightcontroller-ison`/`-level`) |
| Flur UG | Beleuchtung | Flur UG | LightControllerV2 | `activeMoodsNum` [mood id], `presence` (+6 more, see appendix) | rooms story AB#7697: `Luminaire.IsOn` = `activeMoodsNum` <> 778, `DimmingLevel` (Firmianstraße rules `lightcontroller-ison`/`-level`) |
| Garage | Beleuchtung | Garage | LightControllerV2 | `activeMoodsNum` [mood id], `presence` (+6 more, see appendix) | rooms story AB#7697: `Luminaire.IsOn` = `activeMoodsNum` <> 778, `DimmingLevel` (Firmianstraße rules `lightcontroller-ison`/`-level`) |
| Küche OG | Beleuchtung | Küche OG | LightControllerV2 | `activeMoodsNum` [mood id], `presence` (+6 more, see appendix) | rooms story AB#7697: `Luminaire.IsOn` = `activeMoodsNum` <> 778, `DimmingLevel` (Firmianstraße rules `lightcontroller-ison`/`-level`) |
| Schlafzimmer | Beleuchtung | Schlafzimmer | LightControllerV2 | `activeMoodsNum` [mood id], `presence` (+6 more, see appendix) | rooms story AB#7697: `Luminaire.IsOn` = `activeMoodsNum` <> 778, `DimmingLevel` (Firmianstraße rules `lightcontroller-ison`/`-level`) |
| Stiegenhaus | Beleuchtung | Stiegenhaus | LightControllerV2 | `activeMoodsNum` [mood id], `presence` (+6 more, see appendix) | rooms story AB#7697: `Luminaire.IsOn` = `activeMoodsNum` <> 778, `DimmingLevel` (Firmianstraße rules `lightcontroller-ison`/`-level`) |
| Vorratskeller | Beleuchtung | Vorratskeller | LightControllerV2 | `activeMoodsNum` [mood id], `presence` (+6 more, see appendix) | rooms story AB#7697: `Luminaire.IsOn` = `activeMoodsNum` <> 778, `DimmingLevel` (Firmianstraße rules `lightcontroller-ison`/`-level`) |
| Wohnzimmer | Beleuchtung | Wohnzimmer | LightControllerV2 | `activeMoodsNum` [mood id], `presence` (+6 more, see appendix) | rooms story AB#7697: `Luminaire.IsOn` = `activeMoodsNum` <> 778, `DimmingLevel` (Firmianstraße rules `lightcontroller-ison`/`-level`) |
| Zentral | Beleuchtung | Licht Zentral | CentralLightController | `events` | not mapped: central command block |

## Other

Operating switches and scenes, never mapped and never written. The 7 security and access controls are not listed.

| Room | Category | Control | Type | States [unit] | Proposed EnergyIQ target |
|------|----------|---------|------|---------------|--------------------------|
| Zentral | Bedienelemente | Abend | Switch | `active`, `lockedOn` | not mapped: operating switch/scene, no EnergyIQ target |
| Zentral | Bedienelemente | Nacht | Switch | `active`, `lockedOn` | not mapped: operating switch/scene, no EnergyIQ target |
| Zentral | Einstellungen | Alle abwesend | Switch | `active`, `lockedOn` | not mapped: operating switch/scene, no EnergyIQ target |
| Zentral | Einstellungen | Tiefschlaf | Switch | `active`, `lockedOn` | not mapped: operating switch/scene, no EnergyIQ target |
| Zentral | Nicht zugeordnet | Schlafen Wohnzimmer | Switch | `active`, `lockedOn` | not mapped: operating switch/scene, no EnergyIQ target |

## Appendix: full state list per shortened control type

- **LightControllerV2**: `activeMoods`, `moodList`, `activeMoodsNum`, `favoriteMoods`, `additionalMoods`, `circuitNames`, `daylightConfig`, `presence`
- **Jalousie**: `infoText`, `up`, `down`, `position`, `shadePosition`, `safetyActive`, `autoAllowed`, `autoActive`, `locked`, `autoInfoText`, `autoState`, `deviceState`, `targetPosition`, `targetPositionLamelle`, `adjustingEndPos`
- **Daytimer**: `mode`, `override`, `value`, `needsActivation`, `modeList`, `entriesAndDefaultValue`, `resetActive`
- **IRoomControllerV2**: `tempActual`, `tempTarget`, `comfortTemperature`, `comfortTemperatureCool`, `comfortTolerance`, `absentMinOffset`, `absentMaxOffset`, `frostProtectTemperature`, `heatProtectTemperature`, `shadingCoolTemp`, `shadingHeatTemp`, `co2`, `humidityActual`, `actualOutdoorTemp`, `currentMode`, `shadingOut`, `active`, `activeMode`, `comfortTemperatureOffset`, `overrideEntries`, `prepareState`, `useOutdoor`, `operatingMode`, `overrideReason`, `excessEnergyTempOffset`, `averageOutdoorTemp`, `temperatureBoundaryInfo`, `capabilities`, `fan`, `ventMode`, `co2Target`, `airDeviceState`, `openWindow`
- **ClimateControllerUS**: `mode`, `controls`, `currentStatus`, `serviceMode`, `fanTimerUntil`, `modeTimerUntil`, `emergencyTimerUntil`, `demandCool`, `demandHeat`, `stage`, `minimumTempCooling`, `maximumTempHeating`, `actualOutdoorTemp`, `fan`, `emergencyOverride`, `humidity`, `threshold`, `excessEnergy`, `outdoorTempInfo`, `nextMaintenance`, `manualHeating`, `outdoorTempMode`
