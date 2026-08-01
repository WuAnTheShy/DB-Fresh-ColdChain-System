//基础设施类
//数据库连接工厂接口（定义创建数据库连接的方法）
using System.Data;

namespace FreshGroupSystem.Repositories;

/// <summary>
/// 数据库连接工厂接口
/// </summary>
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
