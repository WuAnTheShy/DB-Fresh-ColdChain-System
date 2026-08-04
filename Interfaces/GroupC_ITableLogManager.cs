using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Models.CrossGroup;
namespace DBFreshColdChain.Interfaces
{
    public interface GroupC_ITableLogManager
    {
        bool WriteTableChangeLog(GroupC_LogAuditrails? logData = null);




    }
}
