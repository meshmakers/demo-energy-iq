#octo-cli -c ImportRt -f ./../data/_general/rt-autoincrement.yaml -w

# Import adapters
octo-cli -c ImportRt -f ./../data/_general/rt-adapters-mesh.yaml -w

# Import pipelines
octo-cli -c ImportRt -f ./../data/_pipelines/rt-pipeline-excel.yaml -w
octo-cli -c ImportRt -f ./../data/_pipelines/rt-simulation-adapters.yaml -w

# Import queries
octo-cli -c ImportRt -f ./../data/_queries/_trees.yaml

# Import BIM
octo-cli -c ImportRt -f ./../data/bim/rt-firmianstrasse.yaml -w

