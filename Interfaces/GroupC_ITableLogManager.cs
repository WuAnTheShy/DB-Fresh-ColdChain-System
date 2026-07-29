using DBFreshColdChain.Models;

namespace DBFreshColdChain.Services
{
    public interface GroupC_ITableLogManager
    {
        bool WriteTableChangeLog(Log_Auditrails? logData = null);




    }
}
