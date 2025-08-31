# LinkJam Bridge Management Script
param(
    [Parameter(Position=0)]
    [string]$Command = "help"
)

$ErrorActionPreference = "Continue"

# Colors for output
function Write-Success { Write-Host $args -ForegroundColor Green }
function Write-Info { Write-Host $args -ForegroundColor Cyan }
function Write-Warning { Write-Host $args -ForegroundColor Yellow }
function Write-Error { Write-Host $args -ForegroundColor Red }

# Helper functions
function Stop-LinkJamCompanion {
    Write-Info "Stopping Companion..."
    $killed = $false
    
    # Try graceful shutdown first
    Get-Process "LinkJam.Companion" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if ($?) { $killed = $true }
    
    # Kill any dotnet run processes
    Get-Process "dotnet" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -like "*LinkJam.Companion*"
    } | Stop-Process -Force -ErrorAction SilentlyContinue
    if ($?) { $killed = $true }
    
    if ($killed) {
        Write-Success "Companion stopped"
    } else {
        Write-Info "Companion was not running"
    }
}

function Stop-CarabinerProcess {
    Write-Info "Stopping Carabiner..."
    Get-Process "carabiner" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if ($?) {
        Write-Success "Carabiner stopped"
    } else {
        Write-Info "Carabiner was not running"
    }
}

function Stop-AuthorityServer {
    Write-Info "Stopping Authority server..."
    Get-Process "node" -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -like "*linkjam-bridge*"
    } | Stop-Process -Force -ErrorAction SilentlyContinue
    if ($?) {
        Write-Success "Authority stopped"
    } else {
        Write-Info "Authority was not running"
    }
}

# Main commands
switch ($Command.ToLower()) {
    "help" {
        Write-Info @"

LinkJam Bridge Manager
======================

USAGE: .\linkjam.ps1 [command]

COMMANDS:
  start         - Start all components (Authority, Carabiner, Companion)
  stop          - Stop all components
  restart       - Restart all components
  
  authority     - Start Authority server only
  companion     - Start Companion app only  
  carabiner     - Start Carabiner only
  
  build         - Build all components
  install       - Install dependencies
  download      - Download Carabiner executable
  
  status        - Show status of all components
  clean         - Clean build artifacts
  help          - Show this help

EXAMPLES:
  .\linkjam.ps1 start      # Start everything
  .\linkjam.ps1 stop       # Stop everything
  .\linkjam.ps1 companion  # Start just Companion
  .\linkjam.ps1 status     # Check what's running

"@
    }
    
    "start" {
        Write-Info "Starting LinkJam Bridge..."
        
        # Start Authority
        Write-Info "Starting Authority server..."
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd authority; npm run dev" -PassThru | Out-Null
        Start-Sleep -Seconds 2
        
        # Start Carabiner
        Write-Info "Starting Carabiner..."
        $carabinerPath = Join-Path $PSScriptRoot "companion\LinkJam.Companion\ThirdParty\carabiner.exe"
        if (Test-Path $carabinerPath) {
            Start-Process $carabinerPath -ArgumentList "--port 17000" -PassThru | Out-Null
        } else {
            Write-Warning "Carabiner not found. Run: .\linkjam.ps1 download"
        }
        Start-Sleep -Seconds 2
        
        # Start Companion
        Write-Info "Starting Companion..."
        $companionExe = Join-Path $PSScriptRoot "companion\LinkJam.Companion\bin\Debug\net8.0-windows\LinkJam.Companion.exe"
        if (Test-Path $companionExe) {
            Start-Process $companionExe -PassThru | Out-Null
        } else {
            Write-Warning "Companion not built. Run: .\linkjam.ps1 build"
        }
        
        Write-Success "`nAll components started!"
        Write-Info "Authority: http://localhost:3000"
        Write-Info "Carabiner: Port 17000"
        Write-Info "Companion: Check system tray"
    }
    
    "stop" {
        Stop-LinkJamCompanion
        Stop-CarabinerProcess
        Stop-AuthorityServer
        Write-Success "All components stopped"
    }
    
    "restart" {
        & $PSCommandPath stop
        Start-Sleep -Seconds 2
        & $PSCommandPath start
    }
    
    "authority" {
        Write-Info "Starting Authority server..."
        Set-Location (Join-Path $PSScriptRoot "authority")
        npm run dev
    }
    
    "companion" {
        Stop-LinkJamCompanion
        Write-Info "Starting Companion..."
        $exe = Join-Path $PSScriptRoot "companion\LinkJam.Companion\bin\Debug\net8.0-windows\LinkJam.Companion.exe"
        if (Test-Path $exe) {
            & $exe
        } else {
            Write-Error "Companion not built. Run: .\linkjam.ps1 build"
        }
    }
    
    "carabiner" {
        Write-Info "Starting Carabiner..."
        $carabinerPath = Join-Path $PSScriptRoot "companion\LinkJam.Companion\ThirdParty\carabiner.exe"
        if (Test-Path $carabinerPath) {
            & $carabinerPath --port 17000
        } else {
            Write-Error "Carabiner not found. Run: .\linkjam.ps1 download"
        }
    }
    
    "build" {
        Write-Info "Building Authority..."
        Set-Location (Join-Path $PSScriptRoot "authority")
        npm install
        npm run build
        
        Write-Info "`nBuilding Companion..."
        Set-Location (Join-Path $PSScriptRoot "companion")
        dotnet build
        
        Write-Success "`nBuild complete!"
    }
    
    "install" {
        Write-Info "Installing Authority dependencies..."
        Set-Location (Join-Path $PSScriptRoot "authority")
        npm install
        
        Write-Info "`nRestoring Companion packages..."
        Set-Location (Join-Path $PSScriptRoot "companion")
        dotnet restore
        
        Write-Success "`nDependencies installed!"
    }
    
    "download" {
        Write-Info "Downloading Carabiner..."
        
        $thirdPartyPath = Join-Path $PSScriptRoot "companion\LinkJam.Companion\ThirdParty"
        $carabinerExePath = Join-Path $thirdPartyPath "carabiner.exe"
        
        if (Test-Path $carabinerExePath) {
            Write-Warning "Carabiner already exists"
            $response = Read-Host "Re-download? (y/n)"
            if ($response -ne 'y') { exit }
        }
        
        if (!(Test-Path $thirdPartyPath)) {
            New-Item -ItemType Directory -Path $thirdPartyPath -Force | Out-Null
        }
        
        try {
            Write-Info "Fetching latest release info..."
            $release = Invoke-RestMethod -Uri "https://api.github.com/repos/Deep-Symmetry/carabiner/releases/latest"
            
            Write-Info "Latest version: $($release.tag_name)"
            Write-Info "Available assets:"
            $release.assets | ForEach-Object { Write-Info "  - $($_.name)" }
            
            # Try different naming patterns
            $asset = $release.assets | Where-Object { 
                $_.name -like "*windows*" -or 
                $_.name -like "*win*" -or 
                $_.name -like "*Win*" -or
                $_.name -match "carabiner.*\.zip$"
            } | Select-Object -First 1
            
            if ($asset) {
                $tempZip = Join-Path $env:TEMP "carabiner-download.zip"
                Write-Info "Downloading: $($asset.name)"
                Write-Info "URL: $($asset.browser_download_url)"
                
                # Download with progress
                $ProgressPreference = 'SilentlyContinue'
                Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $tempZip
                $ProgressPreference = 'Continue'
                
                Write-Info "Extracting..."
                Expand-Archive -Path $tempZip -DestinationPath $thirdPartyPath -Force
                Remove-Item $tempZip
                
                # Check if carabiner.exe exists after extraction
                if (Test-Path $carabinerExePath) {
                    Write-Success "Carabiner downloaded successfully!"
                } else {
                    Write-Warning "Extracted but carabiner.exe not found in expected location"
                    Write-Info "Contents of ThirdParty folder:"
                    Get-ChildItem $thirdPartyPath -Recurse | ForEach-Object { Write-Info "  $($_.FullName)" }
                }
            } else {
                Write-Error "Could not find Windows release in assets"
                Write-Warning "You can manually download from:"
                Write-Warning "https://github.com/Deep-Symmetry/carabiner/releases"
                Write-Warning "Then place carabiner.exe in:"
                Write-Warning "  $thirdPartyPath"
            }
        } catch {
            Write-Error "Download failed: $_"
            Write-Warning "Try manual download from:"
            Write-Warning "https://github.com/Deep-Symmetry/carabiner/releases"
        }
    }
    
    "status" {
        Write-Info "LinkJam Bridge Status"
        Write-Info "===================="
        
        # Check Authority
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:3000/health" -TimeoutSec 1 -ErrorAction SilentlyContinue
            Write-Success "Authority: RUNNING (http://localhost:3000)"
        } catch {
            Write-Warning "Authority: NOT RUNNING"
        }
        
        # Check Carabiner
        if (Get-Process "carabiner" -ErrorAction SilentlyContinue) {
            Write-Success "Carabiner: RUNNING (port 17000)"
        } else {
            Write-Warning "Carabiner: NOT RUNNING"
        }
        
        # Check Companion
        if (Get-Process "LinkJam.Companion" -ErrorAction SilentlyContinue) {
            Write-Success "Companion: RUNNING (check system tray)"
        } else {
            Write-Warning "Companion: NOT RUNNING"
        }
        
        # Check rekordbox
        if (Get-Process "rekordbox" -ErrorAction SilentlyContinue) {
            Write-Info "rekordbox: RUNNING"
        } else {
            Write-Info "rekordbox: NOT RUNNING"
        }
    }
    
    "clean" {
        Write-Info "Cleaning build artifacts..."
        
        # Clean Authority
        $authDist = Join-Path $PSScriptRoot "authority\dist"
        if (Test-Path $authDist) {
            Remove-Item $authDist -Recurse -Force
            Write-Info "Cleaned Authority dist"
        }
        
        # Clean Companion
        $companionBin = Join-Path $PSScriptRoot "companion\LinkJam.Companion\bin"
        $companionObj = Join-Path $PSScriptRoot "companion\LinkJam.Companion\obj"
        if (Test-Path $companionBin) {
            Remove-Item $companionBin -Recurse -Force
            Write-Info "Cleaned Companion bin"
        }
        if (Test-Path $companionObj) {
            Remove-Item $companionObj -Recurse -Force
            Write-Info "Cleaned Companion obj"
        }
        
        Write-Success "Clean complete!"
    }
    
    default {
        Write-Error "Unknown command: $Command"
        Write-Info "Run: .\linkjam.ps1 help"
    }
}