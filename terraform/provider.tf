# ====================================================================================
# OVERFLOW PROJECT - TERRAFORM CONFIGURATION
# ====================================================================================
# Project-specific Terraform that references shared infrastructure from
# infrastructure-helios via remote state (Azure Blob Storage backend)
# ====================================================================================

terraform {
  required_version = ">= 1.5"

  # State stored in the same Azure Blob Storage account as infrastructure-helios.
  # Auth via ARM_* env vars — set locally in ~/.config/fish/conf.d/azure-terraform.fish
  # or injected from GitHub Secrets in CI/CD.
  backend "azurerm" {
    resource_group_name  = "rg-helios-tfstate"
    storage_account_name = "stheliosinfrastate"
    container_name       = "tfstate"
    key                  = "overflow.tfstate"
    use_azuread_auth     = true
    use_cli              = false
  }

  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.32"
    }
    null = {
      source  = "hashicorp/null"
      version = "~> 3.2"
    }
  }
}
provider "kubernetes" {
  config_path = var.kubeconfig_path
}

