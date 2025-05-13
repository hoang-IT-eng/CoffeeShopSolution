// Services/InventoryService.cs
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoffeeShop.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IUnitOfWork _unitOfWork;

        public InventoryService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> CheckAvailabilityAsync(List<OrderDetail> orderDetails)
        {
            foreach (var detail in orderDetails)
            {
                var recipes = await _unitOfWork.MenuItemRecipes
                    .FindAsync(r => r.MenuItemId == detail.MenuItemId);

                foreach (var recipe in recipes)
                {
                    var inventoryItem = await _unitOfWork.InventoryItems.GetByIdAsync(recipe.InventoryItemId);
                    if (inventoryItem.Quantity < recipe.QuantityRequired * detail.Quantity)
                    {
                        return false; // Không đủ nguyên liệu
                    }
                }
            }
            return true;
        }

        public async Task DeductStockAsync(List<OrderDetail> orderDetails)
        {
            foreach (var detail in orderDetails)
            {
                var recipes = await _unitOfWork.MenuItemRecipes
                    .FindAsync(r => r.MenuItemId == detail.MenuItemId);

                foreach (var recipe in recipes)
                {
                    var inventoryItem = await _unitOfWork.InventoryItems.GetByIdAsync(recipe.InventoryItemId);
                    inventoryItem.Quantity -= recipe.QuantityRequired * detail.Quantity;
                    _unitOfWork.InventoryItems.Update(inventoryItem);
                }
            }
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync(decimal threshold)
        {
            return await _unitOfWork.InventoryItems
                .FindAsync(i => i.Quantity <= threshold);
        }
    }
}