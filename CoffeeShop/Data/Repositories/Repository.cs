// Data/Repositories/Repository.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CoffeeShop.Data.Repositories
{
    // Assuming GenericRepository provides the base _context and _dbSet,
    // or basic implementations for methods not overridden here.
    public class Repository<T> /*: GenericRepository<T>*/ : IRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context; // Make sure this is protected if GenericRepository accesses it
        protected readonly DbSet<T> _dbSet;

        // If GenericRepository already initializes _context and _dbSet, this constructor might be simpler:
        // public Repository(ApplicationDbContext context) : base(context)
        // {
        //     // _context and _dbSet are initialized by the base class
        // }
        // Otherwise, your current constructor is fine:
        public Repository(ApplicationDbContext context) //: base(context) // If GenericRepository needs context
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task<T> GetByIdAsync(int id) => await _dbSet.FindAsync(id);

        // Implement the new method
        public async Task<T> GetByIdWithIncludesAsync(int id, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet;

            // Dynamically create the predicate for filtering by primary key
            var primaryKeyProperty = _context.Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (primaryKeyProperty == null)
            {
                throw new InvalidOperationException($"Entity type {typeof(T).Name} does not have a primary key defined.");
            }

            var parameter = Expression.Parameter(typeof(T), "x");
            var member = Expression.Property(parameter, primaryKeyProperty.Name);
            var constant = Expression.Constant(id);
            var body = Expression.Equal(member, constant);
            var lambda = Expression.Lambda<Func<T, bool>>(body, parameter);

            query = query.Where(lambda);

            if (includes != null)
            {
                foreach (var includeExpression in includes)
                {
                    query = query.Include(includeExpression);
                }
            }
            return await query.FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();

        // Optional: Implement GetAllWithIncludesAsync if you added it to the interface
        // public async Task<IEnumerable<T>> GetAllWithIncludesAsync(params Expression<Func<T, object>>[] includes)
        // {
        //     IQueryable<T> query = _dbSet;
        //     if (includes != null)
        //     {
        //         foreach (var includeExpression in includes)
        //         {
        //             query = query.Include(includeExpression);
        //         }
        //     }
        //     return await query.ToListAsync();
        // }


        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) => await _dbSet.Where(predicate).ToListAsync();

        public async Task<IEnumerable<T>> FindWithIncludeAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet.Where(predicate);
            if (includes != null)
            {
                // Sửa ở đây: tên biến "include" trùng với tham số "includes"
                // foreach (var include in includes) // Lỗi
                foreach (var includeExpression in includes) // Sửa thành tên khác
                {
                    query = query.Include(includeExpression);
                }
            }
            return await query.ToListAsync();
        }

        public async Task AddAsync(T entity) => await _dbSet.AddAsync(entity);

        // public async Task AddRangeAsync(IEnumerable<T> entities) => await _dbSet.AddRangeAsync(entities); // If added

        public void Update(T entity) => _dbSet.Update(entity);

        public void Remove(T entity) => _dbSet.Remove(entity);

        // public void RemoveRange(IEnumerable<T> entities) => _dbSet.RemoveRange(entities); // If added


        // --- If you are NOT using a base GenericRepository<T> that defines these ---
        // --- and IRepository<T> inherits from IGenericRepository<T> which you haven't shown,
        // --- you might need to implement those methods here if they are not in a base class.
        // --- For example, if IGenericRepository had SaveChangesAsync:
        // public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
    }
}