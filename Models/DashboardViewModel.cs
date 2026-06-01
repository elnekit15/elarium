namespace DesignerStore.Models;

public class DashboardViewModel
{
    // KPI
    public decimal TotalRevenue    { get; set; }
    public int     TotalOrders     { get; set; }
    public int     PaidOrders      { get; set; }
    public decimal AvgOrderValue   { get; set; }
    public int     UniqueCustomers { get; set; }
    public int     TotalProducts   { get; set; }

    // Charts / lists
    public List<MonthlyRevenueDto>       RevenueByMonth        { get; set; } = new();   // поточний рік
    public List<MonthlyRevenueDto>       RevenueByMonthRolling { get; set; } = new();   // останні 12 місяців
    public List<TopProductDto>           TopProducts     { get; set; } = new();
    public Dictionary<OrderStatus, int>  StatusBreakdown { get; set; } = new();
    public List<Order>                   RecentOrders    { get; set; } = new();
}

public class MonthlyRevenueDto
{
    public string  Label  { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int     Count  { get; set; }
}

public class TopProductDto
{
    public int     ProductId    { get; set; }
    public string  Name         { get; set; } = string.Empty;
    public string? ImageUrl     { get; set; }
    public int     QuantitySold { get; set; }
    public decimal Revenue      { get; set; }
}
