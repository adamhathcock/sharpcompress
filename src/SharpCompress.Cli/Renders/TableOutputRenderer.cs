using System.Globalization;
using SharpCompress.Cli.Formats;
using SharpCompress.Cli.Inspection;
using Spectre.Console;

namespace SharpCompress.Cli.Renders;

internal static class TableOutputRenderer
{
    public static void RenderInspect(InspectionExecutionResult report, bool longOutput)
    {
        foreach (var archive in report.Archives)
        {
            AnsiConsole.Write(new Rule(Markup.Escape(archive.ArchivePath)));
            RenderSummaryTable(archive);
            RenderEntryTable(archive, longOutput);
            AnsiConsole.WriteLine();
        }

        RenderErrors(report);
    }

    public static void RenderList(InspectionExecutionResult report, bool longOutput)
    {
        foreach (var archive in report.Archives)
        {
            AnsiConsole.Write(new Rule(Markup.Escape(archive.ArchivePath)));
            RenderEntryTable(archive, longOutput);
            AnsiConsole.WriteLine();
        }

        RenderErrors(report);
    }

    public static void RenderFormats(
        System.Collections.Generic.IReadOnlyList<FormatSupportResult> formats
    )
    {
        var table = new Table().RoundedBorder();
        table.AddColumn("Format");
        table.AddColumn("ArchiveType");
        table.AddColumn("Extensions");
        table.AddColumn("Forward");
        table.AddColumn("Seekable");

        foreach (var format in formats)
        {
            table.AddRow(
                Markup.Escape(format.Name),
                Markup.Escape(format.ArchiveType),
                Markup.Escape(string.Join(", ", format.Extensions)),
                format.SupportsForward ? "yes" : "no",
                format.SupportsSeekable ? "yes" : "no"
            );
        }

        AnsiConsole.Write(table);
    }

    private static void RenderSummaryTable(ArchiveInspectionResult archive)
    {
        var table = new Table().RoundedBorder().HideHeaders();
        table.AddColumn("Key");
        table.AddColumn("Value");

        table.AddRow("archive-type", Markup.Escape(archive.ArchiveType));
        table.AddRow("access-requested", archive.RequestedAccessMode.ToString().ToLowerInvariant());
        table.AddRow("access-used", archive.UsedAccessMode.ToString().ToLowerInvariant());
        table.AddRow("entries", archive.EntryCount.ToString(CultureInfo.InvariantCulture));
        table.AddRow(
            "displayed",
            archive.DisplayedEntryCount.ToString(CultureInfo.InvariantCulture)
        );
        table.AddRow("volumes", archive.VolumeCount.ToString(CultureInfo.InvariantCulture));
        table.AddRow("compressed-size", FormatSize(archive.TotalCompressedSize));
        table.AddRow("uncompressed-size", FormatSize(archive.TotalUncompressedSize));
        table.AddRow("encrypted", archive.IsEncrypted ? "yes" : "no");
        table.AddRow("solid", archive.IsSolid ? "yes" : "no");
        if (archive.IsComplete.HasValue)
        {
            table.AddRow("complete", archive.IsComplete.Value ? "yes" : "no");
        }
        if (archive.AutoFallbackApplied)
        {
            table.AddRow(
                "fallback",
                Markup.Escape(archive.FallbackReason ?? "seekable fallback applied")
            );
        }
        if (archive.OutputTruncated)
        {
            table.AddRow("truncated", "yes");
        }

        AnsiConsole.Write(table);
    }

    private static void RenderEntryTable(ArchiveInspectionResult archive, bool longOutput)
    {
        var table = new Table().RoundedBorder();
        table.AddColumn("Entry");
        table.AddColumn("Compression");
        table.AddColumn("Size");
        table.AddColumn("Packed");
        table.AddColumn("Dir");
        table.AddColumn("Enc");

        if (longOutput)
        {
            table.AddColumn("Modified");
            table.AddColumn("Split");
            table.AddColumn("Volumes");
            table.AddColumn("Link");
        }

        foreach (var entry in archive.Entries)
        {
            if (longOutput)
            {
                table.AddRow(
                    Markup.Escape(entry.Key),
                    Markup.Escape(entry.CompressionType.ToString()),
                    FormatSize(entry.Size),
                    FormatSize(entry.CompressedSize),
                    entry.IsDirectory ? "yes" : "no",
                    entry.IsEncrypted ? "yes" : "no",
                    entry.LastModifiedTime?.ToString("u", CultureInfo.InvariantCulture) ?? "",
                    entry.IsSplitAfter ? "yes" : "no",
                    $"{entry.VolumeIndexFirst}-{entry.VolumeIndexLast}",
                    Markup.Escape(entry.LinkTarget ?? string.Empty)
                );
            }
            else
            {
                table.AddRow(
                    Markup.Escape(entry.Key),
                    Markup.Escape(entry.CompressionType.ToString()),
                    FormatSize(entry.Size),
                    FormatSize(entry.CompressedSize),
                    entry.IsDirectory ? "yes" : "no",
                    entry.IsEncrypted ? "yes" : "no"
                );
            }
        }

        AnsiConsole.Write(table);
    }

    private static void RenderErrors(InspectionExecutionResult report)
    {
        foreach (var error in report.Errors)
        {
            AnsiConsole.MarkupLine(
                $"[red]error[/] {Markup.Escape(error.ArchivePath)}: {Markup.Escape(error.Message)}"
            );
        }
    }

    private static string FormatSize(long value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);
}
