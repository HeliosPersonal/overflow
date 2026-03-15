using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Overflow.Contracts;
using Wolverine;

namespace Overflow.NotificationService.Controllers;

[ApiController]
[Authorize]
[Route("[controller]")]
public class NotificationsController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// Accepts a notification request and publishes it to RabbitMQ for async processing.
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendNotification request)
    {
        await bus.PublishAsync(request);
        return Accepted();
    }
}