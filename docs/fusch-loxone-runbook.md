# Fusch Loxone adapter: rollout runbook (prod-1, tenant energyiq)

Work items: AB#7695 (adapter, configuration, discovery pipeline), AB#7696 (live pipelines), Feature AB#7690.
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
