namespace ShopNexa.Models;

public class SellerDashboardStats
{
    // Product Stats
    public int TotalProducts { get; set; }
    public int LowStockProducts { get; set; }
    public int OutOfStockProducts { get; set; }

    // Order Stats
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int ConfirmedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int DeliveredOrders { get; set; }

    // Customer Stats
    public int TotalCustomers { get; set; }

    // Revenue Stats
    public decimal TotalRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }

    // Recent Data
    public List<RecentOrderViewModel> RecentOrders { get; set; } = new();
    public List<TopProductViewModel> TopProducts { get; set; } = new();
}

public class RecentOrderViewModel
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TopProductViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int SoldCount { get; set; }
}
