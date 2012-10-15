using System.Threading;
using System.Threading.Tasks;
using ServiceStack.ServiceInterface;

namespace Platform.Node
{
    public class StreamService : ServiceBase<ClientDto.WriteEvent>
    {
        readonly IPublisher _publisher;
        
        public StreamService(IPublisher publisher)
        {
            _publisher = publisher;
        }


        protected override object Run(ClientDto.WriteEvent request)
        {
            var token = new ManualResetEventSlim(false);

            
            _publisher.Publish(new ClientMessage.AppendEvents(request.Stream,request.Data, request.ExpectedVersion,s => token.Set()));

            return Task.Factory.StartNew(() =>
                {
                    try
                    {
                        token.Wait();
                        return new ClientDto.WriteEventResponse()
                            {
                                Result = "Completed"
                            };
                    }
                    finally
                    {
                        token.Dispose();
                    }
                
                });
        }
    }
}