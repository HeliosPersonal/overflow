using Overflow.ProfileService.Data;
using Overflow.ProfileService.Features.Profiles.Commands;
using Overflow.ProfileService.Models;
using Overflow.ProfileService.UnitTests.Helpers;
using Shouldly;

namespace Overflow.ProfileService.UnitTests.Handlers;

public class EditProfileHandlerTests
{
    private readonly ProfileDbContext _db;
    private readonly EditProfileHandler _sut;

    public EditProfileHandlerTests()
    {
        _db = DbContextFactory.CreateProfileDb();
        _sut = new EditProfileHandler(_db);
    }

    [Fact]
    public async Task Handle_ExistingProfile_UpdatesAllFields()
    {
        // Arrange
        _db.UserProfiles.Add(new UserProfile
        {
            Id = "user-1", DisplayName = "Old Name", Description = "Old desc", AvatarUrl = "old.jpg"
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new EditProfileCommand("user-1", "New Name", "New desc", "new.jpg");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var profile =
            await _db.UserProfiles.FindAsync(new object?[] { "user-1" }, TestContext.Current.CancellationToken);
        profile!.DisplayName.ShouldBe("New Name");
        profile.Description.ShouldBe("New desc");
        profile.AvatarUrl.ShouldBe("new.jpg");
    }

    [Fact]
    public async Task Handle_NullFields_KeepsExistingValues()
    {
        // Arrange
        _db.UserProfiles.Add(new UserProfile
        {
            Id = "user-1", DisplayName = "Keep Me", Description = "Keep This", AvatarUrl = "keep.jpg"
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new EditProfileCommand("user-1", null, null, null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var profile =
            await _db.UserProfiles.FindAsync(new object?[] { "user-1" }, TestContext.Current.CancellationToken);
        profile!.DisplayName.ShouldBe("Keep Me");
        profile.Description.ShouldBe("Keep This");
        profile.AvatarUrl.ShouldBe("keep.jpg");
    }

    [Fact]
    public async Task Handle_ProfileNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new EditProfileCommand("nonexistent", "Name", null, null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Profile not found");
    }

    [Fact]
    public async Task Handle_PartialUpdate_OnlyUpdatesProvidedFields()
    {
        // Arrange
        _db.UserProfiles.Add(new UserProfile
        {
            Id = "user-1", DisplayName = "Original", Description = "Desc", AvatarUrl = "avatar.jpg"
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new EditProfileCommand("user-1", "Updated Name", null, null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var profile =
            await _db.UserProfiles.FindAsync(new object?[] { "user-1" }, TestContext.Current.CancellationToken);
        profile!.DisplayName.ShouldBe("Updated Name");
        profile.Description.ShouldBe("Desc");
        profile.AvatarUrl.ShouldBe("avatar.jpg");
    }
}