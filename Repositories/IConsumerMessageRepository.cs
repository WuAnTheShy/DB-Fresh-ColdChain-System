using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IConsumerMessageRepository
{
    Task<List<ConsumerMessage>> GetOrderMessagesAsync(string customerId, int take = 100);
}
