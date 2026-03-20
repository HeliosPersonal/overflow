using CommandFlow;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Overflow.Common;
using Overflow.QuestionService.Data;
using Overflow.QuestionService.Models;
using Overflow.QuestionService.Services;
using ZiggyCreatures.Caching.Fusion;

namespace Overflow.QuestionService.Features.Tags.Commands;

// ─── Create Tag ──────────────────────────────────────────────────────────

public record CreateTagCommand(string Slug, string Name, string Description) : ICommand<Result<Tag>>;

public class CreateTagHandler(
    QuestionDbContext db,
    TagService tagService,
    IFusionCache cache)
    : IRequestHandler<CreateTagCommand, Result<Tag>>
{
    public async Task<Result<Tag>> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        var slug = request.Slug.ToLowerInvariant().Trim();

        if (await db.Tags.AsNoTracking().AnyAsync(t => t.Slug == slug, cancellationToken))
            return Result.Failure<Tag>($"A tag with slug '{slug}' already exists.");

        var tag = new Tag
        {
            Id = slug,
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description.Trim()
        };

        db.Tags.Add(tag);
        await db.SaveChangesAsync(cancellationToken);
        tagService.InvalidateCache();
        await cache.RemoveByTagAsync(CacheTags.TagList, token: cancellationToken);

        return tag;
    }
}

// ─── Update Tag ──────────────────────────────────────────────────────────

public record UpdateTagCommand(string Id, string Name, string Description) : ICommand<Result<Tag>>;

public class UpdateTagHandler(
    QuestionDbContext db,
    TagService tagService,
    IFusionCache cache)
    : IRequestHandler<UpdateTagCommand, Result<Tag>>
{
    public async Task<Result<Tag>> Handle(UpdateTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await db.Tags.FindAsync([request.Id], cancellationToken);
        if (tag is null) return Result.Failure<Tag>(DomainErrors.NotFound);

        tag.Name = request.Name.Trim();
        tag.Description = request.Description.Trim();

        await db.SaveChangesAsync(cancellationToken);
        tagService.InvalidateCache();
        await cache.RemoveByTagAsync(CacheTags.TagList, token: cancellationToken);

        return tag;
    }
}

// ─── Delete Tag ──────────────────────────────────────────────────────────

public record DeleteTagCommand(string Id) : ICommand<Result>;

public class DeleteTagHandler(
    QuestionDbContext db,
    TagService tagService,
    IFusionCache cache)
    : IRequestHandler<DeleteTagCommand, Result>
{
    public async Task<Result> Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await db.Tags.FindAsync([request.Id], cancellationToken);
        if (tag is null) return Result.Failure(DomainErrors.NotFound);

        db.Tags.Remove(tag);
        await db.SaveChangesAsync(cancellationToken);
        tagService.InvalidateCache();
        await cache.RemoveByTagAsync(CacheTags.TagList, token: cancellationToken);

        return Result.Success();
    }
}