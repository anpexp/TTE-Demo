using Data;
using Data.Entities;
using Data.Entities.Enums;
using Logica.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Logica.Repositories
{
    public class ExternalMappingRepository : IExternalMappingRepository
    {
        private readonly AppDbContext _context;

        public ExternalMappingRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ExternalMapping?> GetMappingAsync(string sourceId, ExternalSource source, string sourceType)
        {
            return await _context.ExternalMappings
                .FirstOrDefaultAsync(em => em.SourceId == sourceId && 
                                          em.Source == source && 
                                          em.SourceType == sourceType);
        }

        public async Task<IEnumerable<ExternalMapping>> GetMappingsBySourceIdsAsync(IEnumerable<string> sourceIds, ExternalSource source, string sourceType)
        {
            return await _context.ExternalMappings
                .Where(em => sourceIds.Contains(em.SourceId) && 
                            em.Source == source && 
                            em.SourceType == sourceType)
                .ToListAsync();
        }

        public async Task<Dictionary<string, Guid>> GetInternalIdMappingsAsync(IEnumerable<string> sourceIds, ExternalSource source, string sourceType)
        {
            var mappings = await GetMappingsBySourceIdsAsync(sourceIds, source, sourceType);
            return mappings.ToDictionary(m => m.SourceId, m => m.InternalId);
        }

        public async Task<ExternalMapping?> GetByExternalIdAsync(ExternalSource source, string sourceType, string sourceId)
        {
            return await _context.ExternalMappings
                .FirstOrDefaultAsync(em => em.Source == source && 
                                          em.SourceType == sourceType && 
                                          em.SourceId == sourceId);
        }

        public async Task<ExternalMapping> CreateAsync(ExternalMapping mapping)
        {
            _context.ExternalMappings.Add(mapping);
            await _context.SaveChangesAsync();
            return mapping;
        }

        // Additional method for upsert operations
        public async Task<ExternalMapping> CreateOrUpdateAsync(ExternalMapping mapping)
        {
            var existing = await GetByExternalIdAsync(mapping.Source, mapping.SourceType, mapping.SourceId);
            
            if (existing != null)
            {
                existing.InternalId = mapping.InternalId;
                existing.SnapshotJson = mapping.SnapshotJson;
                existing.ImportedAt = mapping.ImportedAt;
                _context.ExternalMappings.Update(existing);
            }
            else
            {
                _context.ExternalMappings.Add(mapping);
            }
            
            await _context.SaveChangesAsync();
            return existing ?? mapping;
        }

        public async Task<int> RemoveOrphanedMappingsAsync(ExternalSource source, string sourceType)
        {
            // Find mappings where the referenced internal entity doesn't exist
            var orphanedMappings = sourceType.ToUpper() switch
            {
                "PRODUCT" => await _context.ExternalMappings
                    .Where(em => em.Source == source && em.SourceType == sourceType)
                    .Where(em => !_context.Products.Any(p => p.Id == em.InternalId))
                    .ToListAsync(),
                
                "USER" => await _context.ExternalMappings
                    .Where(em => em.Source == source && em.SourceType == sourceType)
                    .Where(em => !_context.Users.Any(u => u.Id == em.InternalId))
                    .ToListAsync(),
                
                "CATEGORY" => await _context.ExternalMappings
                    .Where(em => em.Source == source && em.SourceType == sourceType)
                    .Where(em => !_context.Categories.Any(c => c.Id == em.InternalId))
                    .ToListAsync(),
                
                _ => new List<ExternalMapping>()
            };

            if (orphanedMappings.Any())
            {
                _context.ExternalMappings.RemoveRange(orphanedMappings);
                await _context.SaveChangesAsync();
            }

            return orphanedMappings.Count;
        }
    }
}