# NuGet Release Workflow

This document describes the automated NuGet release workflow for SharpCompress.

## Overview

The `nuget-release.yml` workflow automatically builds, tests, and publishes SharpCompress packages to NuGet.org when changes are pushed to the `master` or `release` branch. The workflow runs on both Windows and Ubuntu, but only the Windows build publishes to NuGet.

## How It Works

### Version Determination

The workflow automatically determines the version based on whether the commit is tagged using C# code in the build project:

1. **Tagged Release (Stable)**:
   - If the current commit has a version tag (e.g., `0.42.1`)
   - Uses the tag as the version number
   - Published as a stable release
   - Creates a GitHub Release with the package attached (Windows build only)

2. **Untagged Release (Prerelease)**:
   - If the current commit is NOT tagged
   - Creates a prerelease version based on the next minor version
   - Format: `{NEXT_MINOR_VERSION}-beta.{COMMIT_COUNT}`
   - Example: `0.43.0-beta.123` (if last tag is 0.42.x)
   - Published as a prerelease to NuGet.org (Windows build only)

### Workflow Steps

The workflow runs on a matrix of operating systems (Windows and Ubuntu):

1. **Checkout**: Fetches the repository with full history for version detection
2. **Setup .NET**: Installs .NET 10.0
3. **Determine Version**: Runs `determine-version` build target to check for tags and determine version
4. **Update Version**: Runs `update-version` build target to update the version in the project file
5. **Build and Test**: Runs the full build and test suite on both platforms
6. **Upload Artifacts**: Uploads the generated `.nupkg` files as workflow artifacts (separate for each OS)
7. **Push to NuGet**: (Windows only) Runs `push-to-nuget` build target to publish the package to NuGet.org using the API key
8. **Create GitHub Release**: (Windows only, tagged releases only) Runs `create-release` build target to create a GitHub release with the package

All version detection, file updates, and publishing logic is implemented in C# in the `build/Program.cs` file using build targets.

## Setup Requirements

### 1. NuGet API Key Secret

The workflow requires a `NUGET_API_KEY` secret to be configured in the repository settings:

1. Go to https://www.nuget.org/account/apikeys
2. Create a new API key with "Push" permission for the SharpCompress package
3. In GitHub, go to: **Settings** → **Secrets and variables** → **Actions**
4. Create a new secret named `NUGET_API_KEY` with the API key value

### 2. Branch Protection (Recommended)

Consider enabling branch protection rules for the `release` branch to ensure:
- Code reviews are required before merging
- Status checks pass before merging
- Only authorized users can push to the branch

## Usage

### Creating a Stable Release

1. Ensure all changes are merged and tested on the `master` or `release` branch
2. Create and push a version tag:
   ```bash
   git checkout master  # or release
   git tag 0.43.0
   git push origin 0.43.0
   ```
3. The workflow will automatically:
   - Build and test the project on both Windows and Ubuntu
   - Publish `SharpCompress 0.43.0` to NuGet.org (Windows build)
   - Create a GitHub Release (Windows build)

### Creating a Prerelease

1. Push changes to the `master` or `release` branch without tagging:
   ```bash
   git checkout master  # or release
   git push origin master  # or release
   ```
2. The workflow will automatically:
   - Build and test the project on both Windows and Ubuntu
   - Publish a prerelease version like `0.43.0-beta.456` to NuGet.org (Windows build)

## Troubleshooting

### Workflow Fails to Push to NuGet

- **Check the API Key**: Ensure `NUGET_API_KEY` is set correctly in repository secrets
- **Check API Key Permissions**: Verify the API key has "Push" permission for SharpCompress
- **Check API Key Expiration**: NuGet API keys may expire; create a new one if needed

### Version Conflict

If you see "Package already exists" errors:
- The workflow uses `--skip-duplicate` flag to handle this gracefully
- If you need to republish the same version, delete it from NuGet.org first (if allowed)

### Build or Test Failures

- The workflow will not push to NuGet if build or tests fail
- Check the workflow logs in GitHub Actions for details
- Fix the issues and push again

## Manual Package Creation

If you need to create a package manually without publishing:

```bash
dotnet run --project build/build.csproj -- publish
```

The package will be created in the `artifacts/` directory.

## Build Targets

The workflow uses the following C# build targets defined in `build/Program.cs`:

- **determine-version**: Detects version from git tags and outputs VERSION and PRERELEASE variables
- **update-version**: Updates VersionPrefix, AssemblyVersion, and FileVersion in the project file
- **push-to-nuget**: Pushes the generated NuGet packages to NuGet.org (requires NUGET_API_KEY)
- **create-release**: Creates a GitHub release with the packages attached (requires GITHUB_TOKEN)

These targets can be run manually for testing:

```bash
# Determine the version
dotnet run --project build/build.csproj -- determine-version

# Update version in project file
VERSION=0.43.0 dotnet run --project build/build.csproj -- update-version

# Push to NuGet (requires NUGET_API_KEY environment variable)
NUGET_API_KEY=your-key dotnet run --project build/build.csproj -- push-to-nuget

# Create GitHub release (requires GITHUB_TOKEN and VERSION environment variables)
GITHUB_TOKEN=your-token VERSION=0.43.0 PRERELEASE=false dotnet run --project build/build.csproj -- create-release
```

## Related Files

- `.github/workflows/nuget-release.yml` - The workflow definition
- `build/Program.cs` - Build script with version detection and publishing logic
- `src/SharpCompress/SharpCompress.csproj` - Project file with version information
