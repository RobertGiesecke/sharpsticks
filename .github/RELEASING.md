# Release packages

Push an unprefixed version tag, for example `0.1.0-actions01`, to build a release.
The workflow restores, builds and tests the tagged source in Release configuration,
then packs every packable project in `SharpSticks.slnx`. MinVer receives the exact
tag version, and every generated NuGet manifest is checked against that version.

Packages are retained as a workflow artifact and attached to the GitHub release.
If no release exists, the workflow creates one; tags with a prerelease suffix
create prereleases. Publishing an existing release also triggers the build.
Reruns replace assets of the same name. Packages are also pushed to nuget.org;
already-published versions are skipped. Authentication uses `NuGet/login@v1` and
GitHub OIDC, with `id-token: write` on publishing jobs. No stored API key is needed.
The NuGet username defaults to the package owner `RobertGiesecke`; set the repository
variable `NUGET_USER` to override it. Create NuGet trusted publishing policies for
owner `RobertGiesecke`, repository `sharpsticks`, and workflow filenames
`release-packages.yml` and `publish-nuget.yml` (no directory prefix). Leave the
environment empty, use glob `SharpSticks.*`, and allow new packages and new versions.
The policy's display name is not used by the workflows. Missing trust or failed
pushes fail the job. The built-in GitHub token needs `contents: write` for attachments.

To publish packages from an existing GitHub release without rebuilding or moving
its version tag, use the separate **Publish existing release to NuGet** workflow.
Once it is on the default branch, it supports manual dispatch with a release tag.
It can also be triggered immediately by pushing a `publish-nuget/<version>` tag
pointing to a commit containing that workflow, for example:

```powershell
git tag publish-nuget/0.1.0-actions01 wip/github-actions
git push origin refs/tags/publish-nuget/0.1.0-actions01
```

The workflow downloads the existing release's `.nupkg` assets, verifies their
SharpSticks package IDs and exact version before any push, then publishes them.
The original release tag and package bytes remain unchanged. After creating or
correcting a trusted publishing policy, rerun the failed job.

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

The build compiles and runs the test project before packing the solution. Device-specific
example executables are not package inputs and do not need attached hardware on CI.
For multi-target projects, transitive imports are generated for each framework.
