using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Dapper;
using FreshColdChain.Models;

namespace FreshColdChain.Repositories;

/// <summary>
/// 通用仓储实现（Dapper 版本）
/// 支持 int 和 VARCHAR2(36) 两种主键类型
/// </summary>
public class BaseRepository<T> : IBaseRepository<T> where T : class
{
    protected readonly IUnitOfWork _uow;

    private readonly string _tableName;
    private readonly string _keyColumn;
    private readonly PropertyInfo _keyProp;
    private readonly bool _isGuidPk; // VARCHAR2(36) 主键
    private readonly List<MappedColumn> _columns;
    private readonly List<MappedColumn> _allColumns;

    private record MappedColumn(string PropName, string ColName, PropertyInfo Property);

    public BaseRepository(IUnitOfWork uow)
    {
        _uow = uow;
        var type = typeof(T);

        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        _tableName = tableAttr != null ? tableAttr.Name : type.Name;

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null)
            .ToList();

        var keyProp = props.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null)
                      ?? props.FirstOrDefault(p => p.Name is "Id" or "SupplierID" or "ProductID" or "CategoryID" or "StockID" or "BatchID" or "RuleID");

        if (keyProp == null)
            throw new InvalidOperationException($"实体 {type.Name} 未找到主键属性");

        _keyProp = keyProp;
        _keyColumn = GetColumnName(keyProp);
        _isGuidPk = keyProp.PropertyType == typeof(string);

        _allColumns = props.Select(p => new MappedColumn(p.Name, GetColumnName(p), p)).ToList();
        _columns = _allColumns.Where(c => c.PropName != _keyProp.Name).ToList();
    }

    // ==================== 读 ====================

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        var sql = $"SELECT * FROM {_tableName} WHERE {_keyColumn} = :Id";
        return await _uow.Connection.QuerySingleOrDefaultAsync<T>(sql, new { Id = id }, _uow.Transaction);
    }

    /// <summary>VARCHAR2 主键版本</summary>
    public virtual async Task<T?> GetByIdAsync(string id)
    {
        var sql = $"SELECT * FROM {_tableName} WHERE {_keyColumn} = :Id";
        return await _uow.Connection.QuerySingleOrDefaultAsync<T>(sql, new { Id = id }, _uow.Transaction);
    }

    public virtual async Task<List<T>> GetAllAsync()
    {
        var sql = $"SELECT * FROM {_tableName} ORDER BY {_keyColumn}";
        return (await _uow.Connection.QueryAsync<T>(sql, transaction: _uow.Transaction)).ToList();
    }

    public virtual async Task<List<T>> GetPagedAsync(int pageIndex, int pageSize)
    {
        var sql = $"""
            SELECT * FROM {_tableName}
            ORDER BY {_keyColumn}
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY
            """;
        return (await _uow.Connection.QueryAsync<T>(sql, new
        {
            Skip = (pageIndex - 1) * pageSize,
            Take = pageSize
        }, _uow.Transaction)).ToList();
    }

    public virtual async Task<int> CountAsync()
    {
        var sql = $"SELECT COUNT(*) FROM {_tableName}";
        return await _uow.Connection.ExecuteScalarAsync<int>(sql, transaction: _uow.Transaction);
    }

    public virtual async Task<bool> AnyAsync()
    {
        var sql = $"SELECT COUNT(*) FROM {_tableName} WHERE ROWNUM = 1";
        var count = await _uow.Connection.ExecuteScalarAsync<int>(sql, transaction: _uow.Transaction);
        return count > 0;
    }

    // ==================== 写 ====================

    public virtual async Task<T> AddAsync(T entity)
    {
        if (_isGuidPk)
        {
            // VARCHAR2(36) 主键：应用层生成 GUID
            var pkValue = _keyProp.GetValue(entity)?.ToString();
            if (string.IsNullOrWhiteSpace(pkValue))
                _keyProp.SetValue(entity, Guid.NewGuid().ToString());

            // INSERT 包含主键列
            var allColNames = _allColumns.Select(c => c.ColName);
            var allParamNames = _allColumns.Select(c => $":{c.PropName}");
            var sql = $"INSERT INTO {_tableName} ({string.Join(", ", allColNames)}) VALUES ({string.Join(", ", allParamNames)})";

            var dp = new Dictionary<string, object?>();
            foreach (var col in _allColumns)
                dp.Add(col.PropName, col.Property.GetValue(entity));

            await _uow.Connection.ExecuteAsync(sql, dp, _uow.Transaction);
        }
        else
        {
            // int 自增主键：INSERT 不含主键，RETURNING
            var colNames = _columns.Select(c => c.ColName).ToList();
            var paramNames = _columns.Select(c => $":{c.PropName}").ToList();
            var sql = $"INSERT INTO {_tableName} ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", paramNames)}) RETURNING {_keyColumn} INTO :OutId";

            var dp = new DynamicParameters();
            foreach (var col in _columns)
                dp.Add(col.PropName, col.Property.GetValue(entity));
            dp.Add("OutId", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);

            await _uow.Connection.ExecuteAsync(sql, dp, _uow.Transaction);
            _keyProp.SetValue(entity, dp.Get<int>("OutId"));
        }

        return entity;
    }

    public virtual void Update(T entity)
    {
        var setClauses = _columns.Select(c => $"{c.ColName} = :{c.PropName}");
        var sql = $"UPDATE {_tableName} SET {string.Join(", ", setClauses)} WHERE {_keyColumn} = :PkVal";

        var dp = new Dictionary<string, object?>();
        foreach (var col in _columns)
            dp.Add(col.PropName, col.Property.GetValue(entity));
        dp.Add("PkVal", _keyProp.GetValue(entity));

        _uow.Connection.Execute(sql, dp, _uow.Transaction);
    }

    public virtual void Delete(T entity)
    {
        var sql = $"DELETE FROM {_tableName} WHERE {_keyColumn} = :Id";
        _uow.Connection.Execute(sql, new { Id = _keyProp.GetValue(entity) }, _uow.Transaction);
    }

    public virtual Task SaveChangesAsync() => Task.CompletedTask;

    // ==================== 工具 ====================

    private static string GetColumnName(PropertyInfo prop)
    {
        var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
        return colAttr != null ? colAttr.Name : prop.Name;
    }
}
