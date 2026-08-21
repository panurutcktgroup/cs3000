using CompanyCLI.Configuration;
using CompanyCLI.Models;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;

namespace CompanyCLI.Services;

public class DatabaseProductService : IProductService
{
    private readonly ServerConnectionSettings _settings;

    public DatabaseProductService()
    {
        var store = new ServerConnectionSettingsStore();
        _settings = store.Load(out _);
        if (!_settings.IsConfigured)
        {
            throw new InvalidOperationException("Database connection is not configured.");
        }
    }

    private IDbConnection CreateConnection()
    {
        // สร้าง service ผ่าน factory แล้วเรียก BuildConnectionString จาก concrete type
        var connectionService = DatabaseConnectionServiceFactory.Create(_settings.Provider);

        string connectionString;
        if (connectionService is SqlServerConnectionService sqlSvc)
        {
            connectionString = sqlSvc.BuildConnectionString(_settings);
            return new SqlConnection(connectionString);
        }
        else if (connectionService is MySqlConnectionService mySqlSvc)
        {
            connectionString = mySqlSvc.BuildConnectionString(_settings);
            return new MySqlConnection(connectionString);
        }
        else
        {
            // กรณีเพิ่ม provider ใหม่ในอนาคต
            throw new NotSupportedException($"Unsupported connection service type: {connectionService.GetType().FullName}");
        }
    }

    public List<Product> GetAll()
    {
        var list = new List<Product>();
        using var conn = CreateConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Price FROM Products ORDER BY Id";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Product
            {
                Id = Convert.ToInt32(reader[0]),
                Name = reader[1]?.ToString() ?? string.Empty,
                Price = Convert.ToDecimal(reader[2])
            });
        }

        return list;
    }

    public Product? GetById(int id)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Price FROM Products WHERE Id = @id";
        var p = cmd.CreateParameter();
        p.ParameterName = "@id";
        p.Value = id;
        cmd.Parameters.Add(p);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Product
            {
                Id = Convert.ToInt32(reader[0]),
                Name = reader[1]?.ToString() ?? string.Empty,
                Price = Convert.ToDecimal(reader[2])
            };
        }
        return null;
    }

    public void Add(Product p)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Products (Name, Price) VALUES (@name, @price)";
        var pn = cmd.CreateParameter();
        pn.ParameterName = "@name";
        pn.Value = p.Name;
        cmd.Parameters.Add(pn);

        var pp = cmd.CreateParameter();
        pp.ParameterName = "@price";
        pp.Value = p.Price;
        cmd.Parameters.Add(pp);

        cmd.ExecuteNonQuery();
    }

    public bool Delete(int id)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Products WHERE Id = @id";
        var p = cmd.CreateParameter();
        p.ParameterName = "@id";
        p.Value = id;
        cmd.Parameters.Add(p);

        var rows = cmd.ExecuteNonQuery();
        return rows > 0;
    }

    public int NextId()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(Id), 0) + 1 FROM Products";
        var result = cmd.ExecuteScalar();
        return Convert.ToInt32(result);
    }
}