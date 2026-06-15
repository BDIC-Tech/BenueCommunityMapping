using System.Text.Json;
using BenueCommunityMapping.Data;
using BenueCommunityMapping.Services.Geography;
using BenueCommunityMapping.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BenueCommunityMapping.Controllers
{
    [Route("api/home/[action]")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class homeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IGeographyService _geoService;

        public homeController(AppDbContext context, IGeographyService geoService)
        {
            this._context = context;
            this._geoService = geoService;
        }
        public IActionResult Index()
        {
            return View();
        }

        // ── LGA DataTable ────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> GetLgas()
        {
            return await ExecuteDataTableAsync<BenueCommunityMapping.Services.Geography.LgaDto>(
                (s, l, q, c, d) => _geoService.GetLgasAsync(s, l, q, c, d),
                "Error loading LGAs");
        }

        // ── Ward DataTable ───────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> GetWards()
        {
            return await ExecuteDataTableAsync<BenueCommunityMapping.Services.Geography.WardDto>(
                (s, l, q, c, d) => _geoService.GetWardsAsync(s, l, q, c, d),
                "Error loading Wards");
        }

        // ── Kindred DataTable ────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> GetKindreds()
        {
            return await ExecuteDataTableAsync<BenueCommunityMapping.Services.Geography.KindredDto>(
                (s, l, q, c, d) => _geoService.GetKindredsAsync(s, l, q, c, d),
                "Error loading Kindreds");
        }

        // ── Community DataTable ──────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> GetCommunities()
        {
            return await ExecuteDataTableAsync<BenueCommunityMapping.Services.Geography.CommunityDto>(
                (s, l, q, c, d) => _geoService.GetCommunitiesAsync(s, l, q, c, d),
                "Error loading Communities");
        }

        // ── AJAX Toggle ──────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ToggleEntity(string type, int id)
        {
            try
            {
                string name = "";
                bool newState = false;
                switch (type)
                {
                    case "LGA":
                        var lga = await _context.LGAs.FindAsync(id);
                        if (lga is null) return Json(new { success = false, message = "LGA not found." });
                        lga.IsActive = !lga.IsActive; newState = lga.IsActive; name = lga.Name; break;
                    case "Ward":
                        var ward = await _context.Wards.FindAsync(id);
                        if (ward is null) return Json(new { success = false, message = "Ward not found." });
                        ward.IsActive = !ward.IsActive; newState = ward.IsActive; name = ward.Name; break;
                    case "Kindred":
                        var k = await _context.Kindreds.FindAsync(id);
                        if (k is null) return Json(new { success = false, message = "Kindred not found." });
                        k.IsActive = !k.IsActive; newState = k.IsActive; name = k.Name; break;
                    case "Community":
                        var c = await _context.Communities.FindAsync(id);
                        if (c is null) return Json(new { success = false, message = "Community not found." });
                        c.IsActive = !c.IsActive; newState = c.IsActive; name = c.Name; break;
                    default:
                        return Json(new { success = false, message = "Invalid entity type." });
                }
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"'{name}' is now {(newState ? "Active" : "Inactive")}." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Toggle failed: " + ex.Message });
            }
        }

        // ── AJAX Delete (with dependency check) ──────────────────────
        [HttpPost]
        public async Task<IActionResult> DeleteEntity(string type, int id)
        {
            try
            {
                switch (type)
                {
                    case "LGA":
                        var lga = await _context.LGAs.FindAsync(id);
                        if (lga is null) return Json(new { success = false, message = "LGA not found." });
                        var wardCount = await _context.Wards.CountAsync(w => w.LocalGovernmentAreaId == id);
                        if (wardCount > 0)
                            return Json(new { success = false, message = $"Cannot delete LGA '{lga.Name}' — it has {wardCount} ward(s). Please delete all wards under this LGA first." });
                        _context.LGAs.Remove(lga);
                        await _context.SaveChangesAsync();
                        return Json(new { success = true, message = $"LGA '{lga.Name}' deleted successfully." });

                    case "Ward":
                        var ward = await _context.Wards.FindAsync(id);
                        if (ward is null) return Json(new { success = false, message = "Ward not found." });
                        var kindredCount = await _context.Kindreds.CountAsync(k => k.WardId == id);
                        if (kindredCount > 0)
                            return Json(new { success = false, message = $"Cannot delete Ward '{ward.Name}' — it has {kindredCount} kindred(s). Please delete all kindreds under this ward first." });
                        _context.Wards.Remove(ward);
                        await _context.SaveChangesAsync();
                        return Json(new { success = true, message = $"Ward '{ward.Name}' deleted successfully." });

                    case "Kindred":
                        var k = await _context.Kindreds.FindAsync(id);
                        if (k is null) return Json(new { success = false, message = "Kindred not found." });
                        var commCount = await _context.Communities.CountAsync(c => c.KindredId == id);
                        if (commCount > 0)
                            return Json(new { success = false, message = $"Cannot delete Kindred '{k.Name}' — it has {commCount} community/ies. Please delete all communities under this kindred first." });
                        _context.Kindreds.Remove(k);
                        await _context.SaveChangesAsync();
                        return Json(new { success = true, message = $"Kindred '{k.Name}' deleted successfully." });

                    case "Community":
                        var c2 = await _context.Communities.FindAsync(id);
                        if (c2 is null) return Json(new { success = false, message = "Community not found." });
                        var subCount = await _context.Submissions.CountAsync(s => s.CommunityId == id);
                        if (subCount > 0)
                            return Json(new { success = false, message = $"Cannot delete Community '{c2.Name}' — it has {subCount} submission(s). Please delete or reassign all submissions first." });
                        _context.Communities.Remove(c2);
                        await _context.SaveChangesAsync();
                        return Json(new { success = true, message = $"Community '{c2.Name}' deleted successfully." });

                    default:
                        return Json(new { success = false, message = "Invalid entity type." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Delete failed: " + ex.Message });
            }
        }

        // ── Select2 AJAX: LGAs ───────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetLgaSelect2(string? q)
        {
            var list = await _geoService.GetAllLgasAsync();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.ToLower();
                list = list.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term)).ToList();
            }
            return Json(list.Select(l => new { id = l.Id, text = $"{l.Name} ({l.Code})" }));
        }

        // ── Select2 AJAX: Wards by LGA ───────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetWardSelect2(int lgaId, string? q)
        {
            var list = await _geoService.GetWardsByLgaAsync(lgaId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.ToLower();
                list = list.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term)).ToList();
            }
            return Json(list.Select(w => new { id = w.Id, text = $"{w.Name} ({w.Code})" }));
        }

        // ── Select2 AJAX: Kindreds by Ward ───────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetKindredSelect2(int wardId, string? q)
        {
            var list = await _geoService.GetKindredsByWardAsync(wardId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.ToLower();
                list = list.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term)).ToList();
            }
            return Json(list.Select(k => new { id = k.Id, text = $"{k.Name} ({k.Code})" }));
        }

        // ── Select2 AJAX: Communities by Kindred ────────────────────
        [HttpGet]
        public async Task<IActionResult> GetCommunitySelect2(int kindredId, string? q)
        {
            var list = await _geoService.GetCommunitiesByKindredAsync(kindredId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.ToLower();
                list = list.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term)).ToList();
            }
            return Json(list.Select(c => new { id = c.Id, text = $"{c.Name} ({c.Code})" }));
        }

        private async Task<IActionResult> ExecuteDataTableAsync<T>(Func<int, int, string, int, string, Task<(List<T> data, int recordsTotal, int recordsFiltered)>> serviceCall, string errorMessage = "An error occurred")
        {
            var request = ParseRequest();

            try
            {
                var (data, recordsTotal, recordsFiltered) = await serviceCall(
                    request.Start ?? 0,
                    request.Length ?? 10,
                    request.SearchValue ?? "",
                    request.SortColumn,
                    request.SortDirection ?? "asc");

                return new JsonResult(new
                {
                    draw = request.Draw,
                    recordsFiltered,
                    recordsTotal,
                    data
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
            catch (Exception ex)
            {
                return new JsonResult(new
                {
                    draw = request.Draw,
                    recordsFiltered = 0,
                    recordsTotal = 0,
                    data = new List<T>(),
                    error = errorMessage
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
        }

        private DataTableRequest ParseRequest()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Request.Form["start"].FirstOrDefault();
            var length = Request.Form["length"].FirstOrDefault();
            var sortColumnIndex = Request.Form["order[0][column]"].FirstOrDefault();

            var sortColumn = Request.Form[$"columns[{sortColumnIndex}][name]"].FirstOrDefault();
            var sortDirection = Request.Form["order[0][dir]"].FirstOrDefault();
            var searchValue = Request.Form["search[value]"].FirstOrDefault();

            return new DataTableRequest
            {
                Draw = int.TryParse(draw, out var d) ? d : 0,
                Start = string.IsNullOrEmpty(start) ? 0 : Convert.ToInt32(start),
                Length = string.IsNullOrEmpty(length) ? 0 : Convert.ToInt32(length),
                SortColumn = string.IsNullOrWhiteSpace(sortColumn) ? 0 : Convert.ToInt32(sortColumn),
                SortDirection = sortDirection,
                SearchValue = searchValue
            };
        }
    }
}
