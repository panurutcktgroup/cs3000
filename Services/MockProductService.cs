using System.Collections.Generic;
using System.Linq;
using CompanyCLI.Models;

namespace CompanyCLI.Services;

public class MockProductService : IProductService
{
    private readonly List<Product> _items = new List<Product>
    {
        new Product { Id = 1, Name = "Sample A", Price = 9.99m },
        new Product { Id = 2, Name = "Sample B", Price = 19.50m },
        new Product { Id = 3, Name = "Sample C", Price = 5.25m }
    };

    public List<Product> GetAll() => _items.Select(p => new Product { Id = p.Id, Name = p.Name, Price = p.Price }).ToList();

    public Product? GetById(int id) => _items.FirstOrDefault(p => p.Id == id);

    public void Add(Product p) => _items.Add(p);

    public bool Delete(int id)
    {
        var p = _items.FirstOrDefault(x => x.Id == id);
        if (p == null) return false;
        _items.Remove(p);
        return true;
    }

    public int NextId() => _items.Count == 0 ? 1 : _items.Max(p => p.Id) + 1;
}