namespace BenueCommunityMapping.Helpers
{
    /// <summary>
    /// A lightweight wrapper around a paged slice of data.
    /// Used by every list page to feed the cloudscribe cs-pager tag helper.
    /// </summary>
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Data       { get; }
        public int              TotalItems { get; }
        public int              PageNumber { get; }
        public int              PageSize   { get; }

        public PagedResult(IReadOnlyList<T> allItems, int pageNumber, int pageSize)
        {
            PageSize   = pageSize;
            TotalItems = allItems.Count;
            PageNumber = pageNumber < 1 ? 1 : pageNumber;
            Data       = allItems.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList();
        }
    }
}
