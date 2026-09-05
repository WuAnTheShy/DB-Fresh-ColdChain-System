using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using Microsoft.Extensions.Options;

namespace FreshColdChain.Services;

public sealed class GroupADemoCarrierService(IUnitOfWork unitOfWork, IGroupACarrierRepository repository,
    IGroupALogisticsExtensionProvider logistics, IOptions<DemoCarrierOptions> options)
{
    public Task<IReadOnlyList<CarrierShipmentSummary>> SearchAsync(string? keyword, CancellationToken token) =>
        repository.SearchAsync(options.Value.SupplierIds, keyword, token);

    public async Task<SupplierLogisticsSnapshot?> GetAsync(string deliveryId, CancellationToken token)
    {
        var delivery = await repository.FindAsync(deliveryId, options.Value.SupplierIds, null, token);
        return delivery == null ? null : await logistics.GetSnapshotAsync(new()
            { OrderId = delivery.OrderID, SupplierId = delivery.SupplierID }, token);
    }

    public async Task<SupplierLogisticsSnapshot?> AppendAsync(string deliveryId, CarrierEventCommand command, CancellationToken token)
    {
        await unitOfWork.BeginAsync();
        try
        {
            var delivery = await repository.FindAsync(deliveryId, options.Value.SupplierIds, unitOfWork.Transaction, token);
            if (delivery == null) { await unitOfWork.RollbackAsync(); return null; }
            var result = await logistics.AppendTrackingEventAsync(new()
            {
                EventId = command.EventId, OrderId = delivery.OrderID, SupplierId = delivery.SupplierID,
                StatusCode = command.StatusCode, Location = command.Location ?? "", Description = command.Description,
                OccurredAt = command.OccurredAt, TemperatureCelsius = command.TemperatureCelsius
            }, unitOfWork.Transaction!, token);
            await unitOfWork.CommitAsync();
            return result;
        }
        catch { await unitOfWork.RollbackAsync(); throw; }
    }
}
