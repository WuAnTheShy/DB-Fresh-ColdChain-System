using DBFreshColdChain.Models;

namespace DBFreshColdChain.Interfaces
{
    public interface GroupC_ITableLogManager
    {
        bool WriteTableChangeLog(Log_Auditrails? logData = null);




    }
}
