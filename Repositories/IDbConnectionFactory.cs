//基础设施类
//数据库连接工厂接口（定义创建数据库连接的方法）
using System.Data;

namespace FreshColdChain.Repositories;

// 数据库连接工厂接口
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
