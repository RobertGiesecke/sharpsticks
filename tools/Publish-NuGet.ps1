param(
    [Parameter(Mandatory)][string] $Version,
    [Parameter(Mandatory)][string] $PackageDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$env:NUGET_API_KEY) {
    throw 'NuGet authentication is missing. Pass the temporary API key from NuGet/login in the NUGET_API_KEY environment variable.'
}
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$') {
    throw "Invalid package version: $Version"
}
$packages = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter *.nupkg -File)
if ($packages.Count -eq 0) { throw 'No NuGet packages found' }

# Validate the entire batch before publishing any package, including downloaded releases.
foreach ($package in $packages) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $spec = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
        if ($spec.Count -ne 1) { throw "Invalid package: $($package.Name)" }
        $reader = [System.IO.StreamReader]::new($spec[0].Open())
        try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($nuspec.package.metadata.version -ne $Version -or
            $nuspec.package.metadata.id -notlike 'SharpSticks.*') {
            throw "Unexpected package identity or version: $($package.Name)"
        }
    }
    finally { $archive.Dispose() }
}
foreach ($package in $packages) {
    dotnet nuget push $package.FullName --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
    if ($LASTEXITCODE -ne 0) { throw "NuGet publish failed for $($package.Name)" }
}
Write-Host "Published $($packages.Count) packages (existing versions skipped)."
