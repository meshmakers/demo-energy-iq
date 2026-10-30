# Changelog

## Unreleased

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
