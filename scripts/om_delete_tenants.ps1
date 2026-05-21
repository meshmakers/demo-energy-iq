param (
    [string]$tenantId = "energyiq"
)
octo-cli -c delete -tid $tenantId
