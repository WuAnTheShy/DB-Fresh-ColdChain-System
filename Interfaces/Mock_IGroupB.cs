using DBFreshColdChain.Models.DTOs;
using DBFreshColdChain.Models.CrossGroup;
namespace DBFreshColdChain.Interfaces
{
    public interface Mock_IGroupB   //模拟B组提供的接口函数
    {
        public MockGroupCtoB_OrderInfo FindOrderInfo(string? OrderID); //给定订单编号，返回对应订单信息
        public MockGroupCtoB_OrderDetailInfo FindDetailInfo(string? DetailID);//给定明细编号，返回订单明细

        public void ChangeStatusToAllRefund(string? OrderID);   //给定订单号，将对应订单状态改为全部退款
        public void ChangeStatusToPartRefund(string? OrderID);   //给定订单号，将对应订单状态改为部分退款

        public void RollbackPoints(string? CustomerID,decimal ratio);   //给定消费者编号和回滚比率，回滚积分
    }

}
