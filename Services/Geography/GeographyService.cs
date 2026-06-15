using BenueCommunityMapping.Data;
using BenueCommunityMapping.Models.Geography;
using Microsoft.EntityFrameworkCore;

namespace BenueCommunityMapping.Services.Geography
{
    public class GeographyService : IGeographyService
    {
        private readonly AppDbContext _db;

        public GeographyService(AppDbContext db)
        {
            _db = db;
        }

        // ── LGAs ──────────────────────────────────────────────────────
        public async Task<(List<LgaDto> data, int recordsTotal, int recordsFiltered)> GetLgasAsync(
            int start, int length, string search, int sortColumn, string sortDir)
        {
            var query = _db.LGAs.AsNoTracking();

            var recordsTotal = await query.CountAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(l => l.Name.ToLower().Contains(s) || l.Code.ToLower().Contains(s));
            }

            var recordsFiltered = await query.CountAsync();

            query = ApplyLgaSort(query, sortColumn, sortDir);
            var paged = await query.Skip(start).Take(length).ToListAsync();

            var wardCounts = await _db.Wards
                .GroupBy(w => w.LocalGovernmentAreaId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var data = paged.Select(l => new LgaDto(
                l.Id, l.Name, l.Code, l.IsActive,
                wardCounts.GetValueOrDefault(l.Id, 0))).ToList();

            return (data, recordsTotal, recordsFiltered);
        }

        // ── Wards ──────────────────────────────────────────────────────
        public async Task<(List<WardDto> data, int recordsTotal, int recordsFiltered)> GetWardsAsync(
            int start, int length, string search, int sortColumn, string sortDir)
        {
            IQueryable<Ward> query = _db.Wards.AsNoTracking().Include(w => w.LocalGovernmentArea);

            var recordsTotal = await query.CountAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(w => w.Name.ToLower().Contains(s)
                    || w.Code.ToLower().Contains(s)
                    || w.LocalGovernmentArea.Name.ToLower().Contains(s));
            }

            var recordsFiltered = await query.CountAsync();

            query = ApplyWardSort(query, sortColumn, sortDir);
            var paged = await query.Skip(start).Take(length).ToListAsync();

            var kindredCounts = await _db.Kindreds
                .GroupBy(k => k.WardId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var data = paged.Select(w => new WardDto(
                w.Id, w.Name, w.Code, w.IsActive,
                w.LocalGovernmentArea?.Name ?? "",
                kindredCounts.GetValueOrDefault(w.Id, 0))).ToList();

            return (data, recordsTotal, recordsFiltered);
        }

        // ── Kindreds ───────────────────────────────────────────────────
        public async Task<(List<KindredDto> data, int recordsTotal, int recordsFiltered)> GetKindredsAsync(
            int start, int length, string search, int sortColumn, string sortDir)
        {
            IQueryable<Kindred> query = _db.Kindreds.AsNoTracking()
                .Include(k => k.Ward).ThenInclude(w => w.LocalGovernmentArea);

            var recordsTotal = await query.CountAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(k => k.Name.ToLower().Contains(s)
                    || k.Code.ToLower().Contains(s)
                    || k.Ward.Name.ToLower().Contains(s)
                    || (k.Ward.LocalGovernmentArea != null && k.Ward.LocalGovernmentArea.Name.ToLower().Contains(s)));
            }

            var recordsFiltered = await query.CountAsync();

            query = ApplyKindredSort(query, sortColumn, sortDir);
            var paged = await query.Skip(start).Take(length).ToListAsync();

            var communityCounts = await _db.Communities
                .GroupBy(c => c.KindredId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var data = paged.Select(k => new KindredDto(
                k.Id, k.Name, k.Code, k.IsActive,
                k.Ward?.Name ?? "",
                k.Ward?.LocalGovernmentArea?.Name ?? "",
                communityCounts.GetValueOrDefault(k.Id, 0))).ToList();

            return (data, recordsTotal, recordsFiltered);
        }

        // ── Communities ────────────────────────────────────────────────
        public async Task<(List<CommunityDto> data, int recordsTotal, int recordsFiltered)> GetCommunitiesAsync(
            int start, int length, string search, int sortColumn, string sortDir)
        {
            IQueryable<Community> query = _db.Communities.AsNoTracking()
                .Include(c => c.Kindred).ThenInclude(k => k.Ward).ThenInclude(w => w.LocalGovernmentArea);

            var recordsTotal = await query.CountAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(s)
                    || c.Code.ToLower().Contains(s)
                    || c.Kindred.Name.ToLower().Contains(s)
                    || c.Kindred.Ward.Name.ToLower().Contains(s)
                    || (c.Kindred.Ward.LocalGovernmentArea != null && c.Kindred.Ward.LocalGovernmentArea.Name.ToLower().Contains(s)));
            }

            var recordsFiltered = await query.CountAsync();

            query = ApplyCommunitySort(query, sortColumn, sortDir);
            var paged = await query.Skip(start).Take(length).ToListAsync();

            var submissionCounts = await _db.Submissions
                .GroupBy(s => s.CommunityId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var data = paged.Select(c => new CommunityDto(
                c.Id, c.Name, c.Code, c.IsActive,
                c.Kindred?.Name ?? "",
                c.Kindred?.Ward?.Name ?? "",
                c.Kindred?.Ward?.LocalGovernmentArea?.Name ?? "",
                c.EstimatedPopulation,
                submissionCounts.GetValueOrDefault(c.Id, 0))).ToList();

            return (data, recordsTotal, recordsFiltered);
        }

        // ── Select2 helpers ───────────────────────────────────────────
        public async Task<IReadOnlyList<LgaDto>> GetAllLgasAsync()
        {
            return await _db.LGAs.AsNoTracking()
                .Where(l => l.IsActive)
                .OrderBy(l => l.Name)
                .Select(l => new LgaDto(l.Id, l.Name, l.Code, l.IsActive, 0))
                .ToListAsync();
        }

        public async Task<IReadOnlyList<WardDto>> GetWardsByLgaAsync(int lgaId)
        {
            return await _db.Wards.AsNoTracking()
                .Where(w => w.LocalGovernmentAreaId == lgaId && w.IsActive)
                .OrderBy(w => w.Name)
                .Select(w => new WardDto(w.Id, w.Name, w.Code, w.IsActive, "", 0))
                .ToListAsync();
        }

        public async Task<IReadOnlyList<KindredDto>> GetKindredsByWardAsync(int wardId)
        {
            return await _db.Kindreds.AsNoTracking()
                .Where(k => k.WardId == wardId && k.IsActive)
                .OrderBy(k => k.Name)
                .Select(k => new KindredDto(k.Id, k.Name, k.Code, k.IsActive, "", "", 0))
                .ToListAsync();
        }

        public async Task<IReadOnlyList<CommunityDto>> GetCommunitiesByKindredAsync(int kindredId)
        {
            return await _db.Communities.AsNoTracking()
                .Where(c => c.KindredId == kindredId && c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new CommunityDto(c.Id, c.Name, c.Code, c.IsActive, "", "", "", null, 0))
                .ToListAsync();
        }

        // ── Sorting helpers ────────────────────────────────────────────
        private static IQueryable<LocalGovernmentArea> ApplyLgaSort(
            IQueryable<LocalGovernmentArea> q, int col, string dir)
        {
            return col switch
            {
                1 => dir == "asc" ? q.OrderBy(l => l.Name) : q.OrderByDescending(l => l.Name),
                2 => dir == "asc" ? q.OrderBy(l => l.Code) : q.OrderByDescending(l => l.Code),
                _ => dir == "asc" ? q.OrderBy(l => l.Id) : q.OrderByDescending(l => l.Id)
            };
        }

        private static IQueryable<Ward> ApplyWardSort(IQueryable<Ward> q, int col, string dir)
        {
            return col switch
            {
                1 => dir == "asc" ? q.OrderBy(w => w.Name) : q.OrderByDescending(w => w.Name),
                2 => dir == "asc" ? q.OrderBy(w => w.Code) : q.OrderByDescending(w => w.Code),
                3 => dir == "asc" ? q.OrderBy(w => w.LocalGovernmentArea!.Name) : q.OrderByDescending(w => w.LocalGovernmentArea!.Name),
                _ => dir == "asc" ? q.OrderBy(w => w.Id) : q.OrderByDescending(w => w.Id)
            };
        }

        private static IQueryable<Kindred> ApplyKindredSort(IQueryable<Kindred> q, int col, string dir)
        {
            return col switch
            {
                1 => dir == "asc" ? q.OrderBy(k => k.Name) : q.OrderByDescending(k => k.Name),
                2 => dir == "asc" ? q.OrderBy(k => k.Code) : q.OrderByDescending(k => k.Code),
                3 => dir == "asc" ? q.OrderBy(k => k.Ward!.Name) : q.OrderByDescending(k => k.Ward!.Name),
                4 => dir == "asc" ? q.OrderBy(k => k.Ward!.LocalGovernmentArea!.Name) : q.OrderByDescending(k => k.Ward!.LocalGovernmentArea!.Name),
                _ => dir == "asc" ? q.OrderBy(k => k.Id) : q.OrderByDescending(k => k.Id)
            };
        }

        private static IQueryable<Community> ApplyCommunitySort(IQueryable<Community> q, int col, string dir)
        {
            return col switch
            {
                1 => dir == "asc" ? q.OrderBy(c => c.Name) : q.OrderByDescending(c => c.Name),
                2 => dir == "asc" ? q.OrderBy(c => c.Code) : q.OrderByDescending(c => c.Code),
                3 => dir == "asc" ? q.OrderBy(c => c.Kindred!.Name) : q.OrderByDescending(c => c.Kindred!.Name),
                4 => dir == "asc" ? q.OrderBy(c => c.Kindred!.Ward!.Name) : q.OrderByDescending(c => c.Kindred!.Ward!.Name),
                5 => dir == "asc" ? q.OrderBy(c => c.Kindred!.Ward!.LocalGovernmentArea!.Name) : q.OrderByDescending(c => c.Kindred!.Ward!.LocalGovernmentArea!.Name),
                _ => dir == "asc" ? q.OrderBy(c => c.Id) : q.OrderByDescending(c => c.Id)
            };
        }
    }
}
