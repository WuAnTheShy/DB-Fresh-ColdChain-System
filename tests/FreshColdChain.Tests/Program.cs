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

Environment.ExitCode =
    orderExitCode == 0 &&
    customerMarketingExitCode == 0 &&
    orderLifecycleExitCode == 0 &&
    externalContractExitCode == 0 &&
    consumerMessageExitCode == 0 &&
    groupAAdapterExitCode == 0
    ? 0
    : 1;
