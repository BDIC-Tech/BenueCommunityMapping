using BenueCommunityMapping.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BenueCommunityMapping.Pages.Api
{
    /// <summary>
    /// Minimal JSON API endpoints for the cascading geographic dropdowns.
    /// Routes: /api/geo/wards, /api/geo/kindreds, /api/geo/communities
    /// </summary>
    [IgnoreAntiforgeryToken]
    public class GeoModel : PageModel
    {
        private readonly AppDbContext _db;
        public GeoModel(AppDbContext db) => _db = db;

        public void OnGet() { }

        // GET /api/geo/lgas
        public async Task<IActionResult> OnGetLgasAsync()
        {
            var lgas = await _db.LGAs
                .Where(l => l.IsActive)
                .OrderBy(l => l.Name)
                .Select(l => new { lgaId = l.Id, l.Name, l.Code })
                .ToListAsync();
            return new JsonResult(lgas);
        }

        // GET /api/geo/wards?lgaId=1
        public async Task<IActionResult> OnGetWardsAsync(int lgaId)
        {
            var wards = await _db.Wards
                .Where(w => w.LocalGovernmentAreaId == lgaId && w.IsActive)
                .OrderBy(w => w.Name)
                .Select(w => new { wardId = w.Id, w.Name, w.Code })
                .ToListAsync();
            return new JsonResult(wards);
        }

        // GET /api/geo/kindreds?wardId=1
        public async Task<IActionResult> OnGetKindredsAsync(int wardId)
        {
            var kindreds = await _db.Kindreds
                .Where(k => k.WardId == wardId && k.IsActive)
                .OrderBy(k => k.Name)
                .Select(k => new { kindredId = k.Id, k.Name, k.Code })
                .ToListAsync();
            return new JsonResult(kindreds);
        }

        // GET /api/geo/communities?kindredId=1
        public async Task<IActionResult> OnGetCommunitiesAsync(int kindredId)
        {
            var communities = await _db.Communities
                .Where(c => c.KindredId == kindredId && c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new { communityId = c.Id, c.Name, c.Code, c.EstimatedPopulation })
                .ToListAsync();
            return new JsonResult(communities);
        }
    }
}
