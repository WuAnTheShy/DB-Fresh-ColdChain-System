using FreshColdChain.Models.DTOs;
using FreshColdChain.Models;

namespace FreshColdChain.Interfaces
{
    public interface Mock_IGroupA   //模拟A组提供的接口函数
    {
        public MockGroupCtoA_ProductInfo FindProductInfo(string? ProductID); //给定商品编号，返回商品默认价格和对应供应商编号

        public void RollbackStock(MockGroupCtoB_OrderDetailInfo detailInfo);   

    }

}
