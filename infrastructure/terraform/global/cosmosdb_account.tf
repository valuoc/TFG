locals {
  secondary_regions_sorted_list = [for i,x in sort(var.secondary_regions) : { Index: i, Region: x }]
}

resource "azurerm_cosmosdb_account" "account" {
  name                             = "${local.resource_prefix}-account"
  location                         = azurerm_resource_group.region_rgs[var.main_region].location
  resource_group_name              = azurerm_resource_group.region_rgs[var.main_region].name
  offer_type                       = "Standard"
  kind                             = "GlobalDocumentDB"
  multiple_write_locations_enabled = false
  tags                             = local.tags

  automatic_failover_enabled = true

  consistency_policy {
    consistency_level = "ConsistentPrefix"
  }

  dynamic "geo_location" {
    for_each = local.secondary_regions_sorted_list
    content {
      location          = geo_location.value.Region
      failover_priority = geo_location.value.Index
    }
  }
}

resource "azurerm_cosmosdb_sql_database" "account" {
  name                = "${local.resource_prefix}-account"
  resource_group_name = azurerm_resource_group.region_rgs[var.main_region].name
  account_name        = azurerm_cosmosdb_account.account.name
}

resource "azurerm_cosmosdb_sql_container" "account" {
  name                = "${local.resource_prefix}-account"
  resource_group_name = azurerm_resource_group.region_rgs[var.main_region].name
  account_name        = azurerm_cosmosdb_account.account.name
  database_name       = azurerm_cosmosdb_sql_database.account.name
  partition_key_paths = ["/pk"]

  indexing_policy {
    indexing_mode = "consistent"

    excluded_path {
      path = "/*"
    }
  }
}

resource "azurerm_key_vault_secret" "cosmosdb_account_id" {
  for_each     = local.all_regions
  name         = "CosmosDb--Account--Id"
  value        = azurerm_cosmosdb_account.account.name
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags 
}

resource "azurerm_key_vault_secret" "cosmosdb_account_endpoint" {
  for_each     = local.all_regions
  name         = "CosmosDb--Account--Endpoint"
  value        = azurerm_cosmosdb_account.account.endpoint
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags 
}

resource "azurerm_key_vault_secret" "cosmosdb_account_authkey" {
  for_each     = local.all_regions
  name         = "CosmosDb--Account--AuthKey"
  value        = azurerm_cosmosdb_account.account.primary_key
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags 
}

resource "azurerm_key_vault_secret" "cosmosdb_account_container" {
  for_each     = local.all_regions
  name         = "CosmosDb--Account--Containers--Accounts--Id"
  value        = azurerm_cosmosdb_sql_container.account.name
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags 
}