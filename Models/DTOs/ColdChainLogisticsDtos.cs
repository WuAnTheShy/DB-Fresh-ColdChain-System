namespace FreshGroupSystem.Models.DTOs;

public class FreightItemDto { public string ProductID { get; set; } = string.Empty; public int Quantity { get; set; } }
public class FreightQuoteRequest { public string Province { get; set; } = string.Empty; public string City { get; set; } = string.Empty; public string District { get; set; } = string.Empty; public decimal GoodsAmount { get; set; } public List<FreightItemDto> Items { get; set; } = []; }
public class ShipmentRequest { public string OrderID { get; set; } = string.Empty; public string SupplierID { get; set; } = string.Empty; public List<FreightItemDto> Items { get; set; } = []; }
public class FreightQuoteDto { public decimal FreightAmount { get; set; } public string RuleSummary { get; set; } = string.Empty; }
