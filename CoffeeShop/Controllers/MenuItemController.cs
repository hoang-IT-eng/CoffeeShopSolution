// Controllers/MenuItemController.cs
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using CoffeeShop.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore; // Required for Include/ThenInclude
using System.Linq;
using System.Threading.Tasks;

namespace CoffeeShop.Controllers
{
    [Authorize(Roles = "Admin")] // Only Admins can manage menu items
    public class MenuItemController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public MenuItemController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // GET: MenuItem
        public async Task<IActionResult> Index()
        {
            var menuItems = await _unitOfWork.MenuItems.GetAllAsync();
            return View(menuItems);
        }

        // GET: MenuItem/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menuItem = await _unitOfWork.MenuItems.GetByIdWithIncludesAsync(id.Value,
                                        m => m.Recipes); // Simple include

            // If you need ThenInclude, the params Expression<Func<T, object>>[] style is a bit trickier.
            // For complex ThenInclude with this style, you might need to adjust the repository
            // or do multiple includes if EF Core supports it like:
            // m => m.Recipes, m => m.SomeOtherCollection.Select(r => r.InventoryItem) // This is not standard for params Expression[]
            // A more common way for ThenInclude with the Func<IQueryable, IIncludableQueryable> style is better.
            // For now, let's assume you'll load InventoryItem separately if needed here or in the view,
            // OR adjust GetByIdWithIncludesAsync to take a string path for includes (less type-safe).

            // To make ThenInclude work with your params style, you'd have to fetch Recipes
            // and then manually populate InventoryItem or change GetByIdWithIncludesAsync significantly.
            // Let's do it in two steps for clarity with current repo:
            if (menuItem != null && menuItem.Recipes.Any())
            {
                // Manually load InventoryItems for the recipes if not handled by a single include chain
                // This is less ideal than a single fluent Include(x).ThenInclude(y) call.
                var recipeIds = menuItem.Recipes.Select(r => r.Id).ToList();
                var recipesWithItems = await _unitOfWork.MenuItemRecipes.FindWithIncludeAsync(
                                            r => recipeIds.Contains(r.Id), // Or r.MenuItemId == menuItem.Id
                                            r => r.InventoryItem);
                // Re-associate or use recipesWithItems directly in the view.
                // This part highlights a limitation of simple `params Expression<Func<T, object>>[]` for deep includes.

                // A better approach if GetByIdWithIncludesAsync was like the one I first proposed:
                // var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(id.Value,
                //     include: query => query.Include(m => m.Recipes)
                //                           .ThenInclude(r => r.InventoryItem));
                // Since your repository is different, let's adjust the controller slightly for Details:

                // Simpler for Details: Load MenuItem, then query recipes with inventory items separately
                var menuItemBasic = await _unitOfWork.MenuItems.GetByIdAsync(id.Value);
                if (menuItemBasic == null) return NotFound();

                menuItemBasic.Recipes = (await _unitOfWork.MenuItemRecipes.FindWithIncludeAsync(
                                            r => r.MenuItemId == menuItemBasic.Id,
                                            r => r.InventoryItem
                                        )).ToList();
                menuItem = menuItemBasic; // Assign to the variable the view expects
            }


            if (menuItem == null)
            {
                return NotFound();
            }

            return View(menuItem);
        }

        // GET: MenuItem/Create
        public async Task<IActionResult> Create()
        {
            // Provide a list of inventory items for the recipe dropdown
            ViewBag.InventoryItems = new SelectList(
                await _unitOfWork.InventoryItems.GetAllAsync(),
                nameof(InventoryItem.Id),
                nameof(InventoryItem.Name)
            );
            var viewModel = new MenuItemViewModel();
            // Initialize with one empty recipe row for convenience, or leave empty
            // viewModel.Recipes.Add(new RecipeViewModel()); 
            return View(viewModel);
        }

        // POST: MenuItem/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MenuItemViewModel viewModel)
        {
            // Remove ModelState errors for navigation properties or display-only properties
            // if they are not directly bound or are handled differently.
            // Example: If InventoryItemName in RecipeViewModel is only for display.
            // No need to remove for InventoryItemId or QuantityRequired as they are bound.

            if (ModelState.IsValid)
            {
                var menuItem = new MenuItem
                {
                    Name = viewModel.Name,
                    Price = viewModel.Price,
                    Description = viewModel.Description,
                    ImageUrl = viewModel.ImageUrl,
                    Category = viewModel.Category,
                    IsAvailable = viewModel.IsAvailable
                    // Recipes will be added after MenuItem is saved and has an ID
                };

                await _unitOfWork.MenuItems.AddAsync(menuItem);
                await _unitOfWork.SaveChangesAsync(); // Save MenuItem to generate its ID

                if (viewModel.Recipes != null)
                {
                    foreach (var recipeVm in viewModel.Recipes)
                    {
                        // Ensure that an inventory item is selected and quantity is positive
                        if (recipeVm.InventoryItemId > 0 && recipeVm.QuantityRequired > 0)
                        {
                            var menuItemRecipe = new MenuItemRecipe
                            {
                                MenuItemId = menuItem.Id, // Use the ID of the newly created MenuItem
                                InventoryItemId = recipeVm.InventoryItemId,
                                QuantityRequired = recipeVm.QuantityRequired
                            };
                            await _unitOfWork.MenuItemRecipes.AddAsync(menuItemRecipe);
                        }
                    }
                    await _unitOfWork.SaveChangesAsync(); // Save all the recipes
                }

                TempData["SuccessMessage"] = $"Menu item '{menuItem.Name}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            // If ModelState is invalid, re-populate ViewBag for the dropdown
            ViewBag.InventoryItems = new SelectList(
                await _unitOfWork.InventoryItems.GetAllAsync(),
                nameof(InventoryItem.Id),
                nameof(InventoryItem.Name)
            // Optionally, re-select previously selected items if needed, though more complex for a list
            );
            return View(viewModel);
        }

        // GET: MenuItem/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // For Edit, we primarily need the MenuItem and its directly associated Recipes' IDs and quantities.
            // The InventoryItem names are for the dropdown (ViewBag) and potentially display in the recipe list.
            var menuItem = await _unitOfWork.MenuItems.GetByIdWithIncludesAsync(id.Value,
                                        m => m.Recipes); // This gets MenuItem and its Recipes.

            if (menuItem == null)
            {
                return NotFound();
            }

            // Populate InventoryItemName for existing recipes if needed for display (ViewBag handles the full list for new items)
            var inventoryItems = await _unitOfWork.InventoryItems.GetAllAsync();
            var inventoryItemLookup = inventoryItems.ToDictionary(inv => inv.Id, inv => inv.Name);

            var viewModel = new MenuItemViewModel
            {
                Id = menuItem.Id,
                Name = menuItem.Name,
                Price = menuItem.Price,
                Description = menuItem.Description,
                ImageUrl = menuItem.ImageUrl,
                Category = menuItem.Category,
                IsAvailable = menuItem.IsAvailable,
                Recipes = menuItem.Recipes.Select(r => new RecipeViewModel
                {
                    InventoryItemId = r.InventoryItemId,
                    InventoryItemName = inventoryItemLookup.ContainsKey(r.InventoryItemId) ? inventoryItemLookup[r.InventoryItemId] : "Unknown",
                    QuantityRequired = r.QuantityRequired
                }).ToList()
            };

            ViewBag.InventoryItems = new SelectList(inventoryItems, "Id", "Name");
            return View(viewModel);
        }

        // POST: MenuItem/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MenuItemViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Important: Load the existing entity WITH its current recipes to manage them
                    var menuItemToUpdate = await _unitOfWork.MenuItems.GetByIdWithIncludesAsync(id, m => m.Recipes);

                    if (menuItemToUpdate == null)
                    {
                        TempData["ErrorMessage"] = "Menu item not found.";
                        return NotFound();
                    }

                    // Update scalar properties
                    menuItemToUpdate.Name = viewModel.Name;
                    menuItemToUpdate.Price = viewModel.Price;
                    // ... (rest of the properties)
                    menuItemToUpdate.Description = viewModel.Description;
                    menuItemToUpdate.ImageUrl = viewModel.ImageUrl;
                    menuItemToUpdate.Category = viewModel.Category;
                    menuItemToUpdate.IsAvailable = viewModel.IsAvailable;

                    _unitOfWork.MenuItems.Update(menuItemToUpdate); // Mark MenuItem as modified

                    // --- Manage Recipes (Remove old, Add new) ---
                    // 1. Remove existing recipes associated with this menu item
                    // ToList() is important to avoid issues when modifying a collection being iterated over.
                    var recipesToRemove = menuItemToUpdate.Recipes.ToList();
                    foreach (var existingRecipe in recipesToRemove)
                    {
                        _unitOfWork.MenuItemRecipes.Remove(existingRecipe);
                    }

                    // 2. Add new/updated recipes from the ViewModel
                    if (viewModel.Recipes != null)
                    {
                        foreach (var recipeVm in viewModel.Recipes)
                        {
                            if (recipeVm.InventoryItemId > 0 && recipeVm.QuantityRequired > 0)
                            {
                                var newMenuItemRecipe = new MenuItemRecipe
                                {
                                    MenuItemId = menuItemToUpdate.Id, // Link to the parent MenuItem
                                    InventoryItemId = recipeVm.InventoryItemId,
                                    QuantityRequired = recipeVm.QuantityRequired
                                };
                                await _unitOfWork.MenuItemRecipes.AddAsync(newMenuItemRecipe);
                            }
                        }
                    }

                    await _unitOfWork.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Menu item '{menuItemToUpdate.Name}' updated successfully.";
                }
                // ... (rest of catch blocks) ...
                catch (DbUpdateConcurrencyException)
                {
                    if (!await MenuItemExists(viewModel.Id)) // MenuItemExists would use GetByIdAsync
                    {
                        return NotFound();
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Concurrency error. The item may have been modified by another user.";
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                }
                return RedirectToAction(nameof(Index));
            }

            // If ModelState is invalid, re-populate ViewBag
            ViewBag.InventoryItems = new SelectList(
                await _unitOfWork.InventoryItems.GetAllAsync(),
                "Id", "Name"
            );
            return View(viewModel);
        }
        // GET: MenuItem/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Load MenuItem and related data if you want to display more info on delete confirmation
            var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(id.Value);

            if (menuItem == null)
            {
                return NotFound();
            }

            return View(menuItem);
        }

        // POST: MenuItem/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(id);
            // No need to explicitly load recipes if CascadeOnDelete is configured in DbContext.
            // EF Core will handle deleting related MenuItemRecipe entries.

            if (menuItem != null)
            {
                _unitOfWork.MenuItems.Remove(menuItem);
                await _unitOfWork.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Menu item '{menuItem.Name}' deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Menu item not found or already deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> MenuItemExists(int id)
        {
            return await _unitOfWork.MenuItems.GetByIdAsync(id) != null; // Uses the simple GetByIdAsync
        }
    }
}