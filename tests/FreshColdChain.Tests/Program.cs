using FreshColdChain.Tests;

var orderExitCode = await OrderServiceScenarioTests.RunAllAsync();
var customerMarketingExitCode =
    await CustomerMarketingScenarioTests.RunAllAsync();
var orderLifecycleExitCode =
    await OrderLifecycleScenarioTests.RunAllAsync();
var externalContractExitCode =
    await ExternalContractScenarioTests.RunAllAsync();
var consumerMessageExitCode =
    await ConsumerMessageScenarioTests.RunAllAsync();
var groupAAdapterExitCode =
    await GroupAAdapterScenarioTests.RunAllAsync();
var supplierFulfillmentExitCode =
    await SupplierFulfillmentScenarioTests.RunAllAsync();
var financialTransactionExitCode =
    await FinancialTransactionScenarioTests.RunAllAsync();
var freightAggregationExitCode =
    await FreightAggregationScenarioTests.RunAllAsync();
var authorizationExitCode =
    await AuthorizationScenarioTests.RunAllAsync();
var logisticsPersistenceExitCode =
    await LogisticsPersistenceScenarioTests.RunAllAsync();
var productEvaluationExitCode = await ProductEvaluationScenarioTests.RunAllAsync();

Environment.ExitCode =
    orderExitCode == 0 &&
    customerMarketingExitCode == 0 &&
    orderLifecycleExitCode == 0 &&
    externalContractExitCode == 0 &&
    consumerMessageExitCode == 0 &&
    groupAAdapterExitCode == 0 &&
    supplierFulfillmentExitCode == 0 &&
    financialTransactionExitCode == 0 &&
    freightAggregationExitCode == 0 &&
    authorizationExitCode == 0 &&
    logisticsPersistenceExitCode == 0 &&
    productEvaluationExitCode == 0
    ? 0
    : 1;
