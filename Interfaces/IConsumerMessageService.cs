using FreshColdChain.Models;

namespace FreshColdChain.Interfaces;

public interface IConsumerMessageService
{
    Task<List<ConsumerMessage>> GetMessagesAsync(string customerId, int take = 100);
}
