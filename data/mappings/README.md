# DataPointMapping backup (`datapoint-mappings.json`)

This folder holds the versioned backup of the tenant's **manually created**
DataPointMappings. Everything the Rules-based Auto-Map pipeline generates is
deliberately NOT part of the backup — it is reproducible by re-running that
pipeline, and its deterministic `ruleId|rtId|state` names embed RtIds that
change on every tenant re-initialisation.

The backup survives `om_initialize_tenant` because it stores **natural
identities** instead of RtIds:

| Side | Identity | Why it is stable |
|------|----------|------------------|
| Source (`Loxone/Control`) | `LoxoneUuid` | Assigned by the Miniserver; the browse pipeline GetOrCreates controls by it |
| Target (`EnergyIQ/*`) | `GlobalId` (+ the fixed seed RtIds) | Seeded from `data/bim/rt-firmianstrasse.yaml` |

Entity names are carried as a last-resort fallback (resolved only when unique).

## Workflow

Both pipelines live in the **Mapping Backup** data flow
(`data/_pipelines/rt-pipelines-mapping-backup.yaml`) and require it to be
**deployed** on the mesh adapter (Studio → Communication → Data Flows).

**From the Studio:** the Data Mappings page shows a **Backup** toolbar row
(Export / Import…) as soon as the deployed backup pipelines are detected
(auto-discovered by their `ExportDataPointMappings@1` /
`ImportDataPointMappings@1` nodes). Export downloads
`datapoint-mappings.json`; Import… uploads it and shows the
resolved/unresolved statistics inline. Both run via the Communication
Controller (`ExecutePipelineCommand`) — no direct adapter access needed.

**From the shell:**

```powershell
# Backup: export the manual mappings into this folder, then commit the file.
./scripts/om_export_mappings.ps1

# Restore (e.g. after om_initialize_tenant + one Loxone browse run):
./scripts/om_import_mappings.ps1
```

The import response lists unresolved entries (renamed rooms, deleted controls,
ambiguous names) with a reason each — resolve those manually in the Studio's
Mapping Coverage page (Orphan Sources tab). The import is idempotent: mappings
are matched by name via GetOrCreate, so re-running it never duplicates.

Direct HTTP (what the scripts do):

```
GET  https://localhost:5020/energyiq/mappings/export   → export document
POST https://localhost:5020/energyiq/mappings/import   → import statistics
```
