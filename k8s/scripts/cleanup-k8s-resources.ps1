# Kubernetes Resource Cleanup Script (PowerShell Wrapper)
# Safely removes old/unused resources like secrets, configmaps, and replicasets
# Usage: .\cleanup-k8s-resources.ps1 -Namespace <namespace> [-DryRun]

param(
    [Parameter(Mandatory=$true)]
    [string]$Namespace,
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Check if running in WSL or Git Bash environment
$bashScript = Join-Path $PSScriptRoot "cleanup-k8s-resources.sh"

if (-not (Test-Path $bashScript)) {
    Write-Host "❌ Error: cleanup-k8s-resources.sh not found" -ForegroundColor Red
    Write-Host "Expected location: $bashScript" -ForegroundColor Yellow
    exit 1
}

# Check for WSL
$hasWSL = Get-Command wsl -ErrorAction SilentlyContinue

# Check for Git Bash
$gitBash = "C:\Program Files\Git\bin\bash.exe"
$hasGitBash = Test-Path $gitBash

if ($hasWSL) {
    Write-Host "🐧 Using WSL to run cleanup script..." -ForegroundColor Cyan
    
    # Convert Windows path to WSL path
    $wslPath = wsl wslpath "'$bashScript'"
    
    if ($DryRun) {
        wsl bash "$wslPath" "$Namespace" "--dry-run"
    } else {
        wsl bash "$wslPath" "$Namespace"
    }
} elseif ($hasGitBash) {
    Write-Host "🦊 Using Git Bash to run cleanup script..." -ForegroundColor Cyan
    
    # Convert Windows path to Unix-style path for Git Bash
    $unixPath = $bashScript -replace '\\', '/' -replace '^([A-Z]):', '/$1'
    
    if ($DryRun) {
        & $gitBash -c "$unixPath $Namespace --dry-run"
    } else {
        & $gitBash -c "$unixPath $Namespace"
    }
} else {
    Write-Host "❌ Error: Neither WSL nor Git Bash found" -ForegroundColor Red
    Write-Host "" 
    Write-Host "Please install one of the following:" -ForegroundColor Yellow
    Write-Host "  1. WSL (Windows Subsystem for Linux)" -ForegroundColor White
    Write-Host "     Install: wsl --install" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  2. Git for Windows (includes Git Bash)" -ForegroundColor White
    Write-Host "     Download: https://git-scm.com/download/win" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

