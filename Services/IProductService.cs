using System.Collections.Generic;
using CompanyCLI.Models;

namespace CompanyCLI.Services;

public interface IProductService
{
    List<Product> GetAll();
    Product? GetById(int id);
    void Add(Product p);
    bool Delete(int id);
    int NextId();
}