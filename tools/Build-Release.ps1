param(
    [Parameter(Mandatory)][string] $Version
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Match the repository's unprefixed MinVer tags, including prerelease versions.
if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$') {
    throw "Not a package version: $Version"
}
function Invoke-DotNet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $args failed ($LASTEXITCODE)" }
}

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    New-Item -ItemType Directory -Force packages/local-feed, artifacts/release | Out-Null
    if (Get-ChildItem artifacts/release -Filter *.nupkg) {
        throw 'artifacts/release must contain no packages before a release build'
    }
    $properties = @('-p:Configuration=Release', "-p:MinVerVersionOverride=$Version", '-p:ContinuousIntegrationBuild=true')
    Invoke-DotNet restore SharpSticks.slnx @properties

    # Generate transitive imports before compilation in a fresh checkout.
    [xml] $solution = Get-Content SharpSticks.slnx
    foreach ($project in $solution.Solution.Project) {
        $evaluation = Invoke-DotNet msbuild $project.Path '-getProperty:TargetFramework,TargetFrameworks' @properties | ConvertFrom-Json
        $frameworks = $evaluation.Properties.TargetFrameworks
        if (!$frameworks) { $frameworks = $evaluation.Properties.TargetFramework }
        foreach ($framework in $frameworks.Split(';', [StringSplitOptions]::RemoveEmptyEntries)) {
            Invoke-DotNet msbuild $project.Path '-t:GenerateBuildTransitiveProps' '-p:BuildProjectReferences=false' "-p:TargetFramework=$framework" @properties '-v:quiet'
        }
    }
    Invoke-DotNet build src/Tests/Tests.csproj --no-restore @properties
    Invoke-DotNet test src/Tests/Tests.csproj --no-build --no-restore @properties
    Invoke-DotNet pack SharpSticks.slnx --no-restore @properties -o artifacts/release

    $packages = @(Get-ChildItem artifacts/release -Filter *.nupkg)
    if ($packages.Count -eq 0) { throw 'No packages were produced' }
    foreach ($package in $packages) {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            $spec = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
            if ($spec.Count -ne 1) { throw "Invalid package: $($package.Name)" }
            $reader = [System.IO.StreamReader]::new($spec[0].Open())
            try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($nuspec.package.metadata.version -ne $Version) {
                throw "Wrong version in $($package.Name): $($nuspec.package.metadata.version)"
            }
        }
        finally { $archive.Dispose() }
    }
    Write-Host "Validated $($packages.Count) packages at version $Version"
}
finally { Pop-Location }
