# Fusch MQTT site (AB#7575)

The SHRDZM smart meter at the Fusch site (meter id `1KFM0200601435`) publishes its readings to a local MQTT
broker. This runtime model puts the meter into the `energyiq` tenant as an `EnergyIQ/GridConnection` (EnergyIQ
2.13.0 as is, no CK change) and writes its readings through an edge and a mesh pipeline. Activation on prod-1 is
AB#7511; this repository only holds the model.

## Files

| File | Content |
|------|---------|
| `data/bim/rt-fusch.yaml` | Site `Fusch` -> ExternalSpace `Netzanschluss Fusch` (`System/ParentChild`) -> GridConnection `Hauptzähler Fusch` (`EnergyIQ/SpaceElements`), `rtWellKnownName` = meter id |
| `data/_general/rt-adapters-fusch.yaml` | Deployment site `Fusch Edge` (live rtId, see below) and the MQTT edge adapter (Helm chart `octo-mqtt-adapter`, `Hosts` -> the site) |
| `data/_general/rt-mqtt-configuration-fusch.yaml` | `MqttConfiguration` `FuschShrdzmBroker`, **placeholders only** |
| `data/_pipelines/rt-pipelines-fusch-mqtt.yaml` | DataFlow `Fusch smart meter` with the edge and the mesh pipeline |
| `data/samples/shrdzm-fusch.json` | Constructed SHRDZM sample (two messages), used for the checks below |

Not touched: `rt-archives-energyiq.yaml` (the existing `GridConnectionArchive` `6a0e000000000000000a0007` with its one
column `ActivePower` is used as is), the Loxone files and the CK.

## Pipelines

```
Edge adapter (Fusch)                                            Mesh adapter 670000000000000000000002
MqttTrigger@1 (FuschShrdzmBroker, SHRDZM/+/+/sensor, QoS 1)     FromPipelineDataEvent@1
 -> MqttMessageTransform@1 -> ConvertDataType@1 (strings        -> GetRtEntitiesByWellKnownName@1 (meter id, NO insert)
    to Double, ON THE EDGE) -> BufferData@1 (30 s)              -> If modOperation = 1: Math@1 unit mapping, CreateUpdateInfo@1
 -> ToPipelineDataEvent@1 ------------------------------------>    (else Logger@1: reading dropped)
                                                                -> BackfillFromRtEntity@1 + SaveStreamDataInArchive@1
                                                                -> FilterLatestUpdateInfo@1 -> ApplyChanges@2
```

Choice for the conversion: **on the edge** (as in the verified MqttEdgeSample). The edge sends numbers, the mesh pipeline
only does the arithmetic. All readings go to the archive; only the newest reading per meter is written onto the entity.
A reading whose meter id has no `GridConnection` is dropped with a log line; it never creates an entity.

## Mapping OBIS -> attribute -> unit

| OBIS register | Payload key | Unit in the payload | Attribute | Unit in the model | Rule |
|---------------|-------------|---------------------|-----------|-------------------|------|
| 1.7.0 active power import | `1.7.0` | W | `activePower` (+) | kW, signed | (1.7.0 - 2.7.0) / 1000 |
| 2.7.0 active power export | `2.7.0` | W | `activePower` (-) | kW, signed | see above, + import / - export |
| 1.8.0 active energy import | `1.8.0` | **Wh (assumption, see below)** | `importedEnergy` | kWh | / 1000 |
| 2.8.0 active energy export | `2.8.0` | **Wh (assumption, see below)** | `exportedEnergy` | kWh | / 1000 |
| payload `timestamp` | `timestamp` | - | - | - | reference only (`deviceTimestamp`), not used |
| time of the reading | - | - | archive row time | UTC | time at which the edge received the message (`MqttTrigger@1`), taken before the buffer |

Archive: only `ActivePower` (kW) goes to `GridConnectionArchive`. Per-phase voltage and current (32/52/72.7.0,
31/51/71.7.0), reactive energy (3.8.0/4.8.0), 16.7.0 and the uptime are **not** mapped (Feature AB#7574, non-scope).

### Three facts that could not be confirmed from a recording

The real broker credentials are only in Vault and were deliberately not used, so no message of the live meter was
recorded. `data/samples/shrdzm-fusch.json` is **constructed** in the SHRDZM payload format. Three assumptions are to
be confirmed with the first live message in AB#7511 (compare with the display of the meter):

1. **Energy unit of 1.8.0 / 2.8.0**: assumed Wh (like the power registers in W, OBIS standard); the octo-adapter-mqtt
   sample calls it kWh. If the first reading shows kWh, remove the two `Math@1` nodes that divide the energy by 1000
   (comment in the mesh pipeline) and point the `importedEnergy` / `exportedEnergy` updates at `energyImportWh` /
   `energyExportWh`.
2. **Meter id**: the edge pipeline takes it from the payload `id`. If the live payload carries `1KFM0200601435` only in
   the topic (`SHRDZM/<deviceId>/<moduleId>/sensor`), change the path of the `$.reading.meterId` node to
   `$.message.topicParts.deviceId` (or `moduleId`).
3. **Time**: the payload `timestamp` has no time zone designator and its zone could not be checked. The reading is
   therefore stamped with the UTC time of reception on the edge (offset to the publish time: seconds), which is UTC by
   construction and also right for readings that are delivered late from the edge buffer.

## Requirements of the released platform

* System.Communication 4.13.0 on the tenant (`MqttConfiguration`); prod-1/energyiq has 4.12.0 until the 3.5.46 release.
* The edge adapter with `MqttTrigger@1`, `ConvertDataType@1` including the fix of AB#7509 (bracket-quoted keys such as
  `$.message.value['1.8.0']` after a write) and `BufferData@1`.
* `ConvertDataType@1` has no `onMissing` in a release yet (AB#7540, PR 36 of octo-communication-sdk): a register missing
  from the payload becomes 0 with a warning. Once AB#7540 is released, add `onMissing: Skip` to the four register
  conversions.

## The deployment site rtId

The deployment site "Fusch Edge" already exists on prod-1/energyiq (AB#7470) with rtId `6addb8ec0c28d87762b0ad5e`. In
System.Communication 4.x the former `Pool` is the `DeploymentSite`, and the Communication Operator of the edge device
registers against **the rtId of that entity**. Firmianstraße is the same: its live site rtId `67f0a01ec4ff02221e0b5000`
is the one in `rt-adapters-loxone.yaml`. `rt-adapters-fusch.yaml` therefore defines the site with the live rtId, name and
description; ImportRt upserts by rtId, so a re-import neither creates a second "Fusch Edge" nor renames it. The runtime
state of the site and the adapter (`DeploymentState`, `CommunicationState`, `ConfigurationState`) is not set in the files.

## Import and activation (AB#7511)

Prerequisites: release 3.5.46 on prod-1, Gerald's go for prod-1. Context `prod-1_energyiq`, from
`data/` of this repository. All imports use `-r` (upsert) so that they can be repeated.

1. `LibraryStatus`: System.Communication >= 4.13.0, EnergyIQ 2.13.0. Check that the archive `6a0e000000000000000a0007`
   exists and is Activated; `GetDeploymentSites`: note name, description and states of "Fusch Edge".
2. `octo-cli -c ImportRt -f _general/rt-adapters-fusch.yaml -w -r` (site + adapter). `GetDeploymentSites` again: still one
   "Fusch Edge", same rtId, name, description.
3. `octo-cli -c ImportRt -f _general/rt-mqtt-configuration-fusch.yaml -w -r` **once**.
4. Set host, port, user name and `IsSslEnabled` of `FuschShrdzmBroker` and its password from Vault
   (`secret/meshmakers/edge/sites/fusch/mqtt`) through the Secret-aware update (Refinery Studio form, or MCP
   `set_entity_secrets`; never through a file, a command line or a comment). Do not import step 3 again afterwards.
5. `octo-cli -c ImportRt -f bim/rt-fusch.yaml -w -r` (Site, ExternalSpace, GridConnection).
6. `octo-cli -c ImportRt -f _pipelines/rt-pipelines-fusch-mqtt.yaml -w -r` (DataFlow and both pipelines).
7. `octo-cli -c DeployWorkload -id 6a10f05c0000000000000b02` (adapter `FuschMqttAdapter`); wait for Online/Healthy, the pod
   runs on mm-octo-mesh-edge-0003.
8. `octo-cli -c DeployDataFlow --identifier 6a10f05c0000000000000c01`.
9. Check: `GetLatestPipelineExecution --identifier 6a10f05c0000000000000c02` (edge) and `...c03` (mesh) show executions;
   the GridConnection with `rtWellKnownName 1KFM0200601435` carries `activePower`, `importedEnergy`, `exportedEnergy`
   (GraphQL `energyIQGridConnection`), and `GridConnectionArchive` gets rows; then confirm the three assumptions above.
