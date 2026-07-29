using DBFreshColdChain.Repositories;

namespace DBFreshColdChain.Services
{
    public class RefundManager
    {
        private readonly DbHelper _dbHelper;

        public RefundManager(DbHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }
        
        public void Refund() //整体处理退款函数
        {

        }
    }
}
