using CommandFlow;
using Overflow.EstimationService.Services;

namespace Overflow.EstimationService.Features.Rooms.Commands;

public record ClaimGuestCommand(
    string UserId,
    string? GuestId,
    string DisplayName) : ICommand<int>;

public class ClaimGuestHandler(EstimationRoomService svc) : IRequestHandler<ClaimGuestCommand, int>
{
    public async Task<int> Handle(ClaimGuestCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.GuestId))
            return 0;

        return await svc.ClaimGuestAsync(request.GuestId, request.UserId, request.DisplayName);
    }
}