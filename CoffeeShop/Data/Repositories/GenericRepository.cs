// Data/Repositories/IGenericRepository.cs
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace CoffeeShop.Data.Repositories
{
    public interface IGenericRepository<T> where T : class
    {
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate,
                               Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null);
        Task<T> GetByIdAsync(int id, Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null);
        // Base interface - có thể để trống hoặc thêm common methods
    }
}