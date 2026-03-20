using CommandFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Overflow.Contracts;
using Overflow.NotificationService.Features.Notifications.Commands;

namespace Overflow.NotificationService.Controllers;

[ApiController]
[Authorize]
[Route("[controller]")]
public class NotificationsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Accepts a notification request and publishes it to RabbitMQ for async processing.
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendNotification request)
    {
        await sender.Send(new PublishNotificationCommand(request));
        return Accepted();
    }
}