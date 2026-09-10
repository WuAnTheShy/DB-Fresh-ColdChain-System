namespace FreshColdChain.Models;

// 收货地址的原子成分与展示串转换。
// <para>
// 数据库侧（Biz_Orders）按要求把收货地址拆成
// RECEIVERPROVINCE / RECEIVERCITY / RECEIVERDISTRICT / RECEIVERDETAILADDRESS
// 四个原子列，本类提供唯一的「拼回一段地址」实现 ——
// 订单实体、订单详情投影、供应商履约列表投影的只读 ShippingAddress
// 都走这里，避免拼接规则散落多处。
// </para>
public static class ReceiverAddress
{
    // 省 市 区 详址 拼成一段展示串（空白段自动跳过，与拆分前的历史值保持同一格式）。
    public static string Format(
        string? province,
        string? city,
        string? district,
        string? detailAddress)
    {
        return string.Join(
            " ",
            new[] { province, city, district, detailAddress }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
