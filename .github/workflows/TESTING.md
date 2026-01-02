# Testing Guide for NuGet Release Workflow

This document describes how to test the NuGet release workflow.

## Testing Strategy

Since this workflow publishes to NuGet.org and requires repository secrets, testing should be done carefully.

## Pre-Testing Checklist

- [x] Workflow YAML syntax validated
- [x] Version determination logic tested locally
- [x] Version update logic tested locally
- [x] Build script works (`dotnet run --project build/build.csproj`)

## Manual Testing Steps

### 1. Test Prerelease Publishing (Recommended First Test)

This tests the workflow on untagged commits to the release branch.

**Steps:**
1. Ensure `NUGET_API_KEY` secret is configured in repository settings
2. Create a test commit on the `release` branch (e.g., update a comment or README)
3. Push to the `release` branch
4. Monitor the GitHub Actions workflow at: https://github.com/adamhathcock/sharpcompress/actions
5. Verify:
   - Workflow triggers and runs successfully
   - Version is determined correctly (e.g., `0.42.1-preview.XXX`)
   - Build and tests pass
   - Package is uploaded as artifact
   - Package is pushed to NuGet.org as prerelease
   - No GitHub release is created (only for tagged releases)

**Expected Outcome:**
- A new prerelease package appears on NuGet.org: https://www.nuget.org/packages/SharpCompress/
- Package version follows pattern: `{LAST_TAG}-preview.{COMMIT_COUNT}`

### 2. Test Tagged Release Publishing

This tests the workflow when a version tag is pushed.

**Steps:**
1. Prepare the `release` branch with all desired changes
2. Create a version tag (use a test version if possible, e.g., `0.42.2-test`):
   ```bash
   git checkout release
   git tag 0.42.2-test
   git push origin 0.42.2-test
   ```
3. Monitor the GitHub Actions workflow
4. Verify:
   - Workflow triggers and runs successfully
   - Version is determined as the tag (e.g., `0.42.2-test`)
   - Build and tests pass
   - Package is uploaded as artifact
   - Package is pushed to NuGet.org as stable release
   - GitHub release is created with the package attached

**Expected Outcome:**
- A new stable release package appears on NuGet.org
- Package version matches the tag
- A GitHub release is created: https://github.com/adamhathcock/sharpcompress/releases

### 3. Test Duplicate Package Handling

This tests the `--skip-duplicate` flag behavior.

**Steps:**
1. Push to the `release` branch without making changes
2. Monitor the workflow
3. Verify:
   - Workflow runs but NuGet push is skipped with "duplicate" message
   - No errors occur

### 4. Test Build Failure Handling

This tests that failed builds don't publish packages.

**Steps:**
1. Introduce a breaking change in a test or code
2. Push to the `release` branch
3. Verify:
   - Workflow runs and detects the failure
   - Build or test step fails
   - NuGet push step is skipped
   - No package is published

## Verification

After each test, verify:

1. **GitHub Actions Logs**: Check the workflow logs for any errors or warnings
2. **NuGet.org**: Verify the package appears with correct version and metadata
3. **GitHub Releases**: (For tagged releases) Verify the release is created with package attached
4. **Artifacts**: Download and inspect the uploaded artifacts

## Rollback/Cleanup

If testing produces unwanted packages:

1. **Prerelease packages**: Can be unlisted on NuGet.org (Settings → Unlist)
2. **Stable packages**: Cannot be deleted, only unlisted (use test versions)
3. **GitHub Releases**: Can be deleted from GitHub releases page
4. **Tags**: Can be deleted with:
   ```bash
   git tag -d 0.42.2-test
   git push origin :refs/tags/0.42.2-test
   ```

## Known Limitations

- NuGet.org does not allow re-uploading the same version
- Deleted packages on NuGet.org reserve the version number
- The workflow requires the `NUGET_API_KEY` secret to be set

## Success Criteria

The workflow is considered successful if:

- ✅ Prerelease versions are published correctly with preview suffix
- ✅ Tagged versions are published as stable releases
- ✅ GitHub releases are created for tagged versions
- ✅ Build and test failures prevent publishing
- ✅ Duplicate packages are handled gracefully
- ✅ Workflow logs are clear and informative
