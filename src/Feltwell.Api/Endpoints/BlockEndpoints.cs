using Feltwell.Api.Models;
using Feltwell.Api.Services;

namespace Feltwell.Api.Endpoints;

public static class BlockEndpoints
{
    public static RouteGroupBuilder MapBlockEndpoints(this IEndpointRouteBuilder app)
    {
        var blocks = app.MapGroup("/api/v1/blocks").WithTags("Blocks");

        blocks.MapPost("/", (CreateBlockRequest request, BlockService service) =>
            {
                var block = service.CreateBlock(request);
                return Results.Created($"/api/v1/blocks/{block.Id}", block);
            })
            .WithName("CreateBlock")
            .WithSummary("Add a millinery hat block");

        blocks.MapGet("/", (
                BlockStatus? status,
                string? room,
                string? q,
                int? limit,
                int? offset,
                BlockService service) =>
            Results.Ok(service.ListBlocks(
                status,
                room,
                q,
                limit ?? 50,
                offset ?? 0)))
            .WithName("ListBlocks")
            .WithSummary("List blocks with optional filters");

        blocks.MapGet("/{blockId:guid}", (Guid blockId, BlockService service) =>
                Results.Ok(service.GetBlock(blockId)))
            .WithName("GetBlock")
            .WithSummary("Get a block by id");

        blocks.MapPatch("/{blockId:guid}", (Guid blockId, UpdateBlockRequest request, BlockService service) =>
                Results.Ok(service.UpdateBlock(blockId, request)))
            .WithName("UpdateBlock")
            .WithSummary("Update a block");

        blocks.MapDelete("/{blockId:guid}", (Guid blockId, BlockService service) =>
            {
                service.DeleteBlock(blockId);
                return Results.NoContent();
            })
            .WithName("DeleteBlock")
            .WithSummary("Delete a block and its hat jobs");

        blocks.MapPost("/{blockId:guid}/hats", (
                Guid blockId,
                CreateHatJobRequest request,
                BlockService service) =>
            {
                var job = service.CreateHatJob(blockId, request);
                return Results.Created(
                    $"/api/v1/blocks/{blockId}/hats/{job.Id}",
                    job);
            })
            .WithName("CreateHatJob")
            .WithSummary("Queue a hat job on a block");

        blocks.MapGet("/{blockId:guid}/hats", (
                Guid blockId,
                HatJobStatus? status,
                BlockService service) =>
                Results.Ok(service.ListHatJobs(blockId, status)))
            .WithName("ListHatJobs")
            .WithSummary("List hat jobs for a block");

        blocks.MapGet("/{blockId:guid}/hats/{jobId:guid}", (
                Guid blockId,
                Guid jobId,
                BlockService service) =>
                Results.Ok(service.GetHatJob(blockId, jobId)))
            .WithName("GetHatJob")
            .WithSummary("Get one hat job");

        blocks.MapPatch("/{blockId:guid}/hats/{jobId:guid}", (
                Guid blockId,
                Guid jobId,
                UpdateHatJobRequest request,
                BlockService service) =>
                Results.Ok(service.UpdateHatJob(blockId, jobId, request)))
            .WithName("UpdateHatJob")
            .WithSummary("Update hat job details or status");

        return blocks;
    }
}
