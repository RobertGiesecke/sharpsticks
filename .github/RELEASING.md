# Release packages

Push an unprefixed version tag, for example `0.1.0-actions01`, to build a release.
The workflow restores, builds and tests the tagged source in Release configuration,
then packs every packable project in `SharpSticks.slnx`. MinVer receives the exact
tag version, and every generated NuGet manifest is checked against that version.

Packages are retained as a workflow artifact and attached to the GitHub release.
If no release exists, the workflow creates one; tags with a prerelease suffix
create prereleases. Publishing an existing release also triggers the build.
Reruns replace assets of the same name. Packages are not pushed to nuget.org.
The built-in GitHub token needs `contents: write`; no additional secret is required.

Once this workflow is on the default branch, it can also be run manually with an
existing version tag. The tagged commit must contain the workflow and build script.
The checkout includes all history and explicitly selects the requested tag.

For a local build from the repository root (PowerShell 7 and the required .NET
SDKs must be installed):

```powershell
./tools/Build-Release.ps1 -Version 0.1.0-actions01
```

Use a clean `artifacts/release` directory. The script fails on build, test, pack,
or version-validation errors. The runner installs .NET 10 and additionally .NET 11
when the repository's shared build properties target it.
