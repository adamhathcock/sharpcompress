# NuGet Release Workflow

This document describes the automated NuGet release workflow for SharpCompress.

## Overview

The `nuget-release.yml` workflow automatically builds, tests, and publishes SharpCompress packages to NuGet.org when changes are pushed to the `release` branch.

## How It Works

### Version Determination

The workflow automatically determines the version based on whether the commit is tagged:

1. **Tagged Release (Stable)**:
   - If the current commit on the `release` branch has a version tag (e.g., `0.42.1`)
   - Uses the tag as the version number
   - Published as a stable release
   - Creates a GitHub Release with the package attached

2. **Untagged Release (Prerelease)**:
   - If the current commit is NOT tagged
   - Creates a prerelease version based on the last tag
   - Format: `{LAST_TAG}-preview.{COMMIT_COUNT}+{SHORT_SHA}`
   - Example: `0.42.1-preview.123+abc1234`
   - Published as a prerelease to NuGet.org

### Workflow Steps

1. **Checkout**: Fetches the repository with full history for version detection
2. **Setup .NET**: Installs .NET 10.0
3. **Determine Version**: Checks for tags and determines version
4. **Update Version**: Updates the version in the project file
5. **Build and Test**: Runs the full build and test suite
6. **Upload Artifacts**: Uploads the generated `.nupkg` files as workflow artifacts
7. **Push to NuGet**: Publishes the package to NuGet.org using the API key
8. **Create GitHub Release**: (Only for tagged releases) Creates a GitHub release with the package

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

1. Ensure all changes are merged and tested on the `release` branch
2. Create and push a version tag:
   ```bash
   git checkout release
   git tag 0.43.0
   git push origin 0.43.0
   ```
3. The workflow will automatically:
   - Build and test the project
   - Publish `SharpCompress 0.43.0` to NuGet.org
   - Create a GitHub Release

### Creating a Prerelease

1. Push changes to the `release` branch without tagging:
   ```bash
   git checkout release
   git push origin release
   ```
2. The workflow will automatically:
   - Build and test the project
   - Publish a prerelease version like `0.42.1-preview.456+abc1234` to NuGet.org

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

## Related Files

- `.github/workflows/nuget-release.yml` - The workflow definition
- `src/SharpCompress/SharpCompress.csproj` - Project file with version information
- `build/Program.cs` - Build script that creates the NuGet package
