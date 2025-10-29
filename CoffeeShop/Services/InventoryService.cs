using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CoffeeShop.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IUnitOfWork _unitOfWork;

        public async Task RestockFromOrderAsync(List<OrderDetail> orderDetails)
        {
            if (orderDetails == null) return;

            foreach (var detail in orderDetails)
            {
                if (detail == null || detail.MenuItemId <= 0) continue;

                var recipes = await _unitOfWork.MenuItemRecipes.FindAsync(r => r.MenuItemId == detail.MenuItemId);
                foreach (var recipe in recipes)
                {
                    var inventoryItem = await _unitOfWork.InventoryItems.GetByIdAsync(recipe.InventoryItemId);
                    if (inventoryItem != null)
                    {
                        inventoryItem.Quantity += recipe.QuantityRequired * detail.Quantity;
                        _unitOfWork.InventoryItems.Update(inventoryItem);
                    }
                    // Có thể thêm log nếu inventoryItem không tìm thấy (dữ liệu không nhất quán)
                }
            }
            await _unitOfWork.SaveChangesAsync();
        }
        public InventoryService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> CheckAvailabilityAsync(List<OrderDetail> orderDetails)
        {
            if (orderDetails == null || !orderDetails.Any()) return false;

            foreach (var detail in orderDetails)
            {
                if (detail == null || detail.MenuItemId <= 0) continue;

                var recipes = await _unitOfWork.MenuItemRecipes.FindAsync(r => r.MenuItemId == detail.MenuItemId);

                foreach (var recipe in recipes)
                {
                    var inventoryItem = await _unitOfWork.InventoryItems.GetByIdAsync(recipe.InventoryItemId);
                    if (inventoryItem == null || inventoryItem.Quantity < recipe.QuantityRequired * detail.Quantity)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public async Task<(bool isAvailable, List<string> missingItems)> CheckAvailabilityWithDetailsAsync(List<OrderDetail> orderDetails)
        {
            var missingItems = new List<string>();

            if (orderDetails == null || !orderDetails.Any())
                return (false, new List<string> { "Không có sản phẩm trong đơn hàng" });

            foreach (var detail in orderDetails)
            {
                if (detail == null || detail.MenuItemId <= 0) continue;

                var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(detail.MenuItemId);
                var recipes = await _unitOfWork.MenuItemRecipes.FindAsync(r => r.MenuItemId == detail.MenuItemId);

                foreach (var recipe in recipes)
                {
                    var inventoryItem = await _unitOfWork.InventoryItems.GetByIdAsync(recipe.InventoryItemId);
                    var requiredQuantity = recipe.QuantityRequired * detail.Quantity;

                    if (inventoryItem == null)
                    {
                        missingItems.Add($"Món {menuItem?.Name}: Thiếu nguyên liệu (ID: {recipe.InventoryItemId})");
                    }
                    else if (inventoryItem.Quantity < requiredQuantity)
                    {
                        missingItems.Add($"Món {menuItem?.Name}: Thiếu {inventoryItem.Name} " +
                                       $"(Cần: {requiredQuantity} {inventoryItem.Unit}, Có: {inventoryItem.Quantity} {inventoryItem.Unit})");
                    }
                }
            }

            return (missingItems.Count == 0, missingItems);
        }

        public async Task DeductStockAsync(List<OrderDetail> orderDetails)
        {
            if (orderDetails == null) return;

            foreach (var detail in orderDetails)
            {
                if (detail == null || detail.MenuItemId <= 0) continue;

                var recipes = await _unitOfWork.MenuItemRecipes.FindAsync(r => r.MenuItemId == detail.MenuItemId);

                foreach (var recipe in recipes)
                {
                    var inventoryItem = await _unitOfWork.InventoryItems.GetByIdAsync(recipe.InventoryItemId);
                    var requiredQuantity = recipe.QuantityRequired * detail.Quantity;

                    if (inventoryItem != null && inventoryItem.Quantity >= requiredQuantity)
                    {
                        inventoryItem.Quantity -= requiredQuantity;
                        _unitOfWork.InventoryItems.Update(inventoryItem);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Không đủ {inventoryItem?.Name ?? "nguyên liệu"} trong kho");
                    }
                }
            }
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync(decimal threshold)
        {
            var allItems = await _unitOfWork.InventoryItems.GetAllAsync();
            return allItems.Where(i => i.Quantity <= i.MinimumThreshold && i.Quantity <= threshold);
        }

        public async Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync()
        {
            var allItems = await _unitOfWork.InventoryItems.GetAllAsync();
            return allItems.Where(i => i.Quantity <= i.MinimumThreshold);
        }

        public async Task<IEnumerable<InventoryItem>> GetExpiringItemsAsync(int daysFromNow)
        {
            var targetDate = DateTime.Today.AddDays(daysFromNow);
            var allItems = await _unitOfWork.InventoryItems.GetAllAsync();
            return allItems.Where(i => i.ExpirationDate <= targetDate && i.ExpirationDate > DateTime.Today);
        }

        public async Task AddStockAsync(int inventoryItemId, decimal quantity, DateTime expirationDate)
        {
            var inventoryItem = await _unitOfWork.InventoryItems.GetByIdAsync(inventoryItemId);
            if (inventoryItem != null)
            {
                decimal newQuantity = inventoryItem.Quantity + quantity;
                if (newQuantity >= 0)
                {
                    inventoryItem.Quantity = newQuantity;
                    inventoryItem.ExpirationDate = expirationDate;
                    _unitOfWork.InventoryItems.Update(inventoryItem);
                }
                else
                {
                    throw new InvalidOperationException("Số lượng không thể âm.");
                }
            }
            else
            {
                throw new InvalidOperationException("Không tìm thấy nguyên liệu.");
            }
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<IEnumerable<InventoryItem>> GetItemsByExpirationAsync(DateTime? beforeDate)
        {
            var items = await _unitOfWork.InventoryItems.GetAllAsync();
            if (beforeDate.HasValue)
            {
                return items.Where(i => i.ExpirationDate <= beforeDate.Value);
            }
            return items;
        }

        public async Task<InventoryItem> GetByIdAsync(int id)
        {
            return await _unitOfWork.InventoryItems.GetByIdAsync(id);
        }

        public async Task DeleteAsync(int id)
        {
            var item = await _unitOfWork.InventoryItems.GetByIdAsync(id);
            if (item != null)
            {
                _unitOfWork.InventoryItems.Remove(item);
                await _unitOfWork.SaveChangesAsync();
            }
        }
    }
}