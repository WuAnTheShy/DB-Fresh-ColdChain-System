using FreshColdChain.Tests;

var orderExitCode = await OrderServiceScenarioTests.RunAllAsync();
var customerMarketingExitCode =
    await CustomerMarketingScenarioTests.RunAllAsync();
var orderLifecycleExitCode =
    await OrderLifecycleScenarioTests.RunAllAsync();
var externalContractExitCode =
    await ExternalContractScenarioTests.RunAllAsync();

Environment.ExitCode =
    orderExitCode == 0 &&
    customerMarketingExitCode == 0 &&
    orderLifecycleExitCode == 0 &&
    externalContractExitCode == 0
    ? 0
    : 1;
