using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

public interface IConsumerMessageRepository
{
    Task<List<ConsumerMessage>> GetMessagesAsync(string customerId, int take = 100);
}
