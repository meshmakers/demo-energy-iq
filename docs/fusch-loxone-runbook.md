# Fusch Loxone adapter: rollout runbook (prod-1, tenant energyiq)

Work items: AB#7695 (adapter, configuration, discovery pipeline), AB#7696 (live pipelines), Feature AB#7690.
Decision of 2026-10-28: the Fusch Loxone Miniserver Gen 2 (`192.168.20.10`) is reached **only** through a Loxone
adapter on edge device `mm-octo-mesh-edge-0003`. No tailnet change, no direct HTTP from a developer machine to the
Miniserver, no manual export.

**Nothing in this runbook has been run on prod-1.** Every step needs Gerald's go in the session that runs it
(`agent-operating-rules` §3, §5.1).

## What is deployed

| Entity | rtId | File |
|--------|------|------|
| DeploymentSite `Fusch Edge` (exists on prod-1, unchanged) | `6addb8ec0c28d87762b0ad5e` | `data/_general/rt-adapters-fusch.yaml` |
| Adapter `Fusch Loxone Edge Adapter` (chart `octo-loxone-adapter`), `Hosts` -> Fusch Edge | `6a10f05c0000000000000b04` | same |
| LoxoneConfiguration `FuschLoxoneMiniserverConfig` (host `192.168.20.10`, port 80, no TLS; user and password set at rollout) | `6a10f05c0000000000000b03` | `data/_general/rt-loxone-configuration-fusch.yaml` |
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
- The Miniserver user and password exist. Vault: **no Loxone entry exists yet** (checked 2026-10-28: only
  `secret/meshmakers/edge/sites/fusch/mqtt` with the keys `host, password, port, tls, user`). Use a **read-only Loxone
  user** (no control rights): then even a wrong request cannot change anything. Create the user in Loxone Config, then
  store the values in Vault at `secret/meshmakers/edge/sites/fusch/loxone` (keys `host`, `user`, `password`) before
  step 2. Never write the values into a file, a command line, a work item or the PR.

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

Set both with the value read from Vault into the shell only (no `echo`, no history; commands below show the shape,
`<…>` are placeholders):

- MCP (octomesh MCP of prod-1, user session), high risk, needs the go:
  `set_entity_secrets` with `ckTypeId: System.Communication/LoxoneConfiguration`,
  `rtId: 6a10f05c0000000000000b03`, `secrets: [{ attributePath: "password", value: "<from Vault>" }]`
  and `update_entity` for the attribute `username` (`<miniserver user>`).
- Or Refinery Studio: Communication > Configurations > `FuschLoxoneMiniserverConfig`, fields User name and Password.

Check (never shows the value):

```bash
# get_entity_by_id (MCP) or Studio: "password": secretIsSet = true, secretKeyMissing = false; username is the real user
```

If the Miniserver only accepts HTTPS, set `isSslEnabled: true` and `port: 443` the same way (the file defaults to
HTTP port 80).

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
octo-cli --context prod-1_energyiq -c GetLatestPipelineExecution -id 6a10f05c0000000000000d02 -j > "$SCRATCH/fusch-loxone-structure.json"
```

`$SCRATCH` is the session's own scratch directory (outside the repository).

Checks:

- Execution status Succeeded, no error text. A `Status: null` on a manually triggered run: query again after a few
  seconds, then read the adapter log in Dash0 (`LoxoneBrowse`, `Connecting to Loxone Miniserver`).
- `OutputData` holds `rooms`, `categories`, `controls` and **no** `miniserver` block:
  ```bash
  jq 'keys' "$SCRATCH/fusch-loxone-structure.json"            # expect rooms, categories, controls
  jq '[.controls[]|.type]|group_by(.)|map({type:.[0],n:length})' "$SCRATCH/fusch-loxone-structure.json"
  ```
  (adjust the first path to the `OutputData` field of the execution JSON).
- Typical errors: `ConnectionFailed` = Miniserver unreachable from edge-0003 (LAN/VLAN, port) or wrong host;
  HTTP 401 = wrong user or password; "password ... not available" = the secret is not set or not revealed: redeploy the
  data flow.

## 5. Write the inventory

From `$SCRATCH/fusch-loxone-structure.json` write `docs/fusch-loxone-inventory.md` as defined in AB#7695 (per control:
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
5. If the Miniserver user was created for this: disable it in Loxone Config; delete the Vault entry
   `secret/meshmakers/edge/sites/fusch/loxone` if it was created for this.

The Miniserver itself is never modified by this change, so there is nothing to restore there.

## Validation evidence (2026-10-28, before the PR)

- Pipeline definition validated against the pipeline JSON schema of the Loxone adapter
  (`octo-adapter-loxone/bin/DebugL/net10.0/pipeline-schema.json`): 0 errors. (The mesh adapter on test-2 does not
  know `LoxoneBrowse@1`, so the Loxone node cannot be validated there.)
- Temporary tenant on test-2 (`d7695tmp`, deleted afterwards): the three files imported without error; second
  import with upsert clean; insert-only second import of the configuration fails as designed; `set_entity_secrets`
  on `LoxoneConfiguration.password` reports `secretIsSet`. Details of the projection test: see the PR.
- Grep evidence (no write path): see the PR description.
