using Data.Entities;
using Data.Entities.Enums;

namespace Logica.Interfaces
{
    public interface ICategoryRepository
    {
        // CRUD Operations
        Task<IEnumerable<Category>> GetAllAsync();
        Task<IEnumerable<Category>> GetApprovedAsync();
        Task<Category?> GetByIdAsync(Guid id);
        Task<Category?> GetBySlugAsync(string slug);
        Task<Category> AddAsync(Category category);
        Task<Category?> UpdateAsync(Category category);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> DeactivateAsync(Guid id);
        
        // Search Operations
        Task<IEnumerable<Category>> SearchAsync(string searchTerm);
        
        // Approval Operations
        Task<IEnumerable<Category>> GetPendingApprovalAsync();
        Task<bool> ApproveAsync(Guid id, Guid approvedBy);
        Task<bool> RejectAsync(Guid id);
        
        // Validation Operations
        Task<bool> ExistsByNameAsync(string name);
        Task<bool> ExistsBySlugAsync(string slug);
        Task<bool> ExistsByNameAsync(string name, Guid excludeId);
        Task<bool> ExistsBySlugAsync(string slug, Guid excludeId);
    }
}