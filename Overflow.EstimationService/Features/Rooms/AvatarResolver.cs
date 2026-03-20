using Overflow.EstimationService.Clients;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Features.Rooms;

/// <summary>
/// Shared avatar resolution logic used by all room command/query handlers.
/// Batches profile lookups for all authenticated participants in a room.
/// </summary>
internal static class AvatarResolver
{
    public static async Task<Dictionary<string, string?>> ResolveForRoomAsync(
        EstimationRoom room, string? accessToken, ProfileServiceClient profileClient)
    {
        var userIds = room.Participants
            .Where(p => p.UserId is not null)
            .Select(p => p.UserId!)
            .Distinct()
            .ToList();

        return await ResolveAsync(userIds, accessToken, profileClient);
    }

    public static async Task<Dictionary<string, string?>> ResolveAsync(
        IList<string> userIds, string? accessToken, ProfileServiceClient profileClient)
    {
        var result = new Dictionary<string, string?>();

        await Task.WhenAll(userIds.Select(async userId =>
        {
            var profile = await profileClient.GetProfileDataAsync(userId, accessToken);
            lock (result)
            {
                result[userId] = profile?.AvatarUrl;
            }
        }));

        return result;
    }
}