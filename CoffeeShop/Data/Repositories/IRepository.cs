// Data/Repositories/IRepository.cs
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CoffeeShop.Data.Repositories
{
    public interface IRepository<T> where T : class
    {
        Task<T> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task AddAsync(T entity);
        void Update(T entity);
        void Remove(T entity); // Định nghĩa Remove với đối tượng T
    }
}