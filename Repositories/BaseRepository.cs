using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using Dapper;
using FreshGroupSystem.Data;

namespace FreshGroupSystem.Repositories;

/// <summary>
/// 通用仓储实现（Dapper 版本）
/// 通过读取 [Table]/[Column]/[Key] 特性自动生成 SQL
/// </summary>
public class BaseRepository<T> : IBaseRepository<T> where T : class
{
    protected readonly IUnitOfWork _uow;

    // 缓存反射结果
    private readonly string _tableName;
    private readonly string _keyColumn;
    private readonly PropertyInfo _keyProp;
    private readonly List<MappedColumn> _columns; // 不含主键的读写列
    private readonly List<MappedColumn> _allColumns; // 全部列（含主键）

    private record MappedColumn(string PropName, string ColName, PropertyInfo Property);

    public BaseRepository(IUnitOfWork uow)
    {
        _uow = uow;

        var type = typeof(T);

        // 表名：[Table].Name 或类名大写
        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        _tableName = tableAttr != null ? $"\"{tableAttr.Name}\"" : $"\"{type.Name.ToUpperInvariant()}\"";

        // 主键：[Key] 或名为 Id 的属性
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite) // 跳过只读属性（如 AvailableQuantity）
            .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null) // 跳过 [NotMapped] 属性
            .ToList();

        var keyProp = props.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null)
                      ?? props.FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));

        if (keyProp == null)
            throw new InvalidOperationException($"实体 {type.Name} 未找到主键属性");

        _keyProp = keyProp;
        _keyColumn = GetColumnName(keyProp);

        _allColumns = props.Select(p => new MappedColumn(p.Name, GetColumnName(p), p)).ToList();
        _columns = _allColumns.Where(c => c.PropName != _keyProp.Name).ToList();
    }

    // ==================== 读 ====================

    public virtual async Task<T?> GetByIdAsync(int id)
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
        var colNames = _columns.Select(c => c.ColName).ToList();
        var paramNames = _columns.Select(c => $":{c.PropName}").ToList();

        var sql = $"""
            INSERT INTO {_tableName} ({string.Join(", ", colNames)})
            VALUES ({string.Join(", ", paramNames)})
            RETURNING {_keyColumn} INTO :OutId
            """;

        var dp = new DynamicParameters();
        foreach (var col in _columns)
        {
            dp.Add($":{col.PropName}", col.Property.GetValue(entity));
        }
        dp.Add(":OutId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await _uow.Connection.ExecuteAsync(sql, dp, _uow.Transaction);

        // 将生成的主键写入实体
        var outId = dp.Get<object>(":OutId");
        _keyProp.SetValue(entity, Convert.ToInt32(outId));

        return entity;
    }

    public virtual void Update(T entity)
    {
        var setClauses = _columns.Select(c => $"{c.ColName} = :{c.PropName}");
        var sql = $"UPDATE {_tableName} SET {string.Join(", ", setClauses)} WHERE {_keyColumn} = :{_keyProp.Name}";

        var dp = new DynamicParameters();
        foreach (var col in _columns)
        {
            dp.Add($":{col.PropName}", col.Property.GetValue(entity));
        }
        dp.Add($":{_keyProp.Name}", _keyProp.GetValue(entity));

        _uow.Connection.Execute(sql, dp, _uow.Transaction);
    }

    public virtual void Delete(T entity)
    {
        var sql = $"DELETE FROM {_tableName} WHERE {_keyColumn} = :Id";
        _uow.Connection.Execute(sql, new { Id = _keyProp.GetValue(entity) }, _uow.Transaction);
    }

    public virtual Task SaveChangesAsync()
    {
        // Dapper 立即写入，事务由 UnitOfWork 管理；保持接口兼容
        return Task.CompletedTask;
    }

    // ==================== 工具方法 ====================

    /// <summary>
    /// 读取 [Column] 特性，没有则用属性名大写
    /// </summary>
    private static string GetColumnName(PropertyInfo prop)
    {
        var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
        return colAttr != null ? $"\"{colAttr.Name}\"" : $"\"{prop.Name.ToUpperInvariant()}\"";
    }
}
