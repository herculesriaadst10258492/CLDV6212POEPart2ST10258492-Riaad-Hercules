// ABCRetailByRH/ViewModels/OrderVm.cs
namespace ABCRetailByRH.ViewModels
{
    public class OrderVm
    {
        public string OrderId { get; set; } = "";
        public string Customer { get; set; } = "";
        public double Total { get; set; }
        public string Status { get; set; } = "";
        public System.DateTime? CreatedUtc { get; set; }
        public System.DateTime? ProcessedUtc { get; set; }
    }
}
