using BenueCommunityMapping.Services.Analytics;

namespace BenueCommunityMapping.Services.Export
{
    /// <summary>
    /// Exports analytics data in multiple formats (Excel, CSV, PDF, DOCX).
    /// Used by both Admin/Analytics and DataAnalysis pages.
    /// </summary>
    public interface IExportService
    {
        /// <summary>Export a section's metrics as Excel (.xlsx).</summary>
        Task<byte[]> ExportExcelAsync(string section, AnalyticsFilter filter);

        /// <summary>Export a section's metrics as CSV.</summary>
        Task<byte[]> ExportCsvAsync(string section, AnalyticsFilter filter);

        /// <summary>Export a section's metrics as PDF.</summary>
        Task<byte[]> ExportPdfAsync(string section, AnalyticsFilter filter);

        /// <summary>Export a section's metrics as DOCX.</summary>
        Task<byte[]> ExportDocxAsync(string section, AnalyticsFilter filter);

        /// <summary>List of available section keys for export.</summary>
        IReadOnlyList<(string Key, string Label)> AvailableSections { get; }
    }
}
