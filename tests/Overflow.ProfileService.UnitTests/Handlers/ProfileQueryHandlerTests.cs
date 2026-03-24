using Overflow.ProfileService.Data;
using Overflow.ProfileService.Features.Profiles.Queries;
using Overflow.ProfileService.Models;
using Overflow.ProfileService.UnitTests.Helpers;
using Shouldly;

namespace Overflow.ProfileService.UnitTests.Handlers;

public class ProfileQueryHandlerTests
{
    private readonly ProfileDbContext _db;

    public ProfileQueryHandlerTests()
    {
        _db = DbContextFactory.CreateProfileDb();
    }

    // ── GetProfileById ──────────────────────────────────────────────────

    [Fact]
    public async Task GetProfileById_Exists_ReturnsDto()
    {
        // Arrange
        _db.UserProfiles.Add(new UserProfile
        {
            Id = "user-1", DisplayName = "John", Description = "Dev", Reputation = 100
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetProfileByIdHandler(_db);

        // Act
        var result = await handler.Handle(new GetProfileByIdQuery("user-1"), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result!.UserId.ShouldBe("user-1");
        result.DisplayName.ShouldBe("John");
        result.Reputation.ShouldBe(100);
    }

    [Fact]
    public async Task GetProfileById_NotFound_ReturnsNull()
    {
        // Arrange
        var handler = new GetProfileByIdHandler(_db);

        // Act
        var result = await handler.Handle(new GetProfileByIdQuery("nonexistent"), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    // ── GetProfiles (list) ──────────────────────────────────────────────

    [Fact]
    public async Task GetProfiles_DefaultSort_OrdersByDisplayName()
    {
        // Arrange
        _db.UserProfiles.AddRange(
            new UserProfile { Id = "u-1", DisplayName = "Zara" },
            new UserProfile { Id = "u-2", DisplayName = "Alice" }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetProfilesHandler(_db);

        // Act
        var result = await handler.Handle(new GetProfilesQuery(null), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(2);
        result[0].DisplayName.ShouldBe("Alice");
        result[1].DisplayName.ShouldBe("Zara");
    }

    [Fact]
    public async Task GetProfiles_SortByReputation_OrdersDescending()
    {
        // Arrange
        _db.UserProfiles.AddRange(
            new UserProfile { Id = "u-1", DisplayName = "Low", Reputation = 10 },
            new UserProfile { Id = "u-2", DisplayName = "High", Reputation = 500 }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetProfilesHandler(_db);

        // Act
        var result = await handler.Handle(new GetProfilesQuery("reputation"), CancellationToken.None);

        // Assert
        result[0].DisplayName.ShouldBe("High");
        result[1].DisplayName.ShouldBe("Low");
    }

    // ── GetProfileBatch ─────────────────────────────────────────────────

    [Fact]
    public async Task GetProfileBatch_ReturnsOnlyRequestedIds()
    {
        // Arrange
        _db.UserProfiles.AddRange(
            new UserProfile { Id = "u-1", DisplayName = "One" },
            new UserProfile { Id = "u-2", DisplayName = "Two" },
            new UserProfile { Id = "u-3", DisplayName = "Three" }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetProfileBatchHandler(_db);

        // Act
        var result = await handler.Handle(new GetProfileBatchQuery(["u-1", "u-3"]), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(2);
        result.Select(r => r.UserId).ShouldBe(["u-1", "u-3"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetProfileBatch_NonExistentIds_ReturnsOnlyExisting()
    {
        // Arrange
        _db.UserProfiles.Add(new UserProfile { Id = "u-1", DisplayName = "One" });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetProfileBatchHandler(_db);

        // Act
        var result = await handler.Handle(new GetProfileBatchQuery(["u-1", "nonexistent"]), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(1);
    }
}