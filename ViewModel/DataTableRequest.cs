namespace BenueCommunityMapping.ViewModel
{
    public class DataTableRequest
    {
        public int? Draw { get; set; }
        public int? Start { get; set; }
        public int? Length { get; set; }
        public int SortColumn { get; set; }
        public string SortDirection { get; set; }
        public string SearchValue { get; set; }
        public SearchInfo Search { get; set; }
        public List<OrderInfo> Order { get; set; }
        public Dictionary<string, object> AdditionalParameters { get; set; }
    }

    public class SearchInfo
    {
        public string Value { get; set; }
    }

    public class OrderInfo
    {
        public int Column { get; set; }
        public string Dir { get; set; }
    }
}
