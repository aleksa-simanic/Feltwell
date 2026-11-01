using Feltwell.Api.Errors;
using Feltwell.Api.Models;

namespace Feltwell.Api.Repositories;

public interface IBlockStore
{
    HatBlock AddBlock(CreateBlockRequest request);
    HatBlock? GetBlock(Guid id);
    (IReadOnlyList<HatBlock> Items, int Total) ListBlocks(
        BlockStatus? status,
        string? room,
        string? q,
        int limit,
        int offset);
    HatBlock? UpdateBlock(Guid id, UpdateBlockRequest request);
    bool DeleteBlock(Guid id);

    HatJob AddHatJob(Guid blockId, CreateHatJobRequest request);
    (IReadOnlyList<HatJob> Items, int Total) ListHatJobs(Guid blockId, HatJobStatus? status);
    HatJob? GetHatJob(Guid blockId, Guid jobId);
    HatJob? UpdateHatJob(Guid blockId, Guid jobId, UpdateHatJobRequest request);
    void SeedDemo();
    void Clear();
}

public sealed class InMemoryBlockStore : IBlockStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, HatBlock> _blocks = new();
    private readonly Dictionary<Guid, HatJob> _jobs = new();

    public HatBlock AddBlock(CreateBlockRequest request)
    {
        ValidateBlockInput(request.Code, request.Name, request.Room, request.HeadSizeCm, request.MaxActiveHats, request.Notes);

        lock (_gate)
        {
            if (_blocks.Values.Any(b =>
                    string.Equals(b.Code, request.Code.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException($"Block code '{request.Code.Trim()}' already exists.");
            }

            var block = new HatBlock(
                Guid.NewGuid(),
                request.Code.Trim().ToUpperInvariant(),
                request.Name.Trim(),
                request.Room.Trim(),
                request.HeadSizeCm,
                request.MaxActiveHats,
                BlockStatus.Open,
                NormalizeOptional(request.Notes),
                DateTimeOffset.UtcNow);

            _blocks[block.Id] = block;
            return block;
        }
    }

    public HatBlock? GetBlock(Guid id)
    {
        lock (_gate)
        {
            return _blocks.TryGetValue(id, out var block) ? block : null;
        }
    }

    public (IReadOnlyList<HatBlock> Items, int Total) ListBlocks(
        BlockStatus? status,
        string? room,
        string? q,
        int limit,
        int offset)
    {
        lock (_gate)
        {
            IEnumerable<HatBlock> query = _blocks.Values;

            if (status is not null)
            {
                query = query.Where(b => b.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(room))
            {
                var needle = room.Trim();
                query = query.Where(b => b.Room.Contains(needle, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var needle = q.Trim();
                query = query.Where(b =>
                    b.Code.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || b.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || b.Room.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || (b.Notes?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var filtered = query
                .OrderBy(b => b.Room, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return (filtered.Skip(offset).Take(limit).ToList(), filtered.Count);
        }
    }

    public HatBlock? UpdateBlock(Guid id, UpdateBlockRequest request)
    {
        lock (_gate)
        {
            if (!_blocks.TryGetValue(id, out var existing))
            {
                return null;
            }

            var code = request.Code?.Trim().ToUpperInvariant() ?? existing.Code;
            var name = request.Name?.Trim() ?? existing.Name;
            var room = request.Room?.Trim() ?? existing.Room;
            var headSize = request.HeadSizeCm ?? existing.HeadSizeCm;
            var maxActive = request.MaxActiveHats ?? existing.MaxActiveHats;
            var notes = request.Notes is null ? existing.Notes : NormalizeOptional(request.Notes);
            var status = request.Status ?? existing.Status;

            ValidateBlockInput(code, name, room, headSize, maxActive, notes);

            if (_blocks.Values.Any(b =>
                    b.Id != id
                    && string.Equals(b.Code, code, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException($"Block code '{code}' already exists.");
            }

            var active = ActiveJobCount(id, excludeJobId: null);
            if (active > maxActive)
            {
                throw new ConflictException(
                    $"Block already has {active} active hat jobs (capacity {maxActive}).");
            }

            var updated = existing with
            {
                Code = code,
                Name = name,
                Room = room,
                HeadSizeCm = headSize,
                MaxActiveHats = maxActive,
                Status = status,
                Notes = notes
            };

            _blocks[id] = updated;
            if (status is not BlockStatus.Offline)
            {
                SyncBlockStatus(id);
            }

            return _blocks[id];
        }
    }

    public bool DeleteBlock(Guid id)
    {
        lock (_gate)
        {
            if (!_blocks.Remove(id))
            {
                return false;
            }

            foreach (var jobId in _jobs.Values.Where(j => j.BlockId == id).Select(j => j.Id).ToList())
            {
                _jobs.Remove(jobId);
            }

            return true;
        }
    }

    public HatJob AddHatJob(Guid blockId, CreateHatJobRequest request)
    {
        ValidateJobInput(request.Label, request.Client, request.Notes);

        lock (_gate)
        {
            if (!_blocks.TryGetValue(blockId, out var block))
            {
                throw new NotFoundException($"Block {blockId} not found.");
            }

            if (block.Status is BlockStatus.Offline)
            {
                throw new ConflictException($"Block {block.Code} is offline.");
            }

            EnsureCapacity(blockId, block.MaxActiveHats, excludeJobId: null);

            var now = DateTimeOffset.UtcNow;
            var job = new HatJob(
                Guid.NewGuid(),
                blockId,
                request.Label.Trim(),
                request.Client.Trim(),
                request.Material,
                request.Style,
                HatJobStatus.Blocked,
                NormalizeOptional(request.Notes),
                now,
                now);

            _jobs[job.Id] = job;
            SyncBlockStatus(blockId);
            return job;
        }
    }

    public (IReadOnlyList<HatJob> Items, int Total) ListHatJobs(Guid blockId, HatJobStatus? status)
    {
        lock (_gate)
        {
            if (!_blocks.ContainsKey(blockId))
            {
                throw new NotFoundException($"Block {blockId} not found.");
            }

            IEnumerable<HatJob> query = _jobs.Values.Where(j => j.BlockId == blockId);
            if (status is not null)
            {
                query = query.Where(j => j.Status == status);
            }

            var items = query
                .OrderBy(j => j.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return (items, items.Count);
        }
    }

    public HatJob? GetHatJob(Guid blockId, Guid jobId)
    {
        lock (_gate)
        {
            if (!_blocks.ContainsKey(blockId))
            {
                throw new NotFoundException($"Block {blockId} not found.");
            }

            if (!_jobs.TryGetValue(jobId, out var job) || job.BlockId != blockId)
            {
                return null;
            }

            return job;
        }
    }

    public HatJob? UpdateHatJob(Guid blockId, Guid jobId, UpdateHatJobRequest request)
    {
        lock (_gate)
        {
            if (!_blocks.TryGetValue(blockId, out var block))
            {
                throw new NotFoundException($"Block {blockId} not found.");
            }

            if (!_jobs.TryGetValue(jobId, out var existing) || existing.BlockId != blockId)
            {
                return null;
            }

            var label = request.Label?.Trim() ?? existing.Label;
            var client = request.Client?.Trim() ?? existing.Client;
            var material = request.Material ?? existing.Material;
            var style = request.Style ?? existing.Style;
            var notes = request.Notes is null ? existing.Notes : NormalizeOptional(request.Notes);
            var status = request.Status ?? existing.Status;

            ValidateJobInput(label, client, notes);

            if (IsActive(status))
            {
                if (block.Status is BlockStatus.Offline)
                {
                    throw new ConflictException($"Block {block.Code} is offline.");
                }

                EnsureCapacity(blockId, block.MaxActiveHats, excludeJobId: jobId);
            }

            var updated = existing with
            {
                Label = label,
                Client = client,
                Material = material,
                Style = style,
                Status = status,
                Notes = notes,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _jobs[jobId] = updated;
            SyncBlockStatus(blockId);
            return updated;
        }
    }

    public void SeedDemo()
    {
        lock (_gate)
        {
            if (_blocks.Count > 0)
            {
                return;
            }
        }

        var a = AddBlock(new CreateBlockRequest(
            "HB-A1",
            "Maple Cloche Block",
            "Front Room",
            56,
            2,
            "Primary felt blocking."));

        AddBlock(new CreateBlockRequest(
            "HB-B1",
            "Straw Brim Block",
            "Window Bay",
            58,
            1,
            "Wide brims only."));

        AddBlock(new CreateBlockRequest(
            "HB-C1",
            "Overflow C",
            "Back Room",
            54,
            3,
            null));

        var job = AddHatJob(a.Id, new CreateHatJobRequest(
            "Harbor Cloche",
            "Nora Ellis",
            HatMaterial.Felt,
            HatStyle.Cloche,
            "Match the navy grosgrain."));

        UpdateHatJob(a.Id, job.Id, new UpdateHatJobRequest(
            null, null, null, null, HatJobStatus.Wired, null));
    }

    public void Clear()
    {
        lock (_gate)
        {
            _blocks.Clear();
            _jobs.Clear();
        }
    }

    private void SyncBlockStatus(Guid blockId)
    {
        if (!_blocks.TryGetValue(blockId, out var block) || block.Status is BlockStatus.Offline)
        {
            return;
        }

        var active = ActiveJobCount(blockId, excludeJobId: null);
        var next = active > 0 ? BlockStatus.Busy : BlockStatus.Open;
        _blocks[blockId] = block with { Status = next };
    }

    private void EnsureCapacity(Guid blockId, int maxActive, Guid? excludeJobId)
    {
        var active = ActiveJobCount(blockId, excludeJobId);
        if (active + 1 > maxActive)
        {
            throw new ConflictException(
                $"Block is at capacity ({maxActive} active hats).");
        }
    }

    private int ActiveJobCount(Guid blockId, Guid? excludeJobId) =>
        _jobs.Values.Count(j =>
            j.BlockId == blockId
            && j.Id != excludeJobId
            && IsActive(j.Status));

    private static bool IsActive(HatJobStatus status) =>
        status is HatJobStatus.Blocked or HatJobStatus.Wired or HatJobStatus.Trimmed;

    private static void ValidateBlockInput(
        string code,
        string name,
        string room,
        int headSizeCm,
        int maxActiveHats,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException("Block code is required.");
        }

        if (code.Trim().Length > 20)
        {
            throw new ValidationException("Block code must be at most 20 characters.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Block name is required.");
        }

        if (name.Trim().Length > 120)
        {
            throw new ValidationException("Block name must be at most 120 characters.");
        }

        if (string.IsNullOrWhiteSpace(room))
        {
            throw new ValidationException("Room is required.");
        }

        if (room.Trim().Length > 80)
        {
            throw new ValidationException("Room must be at most 80 characters.");
        }

        if (headSizeCm is < 50 or > 64)
        {
            throw new ValidationException("HeadSizeCm must be between 50 and 64.");
        }

        if (maxActiveHats is < 1 or > 6)
        {
            throw new ValidationException("MaxActiveHats must be between 1 and 6.");
        }

        if (notes is { Length: > 2000 })
        {
            throw new ValidationException("Notes must be at most 2000 characters.");
        }
    }

    private static void ValidateJobInput(string label, string client, string? notes)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ValidationException("Job label is required.");
        }

        if (label.Trim().Length > 80)
        {
            throw new ValidationException("Job label must be at most 80 characters.");
        }

        if (string.IsNullOrWhiteSpace(client))
        {
            throw new ValidationException("Client is required.");
        }

        if (client.Trim().Length > 120)
        {
            throw new ValidationException("Client must be at most 120 characters.");
        }

        if (notes is { Length: > 2000 })
        {
            throw new ValidationException("Notes must be at most 2000 characters.");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
