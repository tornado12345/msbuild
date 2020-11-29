[CmdletBinding(PositionalBinding=$false)]
Param(
  [Parameter(Mandatory = $true)]
  [string] $destination,
  [ValidateSet('Debug','Release')]
  [string] $configuration = "Debug",
  [ValidateSet('Core','Desktop')]
  [string] $runtime = "Desktop"
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

function Copy-WithBackup ($origin) {
    $directoryPart = Join-Path -Path $destination $origin.IntermediaryDirectories 
    $destinationPath = Join-Path -Path $directoryPart (Split-Path $origin.SourceFile -leaf)

    if (Test-Path $destinationPath -PathType Leaf) {
        # Back up previous copy of the file
        Copy-Item $destinationPath $BackupFolder -ErrorAction Stop
    }

    if (!(Test-Path $directoryPart)) {
        [system.io.directory]::CreateDirectory($directoryPart)
    }

    Copy-Item $origin.SourceFile $destinationPath -ErrorAction Stop

    echo "Copied $($origin.SourceFile) to $destinationPath"
}

function FileToCopy([string] $sourceFileRelativeToRepoRoot, [string] $intermediaryDirectories)
{
    return [PSCustomObject]@{"SourceFile"=$([IO.Path]::Combine($PSScriptRoot, "..", $sourceFileRelativeToRepoRoot)); "IntermediaryDirectories"=$intermediaryDirectories}
}

# TODO: find destination in PATH if not specified

# TODO: identify processes likely to be using target MSBuild and warn/offer to kill

# TODO: find most-recently-built MSBuild and make it default $configuration

$BackupFolder = New-Item (Join-Path $destination -ChildPath "Backup-$(Get-Date -Format FileDateTime)") -itemType directory -ErrorAction Stop

Write-Verbose "Copying $configuration MSBuild to $destination"
Write-Host "Existing MSBuild assemblies backed up to $BackupFolder"

if ($runtime -eq "Desktop") {
    $targetFramework = "net472"
} else {
    $targetFramework = "netcoreapp2.1"
}

$bootstrapBinDirectory = "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework"

$filesToCopyToBin = @(
    FileToCopy "$bootstrapBinDirectory\Microsoft.Build.dll"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Build.Framework.dll"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Build.Tasks.Core.dll"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Build.Utilities.Core.dll"

    FileToCopy "$bootstrapBinDirectory\en\Microsoft.Build.resources.dll" "en"
    FileToCopy "$bootstrapBinDirectory\en\Microsoft.Build.Tasks.Core.resources.dll" "en"
    FileToCopy "$bootstrapBinDirectory\en\Microsoft.Build.Utilities.Core.resources.dll" "en"
    FileToCopy "$bootstrapBinDirectory\en\MSBuild.resources.dll" "en"

    FileToCopy "$bootstrapBinDirectory\Microsoft.Common.CrossTargeting.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Common.CurrentVersion.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Common.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.CSharp.CrossTargeting.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.CSharp.CurrentVersion.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.CSharp.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Managed.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Managed.Before.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Managed.After.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Net.props"
    FileToCopy "$bootstrapBinDirectory\Microsoft.NetFramework.CurrentVersion.props"
    FileToCopy "$bootstrapBinDirectory\Microsoft.NetFramework.CurrentVersion.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.NetFramework.props"
    FileToCopy "$bootstrapBinDirectory\Microsoft.NetFramework.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.VisualBasic.CrossTargeting.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.VisualBasic.CurrentVersion.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.VisualBasic.targets"
)

if ($runtime -eq "Desktop") {
    $runtimeSpecificFiles = @(
        FileToCopy "$bootstrapBinDirectory\MSBuild.exe"
        FileToCopy "artifacts\bin\Microsoft.Build.Conversion\$configuration\$targetFramework\Microsoft.Build.Conversion.Core.dll"
        FileToCopy "artifacts\bin\Microsoft.Build.Engine\$configuration\$targetFramework\Microsoft.Build.Engine.dll"

        FileToCopy "artifacts\bin\MSBuildTaskHost\$configuration\net35\MSBuildTaskHost.exe"
        FileToCopy "artifacts\bin\MSBuildTaskHost\$configuration\net35\MSBuildTaskHost.pdb"

        FileToCopy "$bootstrapBinDirectory\Microsoft.Data.Entity.targets"
        FileToCopy "$bootstrapBinDirectory\Microsoft.ServiceModel.targets"
        FileToCopy "$bootstrapBinDirectory\Microsoft.WinFx.targets"
        FileToCopy "$bootstrapBinDirectory\Microsoft.WorkflowBuildExtensions.targets"
        FileToCopy "$bootstrapBinDirectory\Microsoft.Xaml.targets"
        FileToCopy "$bootstrapBinDirectory\Workflow.targets"
        FileToCopy "$bootstrapBinDirectory\Workflow.VisualBasic.targets"
    )
} else {
    $runtimeSpecificFiles = @(
        FileToCopy "$bootstrapBinDirectory\MSBuild.dll"
    )
}

$filesToCopyToBin += $runtimeSpecificFiles

foreach ($file in $filesToCopyToBin) {
    Copy-WithBackup $file
}

Write-Host -ForegroundColor Green "Copy succeeded"
Write-Verbose "Run $destination\MSBuild.exe"
