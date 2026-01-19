param (
    [string]$configuration = "Release"
)

octo-cli -c EnableCommunication
#octo-cli -c EnableReporting

octo-cli -c importck -f ./ck-basic-2.yaml -w
octo-cli -c importck -f ../src/EnergyIqCkModel/bin/$configuration/net10.0/octo-ck-libraries/EnergyIqCkModel/out/ck-energyiq.yaml -w
