#octo-cli -c ImportRt -f ./../data/_general/rt-autoincrement.yaml -w

# Mesh adapter is auto-provisioned by the System.Communication blueprint
# (rtId 670000000000000000000002); no adapter entity import needed.

# Import archives (one per sensor type since v2 - moved here from octo-adapter-loxone).
# -r/Upsert mode lets us correct the Columns of already-imported but not-yet-activated archives.
octo-cli -c ImportRt -f ./../data/_general/rt-archives-energyiq.yaml -w -r
octo-cli -c ActivateArchive -id 6a0e000000000000000a0001
octo-cli -c ActivateArchive -id 6a0e000000000000000a0002
octo-cli -c ActivateArchive -id 6a0e000000000000000a0003

# Import pipelines
octo-cli -c ImportRt -f ./../data/_pipelines/rt-simulation-adapters.yaml -w

# Import queries
octo-cli -c ImportRt -f ./../data/_queries/_trees.yaml

# Import BIM
octo-cli -c ImportRt -f ./../data/bim/rt-firmianstrasse.yaml -w

