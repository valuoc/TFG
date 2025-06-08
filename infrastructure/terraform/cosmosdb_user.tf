resource "azurerm_cosmosdb_account" "user" {
  depends_on                       = [azurerm_cosmosdb_account.session]
  name                             = "${local.resource_prefix}-user"
  location                         = azurerm_resource_group.region_rgs[var.main_region].location
  resource_group_name              = azurerm_resource_group.region_rgs[var.main_region].name
  offer_type                       = "Standard"
  kind                             = "GlobalDocumentDB"
  multiple_write_locations_enabled = true
  tags                             = local.tags

  automatic_failover_enabled = true

  consistency_policy {
    consistency_level = "Session"
  }

  geo_location {
    location          = var.main_region
    failover_priority = 0
    zone_redundant    = false
  }

  dynamic "geo_location" {
    for_each = local.secondary_regions_sorted_list
    content {
      location          = geo_location.value.Region
      failover_priority = geo_location.value.Index
      zone_redundant    = false
    }
  }
}

resource "azurerm_cosmosdb_sql_database" "user" {
  name                = "${local.resource_prefix}-user"
  resource_group_name = azurerm_resource_group.region_rgs[var.main_region].name
  account_name        = azurerm_cosmosdb_account.user.name
}

resource "azurerm_cosmosdb_sql_container" "user_contents" {
  name                = "${local.resource_prefix}-user-contents"
  resource_group_name = azurerm_resource_group.region_rgs[var.main_region].name
  account_name        = azurerm_cosmosdb_account.user.name
  database_name       = azurerm_cosmosdb_sql_database.user.name
  partition_key_paths = ["/pk"]

  indexing_policy {
    indexing_mode = "consistent"

    excluded_path {
      path = "/*"
    }

    included_path {
      path = "/sk/?"
    }

    composite_index {
      index {
        path  = "/isRootConversation"
        order = "Ascending"
      }
      index {
        path  = "/sk"
        order = "Ascending"
      }
    }
  }
}

resource "azurerm_cosmosdb_sql_container" "user_feeds" {
  name                = "${local.resource_prefix}-user-feeds"
  resource_group_name = azurerm_resource_group.region_rgs[var.main_region].name
  account_name        = azurerm_cosmosdb_account.user.name
  database_name       = azurerm_cosmosdb_sql_database.user.name
  partition_key_paths = ["/pk"]

  indexing_policy {
    indexing_mode = "consistent"

    excluded_path {
      path = "/*"
    }

    included_path {
      path = "/sk/?"
    }
  }
}

resource "azurerm_cosmosdb_sql_container" "user_profiles" {
  name                = "${local.resource_prefix}-user-profile"
  resource_group_name = azurerm_resource_group.region_rgs[var.main_region].name
  account_name        = azurerm_cosmosdb_account.user.name
  database_name       = azurerm_cosmosdb_sql_database.user.name
  partition_key_paths = ["/pk"]

  indexing_policy {
    indexing_mode = "consistent"

    excluded_path {
      path = "/*"
    }
  }
}

resource "azurerm_key_vault_secret" "cosmosdb_user_authkey" {
  depends_on = [
    azurerm_key_vault_secret.cosmosdb_user_id,
    azurerm_key_vault_secret.cosmosdb_user_endpoint,
    azurerm_key_vault_secret.cosmosdb_user_contents_container,
    azurerm_key_vault_secret.cosmosdb_user_profiles_container,
    azurerm_key_vault_secret.cosmosdb_user_follows_container,
    azurerm_key_vault_secret.cosmosdb_user_feeds_container
  ]
  for_each     = local.all_regions
  name         = "CosmosDb--User--AuthKey"
  value        = azurerm_cosmosdb_account.user.primary_key
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "cosmosdb_user_id" {
  for_each     = local.all_regions
  name         = "CosmosDb--User--Id"
  value        = azurerm_cosmosdb_account.user.name
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "cosmosdb_user_endpoint" {
  for_each     = local.all_regions
  name         = "CosmosDb--User--Endpoint"
  value        = azurerm_cosmosdb_account.user.endpoint
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "cosmosdb_user_contents_container" {
  for_each     = local.all_regions
  name         = "CosmosDb--User--Containers--Contents--Id"
  value        = azurerm_cosmosdb_sql_container.user_contents.name
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "cosmosdb_user_profiles_container" {
  for_each     = local.all_regions
  name         = "CosmosDb--User--Containers--Profiles--Id"
  value        = azurerm_cosmosdb_sql_container.user_profiles.name
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "cosmosdb_user_follows_container" {
  for_each     = local.all_regions
  name         = "CosmosDb--User--Containers--Follows--Id"
  value        = azurerm_cosmosdb_sql_container.user_profiles.name
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags
}

resource "azurerm_key_vault_secret" "cosmosdb_user_feeds_container" {
  for_each     = local.all_regions
  name         = "CosmosDb--User--Containers--Feeds--Id"
  value        = azurerm_cosmosdb_sql_container.user_feeds.name
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags
}
