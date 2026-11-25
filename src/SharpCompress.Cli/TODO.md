# CLI follow-ups

Use the existing fixtures and known expected values in `tests/SharpCompress.Test/`
to extend `tests/SharpCompress.Cli.Test/`. Keep direct library tests for API
contracts such as stream ownership, forward-only reads, cancellation, and writing.

- [x] Add TAR mode, UID, and GID to detailed listings. Use `Tar.PaxLocalHeader.tar`
  and `Tar.PaxGlobalHeader.tar` to check that local overrides do not leak into
  subsequent entries. Preserve the plain names-only listing.
- [x] Expose ZIP entry comments in detailed listings. Use `Zip.EntryComment.zip`,
  and keep the entry comment distinct from the archive comment in `inspect`.
- [ ] Add `--force-encoding NAME` without changing the fallback behavior of
  `--encoding`. Use `Zip.UnicodePathExtra.zip` to test both decoded names.
- [ ] Assert the exact Unicode archive comment from `Rar5.comment.plain.rar`
  (`123456789012345é`) in `inspect --json`.
- [ ] Add `inspect --parts` to report discovered filenames in order. Use
  `Rar.multi.solid.part01.rar` to test the full six-part set and a directory
  containing only the first part. Keep `Partial` / `MissingVolumes` when parts
  are missing; don't guess missing filenames.
- [ ] Share the fixture list used by the two CLI compatibility matrices and avoid
  rerunning the same basic assertions. Add focused cases instead of replacing
  direct API tests with snapshots.

## Known gaps

- TAR directory modes are not reported. `TarEntry.Mode` adds a legacy `01000`
  directory marker that overlaps the sticky bit. Expose the stored mode before
  enabling this field, and test directories both with and without the sticky bit.
- CLI snapshots normalize timestamps. Add focused tests against known instants
  where the fixture stores one.
- A metadata listing does not establish payload integrity. Consider a read-only
  `verify` command with per-entry hashes checked against known-good payloads.
