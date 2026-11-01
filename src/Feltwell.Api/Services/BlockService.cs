using Feltwell.Api.Errors;
using Feltwell.Api.Models;
using Feltwell.Api.Repositories;

namespace Feltwell.Api.Services;

public sealed class BlockService(IBlockStore store)
{
    public HatBlock CreateBlock(CreateBlockRequest request) => store.AddBlock(request);

    public HatBlock GetBlock(Guid id) =>
        store.GetBlock(id) ?? throw new NotFoundException($"Block {id} not found.");

    public BlockListResponse ListBlocks(
        BlockStatus? status,
        string? room,
        string? q,
        int limit,
        int offset)
    {
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(offset, 0);
        var (items, total) = store.ListBlocks(status, room, q, limit, offset);
        return new BlockListResponse(items, total, limit, offset);
    }

    public HatBlock UpdateBlock(Guid id, UpdateBlockRequest request) =>
        store.UpdateBlock(id, request) ?? throw new NotFoundException($"Block {id} not found.");

    public void DeleteBlock(Guid id)
    {
        if (!store.DeleteBlock(id))
        {
            throw new NotFoundException($"Block {id} not found.");
        }
    }

    public HatJob CreateHatJob(Guid blockId, CreateHatJobRequest request) =>
        store.AddHatJob(blockId, request);

    public HatJobListResponse ListHatJobs(Guid blockId, HatJobStatus? status)
    {
        var (items, total) = store.ListHatJobs(blockId, status);
        return new HatJobListResponse(items, total);
    }

    public HatJob GetHatJob(Guid blockId, Guid jobId) =>
        store.GetHatJob(blockId, jobId)
        ?? throw new NotFoundException($"Hat job {jobId} not found.");

    public HatJob UpdateHatJob(Guid blockId, Guid jobId, UpdateHatJobRequest request) =>
        store.UpdateHatJob(blockId, jobId, request)
        ?? throw new NotFoundException($"Hat job {jobId} not found.");
}
