$ErrorActionPreference = 'Stop'

$ddlPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'groupB_ddl.sql'
$ddl = Get-Content -LiteralPath $ddlPath -Raw
$tables = [ordered]@{
    Crm_MemberLevels  = 'MemberLevelId'
    Crm_Customers     = 'CustomerId'
    Crm_UserAddresses = 'AddressId'
    Mkt_Coupons       = 'CouponId'
    Mkt_CouponRecords = 'RecordId'
    Crm_PointLogs     = 'PointLogId'
    Biz_Orders        = 'OrderId'
    Biz_OrderDetails  = 'OrderDetailId'
}

foreach ($entry in $tables.GetEnumerator()) {
    $tablePattern = "(?is)CREATE\s+TABLE\s+$([regex]::Escape($entry.Key))\s*\((?<body>.*?)\);"
    $tableMatch = [regex]::Match($ddl, $tablePattern)
    if (-not $tableMatch.Success) {
        throw "缺少 B 组表定义：$($entry.Key)"
    }

    $primaryKeyPattern = "(?im)^\s*$([regex]::Escape($entry.Value))\s+VARCHAR2\(36\)\s+PRIMARY\s+KEY"
    if (-not [regex]::IsMatch($tableMatch.Groups['body'].Value, $primaryKeyPattern)) {
        throw "$($entry.Key).$($entry.Value) 未使用 VARCHAR2(36) 主键"
    }

    $seedPattern = "(?is)INSERT\s+INTO\s+$([regex]::Escape($entry.Key))\s*\("
    if (-not [regex]::IsMatch($ddl, $seedPattern)) {
        throw "缺少 $($entry.Key) 演示数据"
    }
}

$numericIdPattern = '(?im)^\s*(CustomerId|AddressId|OrderId|OrderDetailId|CouponId|RecordId|MemberLevelId|PointLogId)\s+NUMBER'
if ([regex]::IsMatch($ddl, $numericIdPattern)) {
    throw 'B 组 DDL 仍包含 NUMBER 类型的内部标识列'
}

$foreignTablePattern = '(?im)^\s*CREATE\s+TABLE\s+(Crm_Promoters|Fin_PaymentRecords|Fin_Refunds|Log_AuditTrails)\b'
if ([regex]::IsMatch($ddl, $foreignTablePattern)) {
    throw 'B 组 DDL 不得创建 C 组负责的表'
}

Write-Output "PASS B组DDL：8张表主键、演示数据和职责边界检查通过"
