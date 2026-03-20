using CommandFlow;
using Overflow.EstimationService.Clients;

namespace Overflow.EstimationService.Features.Rooms.Commands;

public record InvalidateProfileCacheCommand(string UserId) : ICommand;

public class InvalidateProfileCacheHandler(ProfileServiceClient profileClient)
    : ICommandHandler<InvalidateProfileCacheCommand>
{
    public async Task HandleCommand(InvalidateProfileCacheCommand request, CancellationToken cancellationToken)
    {
        await profileClient.InvalidateAsync(request.UserId);
    }
}