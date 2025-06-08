resource "azurerm_cosmosdb_account" "session" {
  depends_on                       = [azurerm_cosmosdb_account.account]
  for_each                         = local.all_regions
  name                             = "${local.resource_prefix}-${each.key}-session"
  location                         = azurerm_resource_group.region_rgs[each.key].location
  resource_group_name              = azurerm_resource_group.region_rgs[each.key].name
  offer_type                       = "Standard"
  kind                             = "GlobalDocumentDB"
  multiple_write_locations_enabled = false
  tags                             = local.tags

  automatic_failover_enabled = true

  consistency_policy {
    consistency_level = "ConsistentPrefix"
  }

  geo_location {
    location          = each.key
    failover_priority = 0
    zone_redundant    = false
  }

  capabilities {
    name = "EnableServerless"
  }
}

resource "azurerm_cosmosdb_sql_database" "session" {
  for_each            = local.all_regions
  name                = "${local.resource_prefix}-${each.key}-session"
  resource_group_name = azurerm_resource_group.region_rgs[each.key].name
  account_name        = azurerm_cosmosdb_account.session[each.key].name
}

resource "azurerm_cosmosdb_sql_container" "session" {
  for_each            = local.all_regions
  name                = "${local.resource_prefix}-${each.key}-session"
  resource_group_name = azurerm_resource_group.region_rgs[each.key].name
  account_name        = azurerm_cosmosdb_account.session[each.key].name
  database_name       = azurerm_cosmosdb_sql_database.session[each.key].name
  partition_key_paths = ["/pk"]

  indexing_policy {
    indexing_mode = "consistent"

    excluded_path {
      path = "/*"
    }
  }
}

resource "azurerm_key_vault_secret" "cosmosdb_session_authkey" {
  depends_on = [
    azurerm_key_vault_secret.cosmosdb_session_id,
    azurerm_key_vault_secret.cosmosdb_session_endpoint,
    azurerm_key_vault_secret.cosmosdb_session_container
  ]
  for_each     = local.all_regions
  name         = "CosmosDb--Session--AuthKey"
  value        = azurerm_cosmosdb_account.session[each.key].primary_key
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "cosmosdb_session_id" {
  for_each     = local.all_regions
  name         = "CosmosDb--Session--Id"
  value        = azurerm_cosmosdb_account.session[each.key].name
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "cosmosdb_session_endpoint" {
  for_each     = local.all_regions
  name         = "CosmosDb--Session--Endpoint"
  value        = azurerm_cosmosdb_account.session[each.key].endpoint
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "cosmosdb_session_container" {
  for_each     = local.all_regions
  name         = "CosmosDb--Session--Containers--Sessions--Id"
  value        = azurerm_cosmosdb_sql_container.session[each.key].name
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags
}
