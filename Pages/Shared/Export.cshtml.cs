using BenueCommunityMapping.Services.Analytics;
using BenueCommunityMapping.Services.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BenueCommunityMapping.Pages.Shared
{
    /// <summary>
    /// Shared export endpoint — accessible from Admin/Analytics and DataAnalysis pages.
    /// GET: /Shared/Export?section=health&format=excel&lgaId=1&...
    /// </summary>
    [Authorize(Roles = "Admin,Coordinator")]
    public class ExportModel : PageModel
    {
        private readonly IExportService _export;
        public ExportModel(IExportService export) => _export = export;

        public async Task<IActionResult> OnGetAsync(
            string section, string format,
            int? lgaId, int? wardId, int? kindredId, int? communityId,
            string? from, string? to)
        {
            if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(format))
                return BadRequest("section and format are required.");

            var filter = new AnalyticsFilter
            {
                LGAId       = lgaId,
                WardId      = wardId,
                KindredId   = kindredId,
                CommunityId = communityId,
                FromDate    = DateTime.TryParse(from, out var fd) ? fd : null,
                ToDate      = DateTime.TryParse(to,   out var td) ? td : null,
            };

            var safeSection = section.Replace(" ", "_");
            var timestamp   = DateTime.Now.ToString("yyyyMMdd_HHmm");
            var baseName    = $"BCM_{safeSection}_{timestamp}";

            return format.ToLower() switch
            {
                "excel" => File(await _export.ExportExcelAsync(section, filter),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{baseName}.xlsx"),

                "csv" => File(await _export.ExportCsvAsync(section, filter),
                    "text/csv", $"{baseName}.csv"),

                "pdf" => File(await _export.ExportPdfAsync(section, filter),
                    "application/pdf", $"{baseName}.pdf"),

                "docx" => File(await _export.ExportDocxAsync(section, filter),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"{baseName}.docx"),

                _ => BadRequest($"Unsupported format: {format}")
            };
        }
    }
}
