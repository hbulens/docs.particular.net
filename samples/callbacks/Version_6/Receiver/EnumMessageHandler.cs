using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;

#region EnumMessageHandler

public class EnumMessageHandler : IHandleMessages<EnumMessage>
{
    static ILog log = LogManager.GetLogger<EnumMessageHandler>();

    public async Task Handle(EnumMessage message, IMessageHandlerContext context)
    {
        log.Info("Message received, Returning");
        await context.Reply(Status.OK);
    }
}

#endregion