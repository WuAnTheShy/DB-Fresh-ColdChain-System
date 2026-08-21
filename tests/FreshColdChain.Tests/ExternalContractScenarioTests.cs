using System.Text.Json;
using FreshColdChain.Models;

namespace FreshColdChain.Tests;

internal static class ExternalContractScenarioTests
{
    public static async Task<int> RunAllAsync()
    {
        var scenarios = new (string Name, Func<Task> Run)[]
        {
            ("消费者商品响应不泄露供应商编号", ConsumerProductHidesSupplierIdAsync),
            ("新增跨组契约的单值标识均为字符串", ExternalContractIdsAreStringsAsync)
        };

        var failed = 0;
        foreach (var scenario in scenarios)
        {
            try
            {
                await scenario.Run();
                Console.WriteLine($"PASS {scenario.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine($"FAIL {scenario.Name}");
                Console.WriteLine(exception);
            }
        }

        Console.WriteLine(
            $"跨组契约场景总数: {scenarios.Length}, 通过: {scenarios.Length - failed}, 失败: {failed}");
        return failed == 0 ? 0 : 1;
    }

    private static Task ConsumerProductHidesSupplierIdAsync()
    {
        var json = JsonSerializer.Serialize(new GroupAConsumerProduct
        {
            ProductId = "P1",
            ProductName = "车厘子",
            SupplierId = "SUP1",
            SalePrice = 50m,
            IsInStock = true
        });

        AssertEx.True(!json.Contains("SupplierId", StringComparison.Ordinal));
        AssertEx.True(!json.Contains("SUP1", StringComparison.Ordinal));
        return Task.CompletedTask;
    }

    private static Task ExternalContractIdsAreStringsAsync()
    {
        var contractTypes = new[]
        {
            typeof(GroupAConsumerProduct),
            typeof(GroupATrustedProduct),
            typeof(GroupCPromoterSummary),
            typeof(GroupCPromoterProductCandidate),
            typeof(GroupCPromoterProductValidation)
        };
        var invalidProperties = contractTypes
            .SelectMany(type => type.GetProperties())
            .Where(property =>
                property.Name.EndsWith("Id", StringComparison.Ordinal) &&
                property.PropertyType != typeof(string))
            .ToList();

        AssertEx.Equal(0, invalidProperties.Count);
        return Task.CompletedTask;
    }
}
