# Fusch Loxone adapter: rollout runbook (prod-1, tenant energyiq)

Work items: AB#7695 (adapter, configuration, discovery pipeline), AB#7696 (live PV pipelines, section 7), Feature AB#7690.
Decision of 2026-10-28: the Fusch Loxone Miniserver Gen 2 (`192.168.20.4`) is reached **only** through a Loxone
adapter on edge device `mm-octo-mesh-edge-0003`. No tailnet change, no direct HTTP from a developer machine to the
Miniserver, no manual export.

Every step on prod-1 needs Gerald's go in the session that runs it (`agent-operating-rules` §3, §5.1).

**Status (2026-10-29):** steps 1-4 ran on prod-1 / `energyiq` (comments on AB#7695): import 2026-10-28, credentials
set by Gerald in Refinery Studio, adapter `…0b04` Deployed/Online on edge-0003, discovery execution
`609f821f-8ec8-438f-8962-7001c2edb9b6` Succeeded on 2026-10-29 after the host was corrected from `192.168.20.10`
(wrong) to `192.168.20.4`. The inventory (step 5) is `docs/fusch-loxone-inventory.md`. Adapter and data flow stay
deployed; the discovery pipeline runs only on `ExecutePipeline`.
The live configuration entity already has host `192.168.20.4` (set by Gerald); the repository file now matches it,
so no re-import of `rt-loxone-configuration-fusch.yaml` is needed (it stays insert-only).

## What is deployed

| Entity | rtId | File |
|--------|------|------|
| DeploymentSite `Fusch Edge` (exists on prod-1, unchanged) | `6addb8ec0c28d87762b0ad5e` | `data/_general/rt-adapters-fusch.yaml` |
| Adapter `Fusch Loxone Edge Adapter` (chart `octo-loxone-adapter`), `Hosts` -> Fusch Edge | `6a10f05c0000000000000b04` | same |
| LoxoneConfiguration `FuschLoxoneMiniserverConfig` (host `192.168.20.4`, port 80, no TLS; user and password set in Refinery Studio) | `6a10f05c0000000000000b03` | `data/_general/rt-loxone-configuration-fusch.yaml` |
| DataFlow `Fusch Loxone discovery` | `6a10f05c0000000000000d01` | `data/_pipelines/rt-pipelines-loxone-fusch.yaml` |
| Pipeline `Browse Fusch Miniserver` (edge adapter `…0b04`) | `6a10f05c0000000000000d02` | same |
| DataFlow `Fusch Loxone PV live` (AB#7696) | `6a10f05c0000000000000d03` | same |
| Pipeline `Configure Fusch PV State Sync` (mesh adapter) | `6a10f05c0000000000000d04` | same |
| Pipeline `Receive Fusch PV State Changes` (edge adapter `…0b04`) | `6a10f05c0000000000000d05` | same |
| Pipeline `Store Fusch PV States` (mesh adapter) | `6a10f05c0000000000000d06` | same |

The Firmianstraße Loxone files, the Fusch MQTT adapter `…0b02` and `data/bim/rt-fusch.yaml` are not touched.

### What the discovery pipeline sends to the Miniserver

Exactly two HTTP GET requests (Basic authentication, port 80, LAN only), made by the `LoxoneBrowse@1` node:

1. `GET /jdev/cfg/apiKey` (connection check)
2. `GET /data/LoxAPP3.json` (structure: rooms, categories, controls, state UUIDs)

No `jdev/sps/io/...` value read, no command, no websocket, no configuration upload. The pipeline contains no write node
towards the Miniserver, the wall connector or evcc, and writes nothing into the tenant model. The result is the
execution output of the pipeline (rooms, categories, controls); the `miniserver` block of the structure file (serial
number, MAC address, firmware, project name) is removed in the pipeline before the result is stored.

## 0. Preconditions

- Gerald's go for prod-1 / tenant `energyiq` in this session.
- Odysseus has finished the import of `data/bim/rt-fusch.yaml` (the Fusch site is not a dependency of discovery,
  but do not run imports in parallel).
- Edge device `mm-octo-mesh-edge-0003` runs the Communication Operator 3.5.46 and is connected:
  ```bash
  octo-cli --context prod-1_energyiq -c authstatus
  octo-cli --context prod-1_energyiq -c GetAdapters -j      # Fusch MQTT adapter 6a10f05c0000000000000b02 Online and Healthy
  ```
- The Miniserver user and password exist and Gerald enters them himself (step 2). **No Vault entry** is used for
  them — the same pattern as the Firmianstraße `LoxoneMiniserverConfig`: the values live only in the configuration
  entity (password as an encrypted Secret attribute). Prefer a Loxone user without control rights. Never write the
  values into a file, a command line, a work item or the PR.

## 1. Import (from `data/`, context `prod-1_energyiq`)

```bash
cd demo-energy-iq/data
octo-cli --context prod-1_energyiq -c ImportRt -f ./_general/rt-adapters-fusch.yaml -w -r
octo-cli --context prod-1_energyiq -c ImportRt -f ./_general/rt-loxone-configuration-fusch.yaml -w     # insert only, no -r
octo-cli --context prod-1_energyiq -c ImportRt -f ./_pipelines/rt-pipelines-loxone-fusch.yaml -w -r
```

- The adapters file upserts by rtId: the existing site `Fusch Edge` and the MQTT adapter keep their values; it adds
  the Loxone adapter.
- The configuration is imported **once, without `-r`**: a second run fails with "already exists" instead of writing
  (verified on test-2).
- Check:
  ```bash
  octo-cli --context prod-1_energyiq -c GetAdapters -j    # contains 6a10f05c0000000000000b04, Undeployed
  ```

## 2. Set the Miniserver user and password

The password is a Secret attribute (`System.Communication/LoxoneConfiguration.Password`): it is never in a file, is
encrypted server-side and is revealed to the adapter at deploy time. The user name is a normal attribute.

**Way used on prod-1 (2026-10-29):** Gerald opens Refinery Studio, Communication > Configurations >
`FuschLoxoneMiniserverConfig`, and enters User name and Password in the form — exactly as for the Firmianstraße
`LoxoneMiniserverConfig`. No agent sees or handles the values; there is no Vault entry.

(Alternative for a maintainer without the Studio: MCP `set_entity_secrets` on `6a10f05c0000000000000b03` with
`attributePath: "password"` and `update_entity` for `username` — the value is typed by the human, never by an agent.)

Check (never shows the value):

```bash
# get_entity_by_id (MCP) or Studio: "password": secretIsSet = true, secretKeyMissing = false; username is the real user
```

If the Miniserver only accepts HTTPS, set `isSslEnabled: true` and `port: 443` the same way (the file defaults to
HTTP port 80).

**Changes take effect only after a deploy.** The communication controller resolves the configuration (host, port,
user, revealed password, mapping targets) when a pipeline is **deployed** and ships it to the adapter; a running
pipeline keeps the values of its last deploy. After changing host, port or credentials run
`DeployDataFlow -id 6a10f05c0000000000000d01` (and every other deployed Fusch Loxone data flow) before the next
execution. Found on 2026-10-29: the host correction to `192.168.20.4` only took effect after `DeployDataFlow d01`.

## 3. Deploy the adapter to edge-0003

```bash
octo-cli --context prod-1_energyiq -c DeployWorkload -id 6a10f05c0000000000000b04
```

(MCP: `deploy_workload`.) The edge operator on `mm-octo-mesh-edge-0003` installs the chart `octo-loxone-adapter`.

Check, wait up to ~3 minutes:

```bash
octo-cli --context prod-1_energyiq -c GetAdapters -j
#   6a10f05c0000000000000b04: deploymentState Deployed, communicationState Online, configurationState Healthy/Configured
```

If it stays `Pending`/`Unregistered`: look at `lastDeploymentError` of the adapter and the operator log of edge-0003
(Dash0, namespace of the edge operator); do not touch the edge cluster by hand.

## 4. Deploy the data flow and run discovery

```bash
octo-cli --context prod-1_energyiq -c DeployDataFlow -id 6a10f05c0000000000000d01
octo-cli --context prod-1_energyiq -c GetPipelineStatus --identifier 6a10f05c0000000000000d02 --json      # Deployed
octo-cli --context prod-1_energyiq -c ExecutePipeline -id 6a10f05c0000000000000d02
octo-cli --context prod-1_energyiq -c GetLatestPipelineExecution -id 6a10f05c0000000000000d02 -j      # status only
```

**Reading the result.** `GetLatestPipelineExecution` shows the status but **no `OutputData`** for this pipeline (seen
on prod-1 on 2026-10-29). Read the structure from the debug capture of one run instead, then switch it off again:

```bash
octo-cli --context prod-1_energyiq -c SetPipelineDebug --identifier 6a10f05c0000000000000d02 --enabled true
octo-cli --context prod-1_energyiq -c ExecutePipeline -id 6a10f05c0000000000000d02        # note the execution id
octo-cli --context prod-1_energyiq -c GetPipelineDebugPoints --identifier 6a10f05c0000000000000d02 --executionId <executionId> --json
#   -> node ids; the node of SetPipelineExecutionResult@1 holds the final structure in its "output"
octo-cli --context prod-1_energyiq -c SetPipelineDebug --identifier 6a10f05c0000000000000d02 --enabled false
```

Switch debug capture off again **also when a step in between fails** (the last command above is the cleanup, run it
in any case), and check it with `GetPipelineDebug --identifier 6a10f05c0000000000000d02 --json` (`enabled: false`).

The data of one node comes from the communication controller REST API (tenant API, bearer token of the octo-cli
session; never print the token):

```
GET <communication-controller>/energyiq/v1/pipelinedebug/System.Communication%2FPipeline@6a10f05c0000000000000d02/<executionId>/<nodeId>
```

Save the response only in the session's own scratch directory (`$SCRATCH`, outside the repository) and use **only
its `output.structure`** (rooms, categories, controls). The `input` and `output.config` parts contain the connection
configuration (host, user name; the password is redacted, see `redactedPaths`) — never copy them anywhere.

Checks:

- Execution status Succeeded, no error text. A `Status: null` on a manually triggered run: query again after a few
  seconds, then read the adapter log in Dash0 (`LoxoneBrowse`, `Connecting to Loxone Miniserver`).
- `output.structure` holds `rooms`, `categories`, `controls` and **no** `miniserver` block:
  ```bash
  jq '.output.structure|keys' "$SCRATCH/dbg-SetPipelineExecutionResult.json"     # expect categories, controls, rooms
  jq '[.output.structure.controls[]|.type]|group_by(.)|map({type:.[0],n:length})' "$SCRATCH/dbg-SetPipelineExecutionResult.json"
  ```
  Result on 2026-10-29: 23 rooms, 46 categories, 68 controls.
- Typical errors: `ConnectionFailed` = Miniserver unreachable from edge-0003 (LAN/VLAN, port) or wrong host;
  HTTP 401 = wrong user or password; "password ... not available" = the secret is not set or not revealed: redeploy the
  data flow. A corrected host still failing with the old address = the data flow was not redeployed (see step 2).

## 5. Write the inventory (done 2026-10-29)

Result: [`docs/fusch-loxone-inventory.md`](fusch-loxone-inventory.md). For a later re-run: from the
`output.structure` of the debug capture (step 4) write `docs/fusch-loxone-inventory.md` as defined in AB#7695 (per control:
room, category, name, control type, state names, proposed EnergyIQ target; sections PV, grid/energy manager, wall
connector, battery, rooms with temperature, heating/cooling, shading, lighting; explicit sentences for rooms/sensors
and for the grid power vs. SHRDZM). The repository is public: **no** serial number, MAC address, user list, host
names of other devices or credentials in the inventory. Example state values are not part of the structure file; take
them from the live pipelines (AB#7696) or leave them out.

Delete the scratch file afterwards. The execution output stays in the tenant (it holds room and control names only).

## 6. Rollback

In this order; each step is independent, stop where the damage stops:

1. Stop the discovery: `UndeployDataFlow -id 6a10f05c0000000000000d01`.
2. Remove the adapter from edge-0003: `octo-cli --context prod-1_energyiq -c UndeployWorkload -id 6a10f05c0000000000000b04 -y`
   (the operator uninstalls the helm release). Check `GetAdapters`: `…0b04` Undeployed.
3. Remove the secret: `set_entity_secrets` with `clearSecretAttributes: ["password"]` on `6a10f05c0000000000000b03`
   (the password may be required to be cleared before the entity is deleted).
4. Delete the entities by rtId (MCP `delete_entity`, in this order): pipeline `6a10f05c0000000000000d02`, DataFlow
   `6a10f05c0000000000000d01`, adapter `6a10f05c0000000000000b04`, configuration `6a10f05c0000000000000b03`.
   **Never** delete `6addb8ec0c28d87762b0ad5e` (site `Fusch Edge`), `6a10f05c0000000000000b02` (MQTT adapter) or
   `6a10f05c0000000000000b01` (MQTT configuration).
5. If the Miniserver user was created for this: disable it in Loxone Config. (There is no Vault entry to remove.)

The Miniserver itself is never modified by this change, so there is nothing to restore there.

## 7. Live PV pipelines (AB#7696)

Data flow `Fusch Loxone PV live` (`…0d03`): PV string power and energy and the PV system total from the Miniserver
into PVString `…0a13` (Süd) / `…0a14` (Nord) and PhotovoltaicSystem `…0a12`, power rows into `PVStringArchive`
(`6a0e000000000000000a000a`) and `PhotovoltaicSystemArchive` (`6a0e000000000000000a000c`). Watched states, units and the
rule for the total: header of `data/_pipelines/rt-pipelines-loxone-fusch.yaml`; why grid power, battery and the wall
connector are not written: `docs/fusch-loxone-inventory.md`.

### 7.0 Preconditions

- Gerald's go for prod-1 / `energyiq` in this session; this PR merged.
- Steps 1-3 done (adapter `…0b04` Deployed/Online, credentials set) — true since 2026-10-29.
- Fusch PV entities `…0a12`-`…0a14` exist (AB#7694, imported) and the two archives are `Activated`.
- The Firmianstraße pipeline `Configure State Sync` (`67f0b00ec4ff02221e0b6009`) loads its configuration by
  well-known name `LoxoneMiniserverConfig` since AB#7741 (prod-1 2026-10-30), so it no longer touches
  `FuschLoxoneMiniserverConfig`. The Fusch pipelines address the Fusch configuration only by rtId.
- Rolled out on prod-1/energyiq 2026-10-30 06:05Z (dump `energyiq-prod-1-pre-ab7696-20261030`).

### 7.1 Import and deploy

```bash
cd demo-energy-iq/data
octo-cli --context prod-1_energyiq -c ImportRt -f ./_pipelines/rt-pipelines-loxone-fusch.yaml -w -r   # adds d03-d06, d01/d02 unchanged
octo-cli --context prod-1_energyiq -c DeployDataFlow -id 6a10f05c0000000000000d03
octo-cli --context prod-1_energyiq -c GetDataFlowStatus --identifier 6a10f05c0000000000000d03 --json     # d04, d05, d06 Deployed
```

Right after this deploy the edge trigger has no watch list yet (adapter log: "No MappingTargets configured, trigger will
not fire") — expected.

### 7.2 Set the watch list (once)

```bash
octo-cli --context prod-1_energyiq -c ExecutePipeline -id 6a10f05c0000000000000d04
```

The pipeline writes the 6 watched states as `MappingTargets` onto `FuschLoxoneMiniserverConfig` (`…0b03`, by rtId) and
redeploys the edge pipeline `…0d05` (`DeployPipeline@1` with the tenant's `ServiceAccountConfig`). Check:

- `get_entity_by_id` `System.Communication/LoxoneConfiguration` `…0b03`: `mappingTargets` has 6 items; host, user and
  the password state are unchanged.
- Adapter log of `…0b04` (Dash0 dataset `prod-1`, container of the Loxone adapter): "Loxone state change trigger
  started, watching 6 UUIDs". When the WebSocket cache is already connected, the trigger sends the current values at
  once ("Cache already ready, sending 6 initial values").

Run `…0d04` again only when the list in the file changes (then import the file again first).

### 7.3 Verify

- Entities (MCP `get_entity_by_id` or Studio): PVString `…0a13`/`…0a14` `currentPower` [kW] and `energyProduced` [kWh],
  PhotovoltaicSystem `…0a12` `totalCurrentPower` [kW] and `totalEnergyProduced` [kWh] have values and a recent
  `rtChangedDateTime`. Plausibility in daylight: `totalCurrentPower` ≈ sum of the two `currentPower` (inverter vs.
  string measurement, a few percent apart); at night all power values 0. Compare one value with the Loxone app.
- Archives: rows for `rtid = 6a10f05c0000000000000a13`/`…a14` in `PVStringArchive` and `…a12` in
  `PhotovoltaicSystemArchive` (Studio query or `query_stream_data_simple`), one row per changed power value.
- GridConnection `…0a03` keeps receiving only SHRDZM values (no change by this data flow).
- Units: if a power value looks 1000× too large (W instead of kW) the Miniserver meter is configured in W: stop
  (step 7.4 item 1) and add a scaling step before going on.

### 7.4 Rollback

1. `octo-cli --context prod-1_energyiq -c UndeployDataFlow -id 6a10f05c0000000000000d03` — stops all three pipelines;
   the discovery data flow `…0d01` and the adapter stay as they are.
2. Optional: remove the watch list — `update_entity` on `…0b03` with `mappingTargets: []` (the adapter has nothing else
   to watch).
3. Optional: delete `…0d06`, `…0d05`, `…0d04`, `…0d03` (MCP `delete_entity`, in this order).
4. Values already written stay on `…0a12`-`…0a14` and in the archives (time series rows are not deleted).

Nothing is ever sent to a control of the Miniserver, the wall connector or evcc, so there is nothing to restore there.

## 8. Rooms and devices (AB#7697)

`data/bim/rt-fusch.yaml` now also holds the Fusch rooms and devices the inventory lists (rtIds `…0a20`-`…0a69`): 11
Spaces under Building `…0a10`, 5 room TemperatureSensors (`…0a30`-`…0a34`), the heat mixer Valve `…0a40` with its flow
TemperatureSensor `…0a41`, 6 ShadingDevices (`…0a50`-`…0a55`) and 10 Luminaires (`…0a60`-`…0a69`). Full list and the
modelling decisions: header of the "Rooms and devices" block in that file. Existing Fusch entities `…0a01`-`…0a16` are
unchanged; the upsert rewrites them with the same values.

The data flow `Fusch Loxone PV live` (`…0d03`, names of the data flow and its pipelines unchanged) now also feeds
these entities, read only, by fixed state UUID → fixed rtId (state UUIDs from the debug output of discovery run
`609f821f-8ec8-438f-8962-7001c2edb9b6`, node `SetPipelineExecutionResult@1`):

| Control (room, name, type) | State | Target | Rule |
|----------------------------|-------|--------|------|
| Bad OG, Flur EG, Küche OG, Schlafzimmer, Wohnzimmer — room controller `IRoomControllerV2` (5) | `tempActual` [°C] | TemperatureSensor `…0a30`-`…0a34` `CurrentValue` → TemperatureSensorArchive `6a0e…0a0001` | as is |
| Heizraum, "Heizungsmischer", `Heatmixer` (1) | `tempActual` [°C] | TemperatureSensor `…0a41` `CurrentValue` → TemperatureSensorArchive `6a0e…0a0001` | as is |
| Bad OG "Automatikjalousie", Küche OG "Balkontür"/"Fenster", Schlafzimmer "Automatikjalousie", Wohnzimmer "Balkontüre"/"Fenster" — `Jalousie` (6) | `position` [0..1] | ShadingDevice `…0a50`-`…0a55` `Position` [%] → ShadingDeviceArchive `6a0e…0a0005` and ShadingDeviceStatusArchive `6a0e…0a0013` | `value * 100` (Firmianstraße `jalousie-shading`) |
| Bad OG, Flur EG, Flur OG, Flur UG, Garage, Küche OG, Schlafzimmer, Stiegenhaus, Vorratskeller, Wohnzimmer — `LightControllerV2` (10) | `activeMoodsNum` | Luminaire `…0a60`-`…0a69` `IsOn` = `value <> 778`, `DimmingLevel` = 100 / 0 → LuminaireArchive `6a0e…0a0004` and LuminaireStatusArchive `6a0e…0a0012` | Firmianstraße `lightcontroller-ison` / `lightcontroller-level` (778 = Loxone "Off" mood) |

Exact UUID → rtId pairs: the `Switch@1` cases of `…0d06` (one comment per case). `…0d04` now writes 28 watched
states (the 6 PV states unchanged, then the 22 new ones). The PV cases of `…0d06` are unchanged; only the internal list
of archived updates is renamed `_archiveUpdates` (PV power and the room values share one list and one `If@1`, because
an `If@1` after the archive nodes did not run in a dry run, see section 7). Not mapped: `tempTarget`, humidity, CO2,
the central blocks, the outdoor temperature and the heating plant states.

Order on prod-1: 8.1 (entities) before 8.2 (mappings) — the mappings address the new rtIds.

### 8.1 Import the rooms and devices

Preconditions: Gerald's go for prod-1 / `energyiq` in this session; the AB#7697 PR merged; no other import running.

```bash
cd demo-energy-iq/data
octo-cli --context prod-1_energyiq -c authstatus

# 1. Rollback copy (tenant dump, without archive data; a few MB). Path outside the repository.
octo-cli --context prod-1_energyiq -c Dump -tid energyiq -f ~/.octo/dumps/energyiq-prod-1-before-ab7697.tar.gz

# 2. Import (upsert by rtId; adds a20-a69, a01-a16 keep their values)
octo-cli --context prod-1_energyiq -c ImportRt -f ./bim/rt-fusch.yaml -w -r
```

No deploy and no pipeline run in this step.

Checks (read only):

- Counts in the tenant: `EnergyIQ/Space` 18 → 29, `EnergyIQ/TemperatureSensor` 21 → 27, `EnergyIQ/Luminaire` 18 → 28,
  `EnergyIQ/ShadingDevice` 8 → 14, `EnergyIQ/Valve` 14 → 15 (`rt_explorer.py count …` or Studio).
- `get_entity_by_id` `EnergyIQ/Space` `6a10f05c0000000000000a2a` ("Wohnzimmer Fusch"): parent `…0a10`, one contained
  sensor (`Temp-Wohnzimmer Fusch`), contained elements `Jalousie Wohnzimmer Balkontüre Fusch`,
  `Jalousie Wohnzimmer Fenster Fusch`, `Licht Wohnzimmer Fusch`.
- PV values on `…0a12`-`…0a14` keep arriving (section 7.3); GridConnection `…0a03` unchanged.

Rollback (in this order, stop where the damage stops):

1. If 8.2 already ran: its rollback first (section 8.2).
2. Delete the new entities by rtId (MCP `delete_entity` or Studio), devices first, spaces last:
   `…0a60`-`…0a69`, `…0a50`-`…0a55`, `…0a41`, `…0a40`, `…0a30`-`…0a34`, `…0a20`-`…0a2a`. **Never** delete
   `…0a01`-`…0a16`.
3. Only if the tenant is otherwise damaged: restore the dump of step 1 (`Restore`), which also resets every change made
   after the dump — ask Gerald first.

### 8.2 Redeploy the live data flow with the room mappings

Preconditions: 8.1 done; the archives `6a0e…0a0001`, `…0a0004`, `…0a0005`, `…0a0012`, `…0a0013` are `Activated` on
prod-1 (they feed the Firmianstraße boards; check in Studio or with `get_archive_storage_stats`); adapter `…0b04`
Deployed/Online.

```bash
cd demo-energy-iq/data
# 1. Rollback copy of the definitions live since 2026-10-30 (commit a806ee6 = state before AB#7697), outside the repository
git show a806ee6:data/_pipelines/rt-pipelines-loxone-fusch.yaml > ~/.octo/dumps/rt-pipelines-loxone-fusch-before-ab7697.yaml
# 2. Import the new definitions (upsert; d01/d02 unchanged, d03-d06 get the new definitions)
octo-cli --context prod-1_energyiq -c ImportRt -f ./_pipelines/rt-pipelines-loxone-fusch.yaml -w -r
# 3. Redeploy the data flow (d04, d05, d06)
octo-cli --context prod-1_energyiq -c DeployDataFlow -id 6a10f05c0000000000000d03
octo-cli --context prod-1_energyiq -c GetDataFlowStatus --identifier 6a10f05c0000000000000d03 --json   # d04, d05, d06 Deployed
# 4. Write the 28 watched states onto FuschLoxoneMiniserverConfig and redeploy the edge pipeline d05
octo-cli --context prod-1_energyiq -c ExecutePipeline -id 6a10f05c0000000000000d04
```

Checks (read only):

- `get_entity_by_id` `System.Communication/LoxoneConfiguration` `…0b03`: `mappingTargets` has **28** items (the first 6
  are the PV states); host, user and the password state are unchanged.
- Adapter log of `…0b04` (Dash0 dataset `prod-1`): "Loxone state change trigger started, watching 28 UUIDs" and, with a
  connected cache, "sending 28 initial values".
- Entities: TemperatureSensors `…0a30`-`…0a34` and `…0a41` `currentValue` in °C (rooms about 18-25, mixer flow about
  25-60); ShadingDevices `…0a50`-`…0a55` `position` 0-100; Luminaires `…0a60`-`…0a69` `isOn` true/false with
  `dimmingLevel` 100/0. Compare one room temperature, one blind and one light with the Loxone app.
- Archives: rows for the new rtIds in `TemperatureSensorArchive`, `LuminaireArchive` / `LuminaireStatusArchive` (with the
  `name` column filled) and `ShadingDeviceArchive` / `ShadingDeviceStatusArchive`; one row per change.
- PV: `…0a12`-`…0a14` keep receiving values (section 7.3); GridConnection `…0a03` unchanged.
- Boards: with a Fusch room picked the Raumtemperaturen chart shows its values; with a Firmianstraße room picked nothing
  changes. The site selector on the three boards is AB#7693.

Rollback:

1. `octo-cli --context prod-1_energyiq -c ImportRt -f ~/.octo/dumps/rt-pipelines-loxone-fusch-before-ab7697.yaml -w -r`,
   then `DeployDataFlow -id …0d03` and `ExecutePipeline -id …0d04` — back to the 6 PV states (watch list 28 → 6).
2. If PV must stop as well: section 7.4.
3. Values already written stay on the entities and in the archives (time series rows are not deleted).

Nothing is ever sent to a control of the Miniserver: the pipelines only receive state changes.

## Validation evidence (2026-10-28, before the PR)

- Pipeline definition validated against the pipeline JSON schema of the Loxone adapter
  (`octo-adapter-loxone/bin/DebugL/net10.0/pipeline-schema.json`, draft 2020-12): 0 errors; the same check reports an
  error for an unknown node and for a `Project@1` without its required `fields`. `validate_pipeline_definition` on
  test-2 is not possible for this pipeline: the mesh adapter does not know `LoxoneBrowse@1`, and the Loxone adapter
  cannot run on test-2 (no edge device for the temporary tenant).
- Temporary tenant on test-2 (`d7695tmp`, deleted afterwards): the three files import without error; a second import
  with upsert is clean; the insert-only second import of the configuration fails as designed;
  `set_entity_secrets` on `LoxoneConfiguration.password` reports `secretIsSet`.
- Not achieved on test-2: a dry run of the two `Project@1` nodes. The central operator left the mesh adapter of the
  temporary tenant in `Pending` (about 10 minutes, no error), so no adapter was online. Covered instead by the SDK unit
  tests of the node (`octo-communication-sdk`, `ProjectNodeTests`: 6 of 6 green, including multi-match
  `$.items[*]` projection and clear + whitelist). The first real run on edge-0003 is the end-to-end proof; check the
  result as described in step 4.
- No write path: the pipeline has exactly the nodes `FromExecutePipelineCommand@1`, `GetPipelineConfigByWellKnownName@1`,
  `LoxoneBrowse@1` (two HTTP GETs, see above), `Project@1` (2x), `SetPipelineExecutionResult@1`.

## Validation evidence AB#7697 (2026-10-30, before the PR)

- Temporary tenant `tmp-daidalos7697` on test-2 (created under `octosystem`, deleted afterwards): EnergyIQ 2.13.0
  (compiled from the unchanged CK source, `ck-energyiq-2.yaml`; `ImportFromCatalog EnergyIQ-2.13.0` imported only the
  dependencies `Basic-2.5.0`/`Basic.Energy-1.10.0` and then reported "already up to date"), then
  `data/bim/rt-fusch.yaml` imported three times: insert-only, upsert, upsert — all three jobs completed without error.
- After the third import: 11 Spaces, 6 TemperatureSensors, 6 ShadingDevices, 10 Luminaires, 1 Valve (no duplicates);
  every TemperatureSensor, Luminaire and ShadingDevice resolves to its Space over `EnergyIQ/SpaceSensors` /
  `EnergyIQ/SpaceElements`, the Valve to "Heizraum Fusch" over `EnergyIQ/SpaceActuators`, the flow sensor to the Valve
  over `EnergyIQ/EquipmentSensors`.
- prod-1 (read only, `rt_explorer.py list`): no entity of the new types in the `6a10f05c…0a2x`-`…0a6x` range; all new
  names end in "Fusch" and do not occur at Firmianstraße.
- Mappings: every UUID of the 22 states (Odin's extraction) cross-checked against the watch list of `…0d04` (control
  UUID, state name, state UUID) and against the `CreateUpdateInfo@1` target of its `…0d06` case; the 6 PV cases are
  identical to `origin/main` apart from the list rename; 28 watch-list entries = 28 cases.
- `validate_pipeline_definition` on test-2 (tenant `energyiq`, mesh adapter `670000000000000000000002`): `isValid: true`
  for `…0d06` with one case of each kind (PV, room temperature, blind, light; all cases come from the same templates)
  and all tail nodes. The same tool rejects a probe with an invalid enum value. The local `pipeline_validate.py`
  passes all definitions but also passes invalid probes, so it is not counted as evidence.
- Dry run of `…0d06` with `data/samples/loxone-fusch-states.json` (29 states) on test-2 `energyiq`: temporary DataFlow
  `6a10f05c0000000000000f01` and pipeline `…0f02` (both named `tmp-AB7697-dryrun`, trigger swapped to
  `FromExecutePipelineCommand@1`), execution `9b3458b0-dea6-4a53-a9bb-a3cb354153ac`. Debug tree: 28 items hit their
  case (6 PV, 6 temperatures, 6 blinds with `Math@1` then `CreateUpdateInfo@1`, 10 lights); the lights with value 778
  ran only the `Equals` branch, the lights with value 1 only the `NotEquals` branch; the `gridCurr` item hit no case;
  both `If@1` blocks ran, with all 7 archive pairs and both `ApplyChanges@2` (dry run, nothing written: PVString
  `…0a13` `rtChangedDateTime` still 2026-10-28). Afterwards the data flow was undeployed and both temporary entities
  deleted; GraphQL shows `totalCount: 0` for both. No other entity on test-2 was deployed or changed.
- Not dry-run: `…0d04` (all its writes are dry-run honoured nodes, its only change is the longer JSON list) and the
  edge pipeline `…0d05` (unchanged). A scratch tenant `tmp-daidalos7697b` for the dry run was given up: its mesh adapter
  stayed `Pending`/`Unregistered` for 10 minutes (same as section 7; reported to Odin); deleted afterwards.
