param (
    [string]$configuration = "Release"
)

octo-cli -c EnableCommunication
octo-cli -c EnableStreamData
#octo-cli -c EnableReporting

octo-cli -c ImportFromCatalog -cn LocalFileSystemCatalog -m Basic-2.0.2 -w
octo-cli -c ImportFromCatalog -cn LocalFileSystemCatalog -m Basic.Energy-1.1.2 -w
octo-cli -c importck -f ../src/EnergyIqCkModel/bin/$configuration/net10.0/octo-ck-libraries/EnergyIqCkModel/out/ck-energyiq-2.yaml -w
