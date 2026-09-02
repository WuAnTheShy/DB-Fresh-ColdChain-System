$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$groupBIntegrationFiles = @(
    'Services/GroupAInventoryServiceAdapter.cs',
    'Services/GroupALogisticsServiceAdapter.cs',
    'Services/GroupCPromoterCatalogService.cs',
    'Services/ConsumerMessageService.cs',
    'Services/OrderService.cs',
    'Services/GroupBMaintenanceServices.cs',
    'Repositories/ConsumerMessageRepository.cs'
)
$foreignRepositoryTypes = @(
    'IProductRepository',
    'IStockSummaryRepository',
    'IStockBatchRepository',
    'ILogFreightTemplateRepository',
    'ILogExpressDeliveryRepository',
    'ILogFulfillmentBatchItemRepository',
    'IPromoterRepository',
    'IRefundRepository',
    'IPaymentRepository',
    'ICommissionRepository'
)
$foreignTables = @(
    'Inv_Products',
    'Inv_StockSummary',
    'Inv_StockBatches',
    'Log_ExpressDeliveries',
    'Log_FreightTemplates',
    'CRM_PROMOTERS',
    'CRM_PCR',
    'CRM_PSRELATION',
    'CRM_PRODUCT_ENTRIES',
    'FIN_PAYMENTRECORDS',
    'FIN_REFUND',
    'FIN_PROCOMRECORDS'
)

$violations = [System.Collections.Generic.List[string]]::new()
foreach ($relativePath in $groupBIntegrationFiles) {
    $path = Join-Path $repositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        $violations.Add("缺少 B 组边界文件: $relativePath")
        continue
    }

    $content = Get-Content -Raw -Encoding UTF8 -LiteralPath $path
    foreach ($typeName in $foreignRepositoryTypes) {
        if ($content -match "\b$([regex]::Escape($typeName))\b") {
            $violations.Add("$relativePath 直接依赖其他组 Repository: $typeName")
        }
    }
    foreach ($tableName in $foreignTables) {
        $sqlPattern = "(?i)\b(FROM|JOIN|INTO|UPDATE|MERGE\s+INTO|DELETE\s+FROM)\s+$([regex]::Escape($tableName))\b"
        if ($content -match $sqlPattern) {
            $violations.Add("$relativePath 直接访问其他组表: $tableName")
        }
    }
}

$inventoryAdapter = Get-Content -Raw -Encoding UTF8 -LiteralPath (
    Join-Path $repositoryRoot 'Services/GroupAInventoryServiceAdapter.cs')
foreach ($requiredType in @('IGroupAInventoryGateway', 'IProductInventoryService', 'ISupplierService')) {
    if ($inventoryAdapter -notmatch "\b$requiredType\b") {
        $violations.Add("库存适配器未调用 A 组公开服务: $requiredType")
    }
}

$logisticsAdapter = Get-Content -Raw -Encoding UTF8 -LiteralPath (
    Join-Path $repositoryRoot 'Services/GroupALogisticsServiceAdapter.cs')
foreach ($requiredCall in @(
    'IColdChainLogisticsService',
    'QuoteFreightAsync',
    'CreateShipmentAsync',
    'GetTraceabilityByOrderAsync'
)) {
    if ($logisticsAdapter -notmatch "\b$requiredCall\b") {
        $violations.Add("物流适配器未调用 A 组公开能力: $requiredCall")
    }
}

$program = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $repositoryRoot 'Program.cs')
if (($program | Select-String -Pattern 'AddGroupBModule\(' -AllMatches).Matches.Count -ne 1) {
    $violations.Add('Program.cs 必须且只能通过一个 AddGroupBModule 初始化 B 组')
}

$ordersApi = Get-Content -Raw -Encoding UTF8 -LiteralPath (
    Join-Path $repositoryRoot 'Controllers/Api/OrdersApiController.cs')
if ($ordersApi -match 'HttpPost\("\{orderId\}/transition"\)') {
    $violations.Add('消费者订单 API 不得暴露通用订单状态流转入口')
}

$orderController = Get-Content -Raw -Encoding UTF8 -LiteralPath (
    Join-Path $repositoryRoot 'Controllers/OrderController.cs')
if ($orderController -notmatch 'GroupBAdminSessionAuthorizationFilter') {
    $violations.Add('MVC 订单管理入口必须启用管理员会话权限过滤器')
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output 'PASS: B 组未直连其他组 Repository/数据表，A 组库存物流真实服务已接入，初始化入口唯一。'
