﻿namespace Core6.Headers.Writers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using NServiceBus;
    using NServiceBus.MessageMutator;
    using NUnit.Framework;
    using Operations.Msmq;

    [TestFixture]
    public class HeaderWriterError
    {
        static ManualResetEvent ManualResetEvent = new ManualResetEvent(false);
        string endpointName = "HeaderWriterErrorV6";

        [SetUp]
        [TearDown]
        public void Setup()
        {
            QueueDeletion.DeleteQueuesForEndpoint(endpointName);
        }

        [Test]
        public async Task Write()
        {
            var endpointConfiguration = new EndpointConfiguration(endpointName);
            endpointConfiguration.SendFailedMessagesTo("error");
            var typesToScan = TypeScanner.NestedTypes<HeaderWriterError>();
            endpointConfiguration.SetTypesToScan(typesToScan);
            endpointConfiguration.EnableInstallers();
            endpointConfiguration.UsePersistence<InMemoryPersistence>();
            endpointConfiguration.RegisterComponents(
                registration: components =>
                {
                    components.ConfigureComponent<Mutator>(DependencyLifecycle.InstancePerCall);
                });
            var recoverability = endpointConfiguration.Recoverability();
            recoverability.Immediate(settings => settings.NumberOfRetries(1));
            recoverability.Delayed(settings =>
            {
                settings.NumberOfRetries(0);
            });

            var endpointInstance = await Endpoint.Start(endpointConfiguration)
                .ConfigureAwait(false);

            await endpointInstance.SendLocal(new MessageToSend())
                .ConfigureAwait(false);
            ManualResetEvent.WaitOne();
        }

        class MessageToSend :
            IMessage
        {
        }


        class MessageHandler :
            IHandleMessages<MessageToSend>
        {

            public Task Handle(MessageToSend message, IMessageHandlerContext context)
            {
                throw new Exception("The exception message from the handler.");
            }
        }

        class Mutator :
            IMutateIncomingTransportMessages
        {
            public Mutator(Notifications busNotifications)
            {
                var errorsNotifications = busNotifications.Errors;
                errorsNotifications.MessageSentToErrorQueue += (sender, retry) =>
                {
                    var headers = retry.Headers;
                    var headerText = HeaderWriter.ToFriendlyString<HeaderWriterError>(headers);
                    headerText = BehaviorCleaner.CleanStackTrace(headerText);
                    headerText = StackTraceCleaner.CleanStackTrace(headerText);
                    SnippetLogger.Write(
                        text: headerText,
                        suffix: "Error",
                        version: "6");
                    ManualResetEvent.Set();
                };
            }

            static bool hasCapturedMessage;

            public Task MutateIncoming(MutateIncomingTransportMessageContext context)
            {
                if (!hasCapturedMessage && context.IsMessageOfTye<MessageToSend>())
                {
                    hasCapturedMessage = true;
                    var headers = context.Headers;
                    var sendingText = HeaderWriter.ToFriendlyString<HeaderWriterError>(headers);
                    SnippetLogger.Write(
                        text: sendingText,
                        suffix: "Sending",
                        version: "6");
                }
                return Task.FromResult(0);
            }
        }
    }
}