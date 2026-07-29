using DBFreshColdChain.Repositories;

namespace DBFreshColdChain.Services
{

    public class PaymentManager
    {
        private readonly DbHelper _dbHelper;

        public PaymentManager(DbHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }
    }

}
