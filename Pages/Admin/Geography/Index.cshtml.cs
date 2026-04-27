using BenueCommunityMapping.Data;
using BenueCommunityMapping.Models.Geography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BenueCommunityMapping.Pages.Admin.Geography
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        public IndexModel(AppDbContext db) => _db = db;

        public List<LocalGovernmentArea> LGAs        { get; private set; } = [];
        public List<Ward>                Wards       { get; private set; } = [];
        public List<Kindred>             Kindreds    { get; private set; } = [];
        public List<Community>           Communities { get; private set; } = [];

        // Counts for display
        public int LGACount       { get; private set; }
        public int WardCount      { get; private set; }
        public int KindredCount   { get; private set; }
        public int CommunityCount { get; private set; }

        public Dictionary<int, int> WardCounts       { get; private set; } = [];
        public Dictionary<int, int> KindredCounts    { get; private set; } = [];
        public Dictionary<int, int> CommunityCounts  { get; private set; } = [];
        public Dictionary<int, int> SubmissionCounts { get; private set; } = [];

        public async Task OnGetAsync()
        {
            LGAs = await _db.LGAs.OrderBy(l => l.Name).ToListAsync();
            Wards = await _db.Wards.Include(w => w.LocalGovernmentArea).OrderBy(w => w.Name).ToListAsync();
            Kindreds = await _db.Kindreds
                .Include(k => k.Ward).ThenInclude(w => w.LocalGovernmentArea)
                .OrderBy(k => k.Name).ToListAsync();
            Communities = await _db.Communities
                .Include(c => c.Kindred).ThenInclude(k => k.Ward).ThenInclude(w => w.LocalGovernmentArea)
                .OrderBy(c => c.Name).ToListAsync();

            LGACount       = LGAs.Count;
            WardCount      = Wards.Count;
            KindredCount   = Kindreds.Count;
            CommunityCount = Communities.Count;

            // Child counts per parent
            WardCounts      = await _db.Wards.GroupBy(w => w.LocalGovernmentAreaId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());
            KindredCounts   = await _db.Kindreds.GroupBy(k => k.WardId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());
            CommunityCounts = await _db.Communities.GroupBy(c => c.KindredId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());
            SubmissionCounts = await _db.Submissions.GroupBy(s => s.CommunityId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());
        }

        // ── Add LGA ──────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAddLGAAsync(string lgaName, string lgaCode)
        {
            if (string.IsNullOrWhiteSpace(lgaName) || string.IsNullOrWhiteSpace(lgaCode))
            { TempData["Error"] = "LGA name and code are required."; return RedirectToPage(); }

            _db.LGAs.Add(new LocalGovernmentArea { Name = lgaName.Trim(), Code = lgaCode.Trim().ToUpper() });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"LGA '{lgaName}' added.";
            return RedirectToPage();
        }

        // ── Add Ward ─────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAddWardAsync(int wardLgaId, string wardName, string wardCode)
        {
            if (string.IsNullOrWhiteSpace(wardName) || string.IsNullOrWhiteSpace(wardCode))
            { TempData["Error"] = "Ward name and code are required."; return RedirectToPage(); }

            _db.Wards.Add(new Ward
            {
                LocalGovernmentAreaId = wardLgaId,
                Name = wardName.Trim(),
                Code = wardCode.Trim().ToUpper()
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Ward '{wardName}' added.";
            return RedirectToPage();
        }

        // ── Add Kindred ───────────────────────────────────────────────
        public async Task<IActionResult> OnPostAddKindredAsync(int kindredWardId, string kindredName, string kindredCode)
        {
            if (string.IsNullOrWhiteSpace(kindredName) || string.IsNullOrWhiteSpace(kindredCode))
            { TempData["Error"] = "Kindred name and code are required."; return RedirectToPage(); }

            _db.Kindreds.Add(new Kindred
            {
                WardId = kindredWardId,
                Name   = kindredName.Trim(),
                Code   = kindredCode.Trim().ToUpper()
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Kindred '{kindredName}' added.";
            return RedirectToPage();
        }

        // ── Add Community ─────────────────────────────────────────────
        public async Task<IActionResult> OnPostAddCommunityAsync(
            int     communityKindredId,
            string  communityName,
            string  communityCode,
            string? communityEthnic,
            int?    communityPop)
        {
            if (string.IsNullOrWhiteSpace(communityName) || string.IsNullOrWhiteSpace(communityCode))
            { TempData["Error"] = "Community name and code are required."; return RedirectToPage(); }

            _db.Communities.Add(new Community
            {
                KindredId           = communityKindredId,
                Name                = communityName.Trim(),
                Code                = communityCode.Trim().ToUpper(),
                MajorEthnicGroups   = communityEthnic?.Trim(),
                EstimatedPopulation = communityPop
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Community '{communityName}' added.";
            return RedirectToPage();
        }

        // ── Toggle active/inactive ────────────────────────────────────
        public async Task<IActionResult> OnPostToggleLGAAsync(int id)
        {
            var lga = await _db.LGAs.FindAsync(id);
            if (lga is not null) { lga.IsActive = !lga.IsActive; await _db.SaveChangesAsync(); }
            TempData["Success"] = "LGA status updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleWardAsync(int id)
        {
            var ward = await _db.Wards.FindAsync(id);
            if (ward is not null) { ward.IsActive = !ward.IsActive; await _db.SaveChangesAsync(); }
            TempData["Success"] = "Ward status updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleKindredAsync(int id)
        {
            var k = await _db.Kindreds.FindAsync(id);
            if (k is not null) { k.IsActive = !k.IsActive; await _db.SaveChangesAsync(); }
            TempData["Success"] = "Kindred status updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleCommunityAsync(int id)
        {
            var c = await _db.Communities.FindAsync(id);
            if (c is not null) { c.IsActive = !c.IsActive; await _db.SaveChangesAsync(); }
            TempData["Success"] = "Community status updated.";
            return RedirectToPage();
        }

        // ── Edit LGA ──────────────────────────────────────────────────
        public async Task<IActionResult> OnPostEditLGAAsync(int editLgaId, string editLgaName, string editLgaCode)
        {
            if (string.IsNullOrWhiteSpace(editLgaName) || string.IsNullOrWhiteSpace(editLgaCode))
            { TempData["Error"] = "LGA name and code are required."; return RedirectToPage(); }

            var lga = await _db.LGAs.FindAsync(editLgaId);
            if (lga is null) { TempData["Error"] = "LGA not found."; return RedirectToPage(); }

            lga.Name = editLgaName.Trim();
            lga.Code = editLgaCode.Trim().ToUpper();
            await _db.SaveChangesAsync();
            TempData["Success"] = $"LGA '{lga.Name}' updated.";
            return RedirectToPage();
        }

        // ── Edit Ward ─────────────────────────────────────────────────
        public async Task<IActionResult> OnPostEditWardAsync(int editWardId, int editWardLgaId, string editWardName, string editWardCode)
        {
            if (string.IsNullOrWhiteSpace(editWardName) || string.IsNullOrWhiteSpace(editWardCode))
            { TempData["Error"] = "Ward name and code are required."; return RedirectToPage(); }

            var ward = await _db.Wards.FindAsync(editWardId);
            if (ward is null) { TempData["Error"] = "Ward not found."; return RedirectToPage(); }

            ward.LocalGovernmentAreaId = editWardLgaId;
            ward.Name = editWardName.Trim();
            ward.Code = editWardCode.Trim().ToUpper();
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Ward '{ward.Name}' updated.";
            return RedirectToPage();
        }

        // ── Edit Kindred ──────────────────────────────────────────────
        public async Task<IActionResult> OnPostEditKindredAsync(int editKindredId, int editKindredWardId, string editKindredName, string editKindredCode)
        {
            if (string.IsNullOrWhiteSpace(editKindredName) || string.IsNullOrWhiteSpace(editKindredCode))
            { TempData["Error"] = "Kindred name and code are required."; return RedirectToPage(); }

            var k = await _db.Kindreds.FindAsync(editKindredId);
            if (k is null) { TempData["Error"] = "Kindred not found."; return RedirectToPage(); }

            k.WardId = editKindredWardId;
            k.Name = editKindredName.Trim();
            k.Code = editKindredCode.Trim().ToUpper();
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Kindred '{k.Name}' updated.";
            return RedirectToPage();
        }

        // ── Edit Community ────────────────────────────────────────────
        public async Task<IActionResult> OnPostEditCommunityAsync(
            int editCommunityId, int editCommunityKindredId, string editCommunityName,
            string editCommunityCode, string? editCommunityEthnic, int? editCommunityPop)
        {
            if (string.IsNullOrWhiteSpace(editCommunityName) || string.IsNullOrWhiteSpace(editCommunityCode))
            { TempData["Error"] = "Community name and code are required."; return RedirectToPage(); }

            var c = await _db.Communities.FindAsync(editCommunityId);
            if (c is null) { TempData["Error"] = "Community not found."; return RedirectToPage(); }

            c.KindredId = editCommunityKindredId;
            c.Name = editCommunityName.Trim();
            c.Code = editCommunityCode.Trim().ToUpper();
            c.MajorEthnicGroups = editCommunityEthnic?.Trim();
            c.EstimatedPopulation = editCommunityPop;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Community '{c.Name}' updated.";
            return RedirectToPage();
        }

        // ── Delete handlers ───────────────────────────────────────────
        public async Task<IActionResult> OnPostDeleteLGAAsync(int id)
        {
            var lga = await _db.LGAs.FindAsync(id);
            if (lga is null) { TempData["Error"] = "LGA not found."; return RedirectToPage(); }
            if (await _db.Wards.AnyAsync(w => w.LocalGovernmentAreaId == id))
            { TempData["Error"] = "Cannot delete LGA — it has wards. Remove wards first."; return RedirectToPage(); }

            _db.LGAs.Remove(lga);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"LGA '{lga.Name}' deleted.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteWardAsync(int id)
        {
            var ward = await _db.Wards.FindAsync(id);
            if (ward is null) { TempData["Error"] = "Ward not found."; return RedirectToPage(); }
            if (await _db.Kindreds.AnyAsync(k => k.WardId == id))
            { TempData["Error"] = "Cannot delete Ward — it has kindreds. Remove kindreds first."; return RedirectToPage(); }

            _db.Wards.Remove(ward);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Ward '{ward.Name}' deleted.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteKindredAsync(int id)
        {
            var k = await _db.Kindreds.FindAsync(id);
            if (k is null) { TempData["Error"] = "Kindred not found."; return RedirectToPage(); }
            if (await _db.Communities.AnyAsync(c => c.KindredId == id))
            { TempData["Error"] = "Cannot delete Kindred — it has communities. Remove communities first."; return RedirectToPage(); }

            _db.Kindreds.Remove(k);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Kindred '{k.Name}' deleted.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteCommunityAsync(int id)
        {
            var c = await _db.Communities.FindAsync(id);
            if (c is null) { TempData["Error"] = "Community not found."; return RedirectToPage(); }
            if (await _db.Submissions.AnyAsync(s => s.CommunityId == id))
            { TempData["Error"] = "Cannot delete Community — it has submissions. Remove submissions first."; return RedirectToPage(); }

            _db.Communities.Remove(c);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Community '{c.Name}' deleted.";
            return RedirectToPage();
        }
    }
}
