using CoffeeShop.Data.Repositories;
using CoffeeShop.ViewModels;

public interface IMenuItemRecipeRepository : IRepository<MenuItemRecipe>
{
    Task<IEnumerable<MenuItemRecipe>> GetRecipesWithInventoryAsync(int menuItemId);
}
