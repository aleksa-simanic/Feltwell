namespace Feltwell.Api.Models;

public enum BlockStatus
{
    Open,
    Busy,
    Offline
}

public enum HatMaterial
{
    Felt,
    Straw,
    Sinamay
}

public enum HatStyle
{
    Cloche,
    Fedora,
    Beret,
    WideBrim
}

public enum HatJobStatus
{
    Blocked,
    Wired,
    Trimmed,
    Ready,
    Scrapped
}

public sealed record HatBlock(
    Guid Id,
    string Code,
    string Name,
    string Room,
    int HeadSizeCm,
    int MaxActiveHats,
    BlockStatus Status,
    string? Notes,
    DateTimeOffset CreatedAt);

public sealed record HatJob(
    Guid Id,
    Guid BlockId,
    string Label,
    string Client,
    HatMaterial Material,
    HatStyle Style,
    HatJobStatus Status,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateBlockRequest(
    string Code,
    string Name,
    string Room,
    int HeadSizeCm,
    int MaxActiveHats,
    string? Notes);

public sealed record UpdateBlockRequest(
    string? Code,
    string? Name,
    string? Room,
    int? HeadSizeCm,
    int? MaxActiveHats,
    BlockStatus? Status,
    string? Notes);

public sealed record CreateHatJobRequest(
    string Label,
    string Client,
    HatMaterial Material,
    HatStyle Style,
    string? Notes);

public sealed record UpdateHatJobRequest(
    string? Label,
    string? Client,
    HatMaterial? Material,
    HatStyle? Style,
    HatJobStatus? Status,
    string? Notes);

public sealed record BlockListResponse(
    IReadOnlyList<HatBlock> Items,
    int Total,
    int Limit,
    int Offset);

public sealed record HatJobListResponse(
    IReadOnlyList<HatJob> Items,
    int Total);
