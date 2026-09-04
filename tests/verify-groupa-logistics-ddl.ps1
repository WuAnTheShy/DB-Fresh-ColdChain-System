$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$ddl = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $repositoryRoot 'groupA_logistics_persistence.sql')
$requiredPatterns = [ordered]@{
    '遇到错误停止' = 'WHENEVER\s+SQLERROR\s+EXIT'
    '重复基础单预检' = 'HAVING\s+COUNT\(\*\)\s*>\s*1'
    '基础单幂等约束' = 'UQ_LED_ORDER_SUPPLIER\s+UNIQUE\s*\(OrderID,\s*SupplierID\)'
    '元数据主键' = 'PK_LLD\s+PRIMARY\s+KEY\s*\(DeliveryId\)'
    '元数据外键' = 'FK_LLD_DELIVERY\s+FOREIGN\s+KEY\s*\(DeliveryId\)\s+REFERENCES\s+Log_ExpressDeliveries\(DeliveryID\)'
    '运单唯一约束' = 'UQ_LLD_TRACKING\s+UNIQUE\s*\(CarrierTrackingKey\)'
    '温区检查' = 'CK_LLD_ZONE\s+CHECK'
    '发货备注' = 'Remark\s+VARCHAR2\(300\s+CHAR\)'
    '事件主键' = 'PK_LLE\s+PRIMARY\s+KEY\s*\(EventId\)'
    '事件外键' = 'FK_LLE_DELIVERY\s+FOREIGN\s+KEY\s*\(DeliveryId\)\s+REFERENCES\s+Log_ExpressDeliveries\(DeliveryID\)'
    '事件请求哈希' = 'RequestHash\s+VARCHAR2\(64\)\s+NOT\s+NULL'
    '事件序号唯一' = 'UQ_LLE_SEQUENCE\s+UNIQUE\s*\(DeliveryId,\s*SequenceNo\)'
    '事件状态检查' = 'CK_LLE_STATUS\s+CHECK'
    '轨迹时间精度' = 'OccurredAt\s+TIMESTAMP\(7\)\s+NOT\s+NULL'
    '时效时间精度' = 'EstimatedArrivalAt\s+TIMESTAMP\(7\)'
    '温控异常检查' = 'CK_LLE_TEMP\s+CHECK'
}
foreach ($item in $requiredPatterns.GetEnumerator()) {
    if ($ddl -notmatch "(?is)$($item.Value)") { throw "物流迁移缺少：$($item.Key)" }
}
if ($ddl -match '(?im)^\s*(DROP|TRUNCATE|DELETE)\s') { throw '增量迁移不得删除已有业务数据' }
$repository = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $repositoryRoot 'Repositories/GroupALogisticsRepository.cs')
foreach ($pattern in @('FOR UPDATE', 'OracleDbType.TimeStamp', 'cancellationToken: cancellationToken')) {
    if (-not $repository.Contains($pattern)) { throw "物流仓储缺少必要约束：$pattern" }
}
if ($repository -match '\.(Commit|Rollback|BeginTransaction|CreateConnection)\(') { throw '物流仓储不得自行管理事务生命周期' }
Write-Output 'A 组物流 DDL 和仓储静态检查通过（不代表已执行 Oracle 迁移或并发验证）'
