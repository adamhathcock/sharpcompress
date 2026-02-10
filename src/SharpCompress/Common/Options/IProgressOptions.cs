using System;

namespace SharpCompress.Common.Options;

public interface IProgressOptions
{
    IProgress<ProgressReport>? Progress { get; init; }
}
