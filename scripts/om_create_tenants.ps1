param (
    [string]$tenantId = "energyiq"
)
octo-cli -c Create -tid $tenantId -db $tenantId


