using Microsoft.AspNetCore.Mvc;
using Overflow.EstimationService.Exceptions;

namespace Overflow.EstimationService.Extensions;

internal static class RoomErrorExtensions
{
    public static IActionResult ToActionResult(this RoomError error) => error.Code switch
    {
        RoomErrorCode.NotFound => new NotFoundObjectResult(error.Message),
        RoomErrorCode.Forbidden => new ForbidResult(),
        RoomErrorCode.Archived or
            RoomErrorCode.ParticipantNotFound or
            RoomErrorCode.InvalidState or
            RoomErrorCode.InvalidVote or
            _ => new BadRequestObjectResult(error.Message)
    };
}