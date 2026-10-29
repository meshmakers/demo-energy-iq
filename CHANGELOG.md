# Changelog

## Unreleased

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
