using Data;
using Data.Entities;
using Data.Entities.Enums;
using Logica.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Logica.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly AppDbContext _context;

        public CategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            return await _context.Categories
                .Include(c => c.Creator)
                .Include(c => c.Approver)
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetApprovedAsync()
        {
            return await _context.Categories
                .Include(c => c.Creator)
                .Include(c => c.Approver)
                .Include(c => c.Products)
                .Where(c => c.State == ApprovalState.Approved)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Category?> GetByIdAsync(Guid id)
        {
            return await _context.Categories
                .Include(c => c.Creator)
                .Include(c => c.Approver)
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Category?> GetBySlugAsync(string slug)
        {
            return await _context.Categories
                .Include(c => c.Creator)
                .Include(c => c.Approver)
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Slug == slug);
        }

        public async Task<Category> AddAsync(Category category)
        {
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            
            // Reload with navigation properties
            return await GetByIdAsync(category.Id) ?? category;
        }

        public async Task<Category?> UpdateAsync(Category category)
        {
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            
            // Reload with navigation properties
            return await GetByIdAsync(category.Id);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return false;

            // Check if category has products
            var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
            if (hasProducts)
                throw new InvalidOperationException("Can't delete a category that has asociated products.");
            if (category.State == ApprovalState.Deleted)
                throw new InvalidOperationException("Category is already deleted.");

            category.State = ApprovalState.Deleted;
            category.UpdatedAt = DateTime.UtcNow;
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Category>> SearchAsync(string searchTerm)
        {
            return await _context.Categories
                .Include(c => c.Creator)
                .Include(c => c.Approver)
                .Include(c => c.Products)
                .Where(c => c.Name.Contains(searchTerm) || c.Slug.Contains(searchTerm))
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetPendingApprovalAsync()
        {
            return await _context.Categories
                .Include(c => c.Creator)
                .Include(c => c.Approver)
                .Include(c => c.Products)
                .Where(c => c.State == ApprovalState.PendingApproval)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> ApproveAsync(Guid id, Guid approvedBy)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return false;

            // Check if category is already approved
            if (category.State == ApprovalState.Approved)
                throw new InvalidOperationException("Category is already approved.");

            // Only allow approval from PendingApproval state
            if (category.State != ApprovalState.PendingApproval)
                return false;

            category.State = ApprovalState.Approved;
            category.ApprovedBy = approvedBy;
            category.ApprovedAt = DateTime.UtcNow;
            category.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectAsync(Guid id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null || category.State != ApprovalState.PendingApproval)
                return false;

            category.State = ApprovalState.Declined;
            category.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.Categories.AnyAsync(c => c.Name.ToLower() == name.ToLower());
        }

        public async Task<bool> ExistsBySlugAsync(string slug)
        {
            return await _context.Categories.AnyAsync(c => c.Slug.ToLower() == slug.ToLower());
        }

        public async Task<bool> ExistsByNameAsync(string name, Guid excludeId)
        {
            return await _context.Categories.AnyAsync(c => c.Name.ToLower() == name.ToLower() && c.Id != excludeId);
        }

        public async Task<bool> ExistsBySlugAsync(string slug, Guid excludeId)
        {
            return await _context.Categories.AnyAsync(c => c.Slug.ToLower() == slug.ToLower() && c.Id != excludeId);
        }

        public async Task<bool> DeactivateAsync(Guid id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return false;

            // Check if category has products
            var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id && p.State == ApprovalState.Approved);
            if (hasProducts)
                throw new InvalidOperationException("Cannot deactivate a category that has approved products.");
            
            if (category.State == ApprovalState.Declined)
                throw new InvalidOperationException("Category is already deactivated.");

            category.State = ApprovalState.Declined;
            category.UpdatedAt = DateTime.UtcNow;
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}