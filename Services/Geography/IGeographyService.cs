using BenueCommunityMapping.Models.Geography;

namespace BenueCommunityMapping.Services.Geography
{
    public record LgaDto(int Id, string Name, string Code, bool IsActive, int WardCount);
    public record WardDto(int Id, string Name, string Code, bool IsActive, string LgaName, int KindredCount);
    public record KindredDto(int Id, string Name, string Code, bool IsActive, string WardName, string LgaName, int CommunityCount);
    public record CommunityDto(int Id, string Name, string Code, bool IsActive, string KindredName, string WardName, string LgaName, int? EstimatedPopulation, int SubmissionCount);

    public interface IGeographyService
    {
        Task<(List<LgaDto> data, int recordsTotal, int recordsFiltered)> GetLgasAsync(int start, int length, string search, int sortColumn, string sortDir);
        Task<(List<WardDto> data, int recordsTotal, int recordsFiltered)> GetWardsAsync(int start, int length, string search, int sortColumn, string sortDir);
        Task<(List<KindredDto> data, int recordsTotal, int recordsFiltered)> GetKindredsAsync(int start, int length, string search, int sortColumn, string sortDir);
        Task<(List<CommunityDto> data, int recordsTotal, int recordsFiltered)> GetCommunitiesAsync(int start, int length, string search, int sortColumn, string sortDir);

        Task<IReadOnlyList<LgaDto>> GetAllLgasAsync();
        Task<IReadOnlyList<WardDto>> GetWardsByLgaAsync(int lgaId);
        Task<IReadOnlyList<KindredDto>> GetKindredsByWardAsync(int wardId);
        Task<IReadOnlyList<CommunityDto>> GetCommunitiesByKindredAsync(int kindredId);
    }
}
