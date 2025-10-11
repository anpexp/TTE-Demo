using Data.Entities;
using Data.Entities.Enums;

namespace Logica.Interfaces
{
    public interface IExternalMappingRepository
    {
        Task<ExternalMapping?> GetMappingAsync(string sourceId, ExternalSource source, string sourceType);
        Task<IEnumerable<ExternalMapping>> GetMappingsBySourceIdsAsync(IEnumerable<string> sourceIds, ExternalSource source, string sourceType);
        Task<Dictionary<string, Guid>> GetInternalIdMappingsAsync(IEnumerable<string> sourceIds, ExternalSource source, string sourceType);
        
        // Additional methods needed for user sync
        Task<ExternalMapping?> GetByExternalIdAsync(ExternalSource source, string sourceType, string sourceId);
        Task<ExternalMapping> CreateAsync(ExternalMapping mapping);
        Task<ExternalMapping> CreateOrUpdateAsync(ExternalMapping mapping);
        
        // Cleanup methods
        Task<int> RemoveOrphanedMappingsAsync(ExternalSource source, string sourceType);
    }
}