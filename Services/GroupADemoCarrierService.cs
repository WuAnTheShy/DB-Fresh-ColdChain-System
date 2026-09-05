using FreshColdChain.Interfaces;
using FreshColdChain.Models;
using FreshColdChain.Repositories;
using Microsoft.Extensions.Options;

namespace FreshColdChain.Services;

public sealed class GroupADemoCarrierService(IUnitOfWork unitOfWork, IGroupACarrierRepository repository,
    IGroupALogisticsExtensionProvider logistics, ISupplierFulfillmentService fulfillment,
    IOptions<DemoCarrierOptions> options)
{
    public Task<IReadOnlyList<CarrierShipmentSummary>> SearchAsync(string? keyword, CancellationToken token) =>
        repository.SearchAsync(options.Value.IncludeAllDatabaseShipments, options.Value.SupplierIds, keyword, token);

    public async Task<SupplierLogisticsSnapshot?> GetAsync(string deliveryId, CancellationToken token)
    {
        var delivery = await repository.FindAsync(deliveryId, options.Value.IncludeAllDatabaseShipments, options.Value.SupplierIds, null, token);
        return delivery == null ? null : await logistics.GetSnapshotAsync(new()
            { OrderId = delivery.OrderID, SupplierId = delivery.SupplierID }, token);
    }

    public async Task<SupplierLogisticsSnapshot?> HandoffAsync(CarrierHandoffCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!GroupBIds.IsValid(command.OrderId) || !GroupBIds.IsValid(command.SupplierId))
            throw new OrderBusinessException("订单或供应商编号无效");
        var settings = options.Value;
        if (!settings.IncludeAllDatabaseShipments &&
            !(settings.SupplierIds ?? []).Contains(command.SupplierId, StringComparer.Ordinal))
            return null;
        var packageTemperature = settings.PackageTemperature?.Trim().ToUpperInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(settings.CarrierCode) || string.IsNullOrWhiteSpace(settings.CarrierName) ||
            packageTemperature is not ("CHILLED" or "FROZEN" or "AMBIENT") || settings.EstimatedTransitHours <= 0)
            throw new OrderBusinessException("物流模拟器接单配置不完整");

        return await fulfillment.ShipAsync(command.SupplierId, command.OrderId, new()
        {
            SupplierId = command.SupplierId,
            CarrierCode = settings.CarrierCode.Trim(),
            CarrierName = settings.CarrierName.Trim(),
            PackageTemperature = packageTemperature,
            EstimatedArrivalAt = DateTime.Now.AddHours(settings.EstimatedTransitHours),
            Remark = "独立物流模拟器接单"
        }, token);
    }

    public async Task<SupplierLogisticsSnapshot?> AppendAsync(string deliveryId, CarrierEventCommand command, CancellationToken token)
    {
        await unitOfWork.BeginAsync();
        try
        {
            var delivery = await repository.FindAsync(deliveryId, options.Value.IncludeAllDatabaseShipments,
                options.Value.SupplierIds, unitOfWork.Transaction, token);
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
