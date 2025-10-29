// Data/Repositories/IRepository.cs
using System;
using System.Collections.Generic;
using System.Linq; // Add this for IQueryable
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Query; // Add this for IIncludableQueryable

namespace CoffeeShop.Data.Repositories
{
    // Assuming IGenericRepository might have common methods like Add, Update, Remove
    // If not, you can remove the inheritance if these methods are fully defined here.
    public interface IRepository<T> /*: IGenericRepository<T>*/ where T : class
    {
        Task<T> GetByIdAsync(int id);

        // OPTION 1: Add a new method specifically for GetByIdAsync with includes
        Task<T> GetByIdWithIncludesAsync(int id, params Expression<Func<T, object>>[] includes);
        // OR OPTION 2: (Closer to my previous suggestion, more flexible for complex includes)
        // Task<T> GetByIdAsync(int id, Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null);


        Task<IEnumerable<T>> GetAllAsync();
        // Optional: Add GetAllWithIncludesAsync if needed elsewhere
        // Task<IEnumerable<T>> GetAllWithIncludesAsync(params Expression<Func<T, object>>[] includes);


        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        // Your existing FindWithIncludeAsync is good for finding multiple items
        Task<IEnumerable<T>> FindWithIncludeAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);

        Task AddAsync(T entity);
        // Task AddRangeAsync(IEnumerable<T> entities); // Consider adding if you use it
        void Update(T entity);
        void Remove(T entity);

        // void RemoveRange(IEnumerable<T> entities); // Consider adding
    }
}