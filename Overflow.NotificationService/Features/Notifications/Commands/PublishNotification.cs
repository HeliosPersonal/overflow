using CommandFlow;
using Overflow.Contracts;
using Wolverine;

namespace Overflow.NotificationService.Features.Notifications.Commands;

public record PublishNotificationCommand(SendNotification Notification) : ICommand;

public class PublishNotificationHandler(IMessageBus bus) : ICommandHandler<PublishNotificationCommand>
{
    public async Task HandleCommand(PublishNotificationCommand request, CancellationToken cancellationToken)
    {
        await bus.PublishAsync(request.Notification);
    }
}