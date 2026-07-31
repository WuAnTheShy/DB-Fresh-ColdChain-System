using System.Data;

namespace FreshGroupSystem.Repositories;

/// <summary>
/// 数据库连接工厂接口
/// </summary>
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
