using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LiteDB;
using Shelly_UI.Models;

namespace Shelly_UI.Services.LocalDatabase;

public class DatabaseService : IDatabaseService
{
    private static readonly string DbFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Shelly/Shelly.db");

    private const int PageSize = 20;

    /// <inheritdoc/>
    public async Task<bool> AddToDatabase<T>(List<T> packages, string collection)
    {
        try
        {
            using var db = new LiteDatabase(DbFolder);
            var col = db.GetCollection<T>(collection);
            foreach (var model in packages)
            {
                col.Upsert(model);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("error" + e);
        }

        return true;
    }

    /// <inheritdoc/>
    public List<T> GetCollection<T>(string collection)
    {
        using var db = new LiteDatabase(DbFolder);
        var col = db.GetCollection<T>(collection);

        var packages = col.FindAll();
        return packages.ToList();
    }

    /// <inheritdoc/>
    public List<T> GetNextPage<T, TKey>(
        string collection,
        int pageNumber,
        int pageSize,
        Expression<Func<T, TKey>> orderBySelector,
        Expression<Func<T, bool>>? predicate = null)
    {
        using var db = new LiteDatabase(DbFolder);
        var col = db.GetCollection<T>(collection);

        var query = col.Query();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return query
            .OrderBy(orderBySelector)
            .Skip(pageNumber * pageSize)
            .Limit(pageSize)
            .ToList();
    }

    /// <inheritdoc/>
    public List<T> SearchDatabase<T, TKey>(
        string collection,
        Expression<Func<T, bool>>? predicate = null)
    {
        using var db = new LiteDatabase(DbFolder);
        var col = db.GetCollection<T>(collection);

        var query = col.Query();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return query
            .ToList();
    }

    /// <inheritdoc/>
    public Task EnsureIndex<T>(string collection, params Expression<Func<T, object>>[] indexes) where T : class
    {
        using (var db = new LiteDatabase(DbFolder))
        {
            var col = db.GetCollection<T>(collection);
            foreach (var index in indexes)
            {
                col.EnsureIndex(index);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public bool CollectionExists<T>(string collectionName)
    {
        using var db = new LiteDatabase(DbFolder);
        var col = db.GetCollection<T>(collectionName);
        return col.Count() > 0;
    }
}