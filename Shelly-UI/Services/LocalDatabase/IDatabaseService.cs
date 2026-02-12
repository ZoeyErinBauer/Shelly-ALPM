using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Shelly_UI.Services.LocalDatabase;

/// <summary>
/// Provides database operations for local data storage using LiteDB.
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Adds or updates a list of items to the specified database collection.
    /// </summary>
    /// <typeparam name="T">The type of items to add.</typeparam>
    /// <param name="packages">The list of items to add or update.</param>
    /// <param name="collection">The name of the collection to add items to.</param>
    /// <returns>A task that represents the asynchronous operation, returning true if successful.</returns>
    public Task<bool> AddToDatabase<T>(List<T> packages, string collection);

    /// <summary>
    /// Retrieves all items from the specified database collection.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The name of the collection to retrieve.</param>
    /// <returns>A list of all items in the collection.</returns>
    public List<T> GetCollection<T>(string collection);

    /// <summary>
    /// Retrieves a paginated subset of items from the specified collection with optional filtering and ordering.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <typeparam name="TKey">The type of the property used for ordering.</typeparam>
    /// <param name="collection">The name of the collection to query.</param>
    /// <param name="pageNumber">The zero-based page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="orderBySelector">An expression defining the property to order by.</param>
    /// <param name="predicate">An optional filter expression to apply to the query.</param>
    /// <returns>A list of items for the specified page.</returns>
    public List<T> GetNextPage<T, TKey>(
        string collection,
        int pageNumber,
        int pageSize,
        Expression<Func<T, TKey>> orderBySelector,
        Expression<Func<T, bool>>? predicate = null);
    
    /// <summary>
    /// Retrieves a paginated subset of items from the specified collection with optional filtering and ordering.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <typeparam name="TKey">The type of the property used for ordering.</typeparam>
    /// <param name="collection">The name of the collection to query.</param>
    /// <param name="predicate">An optional filter expression to apply to the query.</param>
    /// <returns>A list of items for the specified page.</returns>
    public List<T> SearchDatabase<T, TKey>(
        string collection,
        Expression<Func<T, bool>>? predicate = null);

    /// <summary>
    /// Ensures that the specified indexes exist on the collection for improved query performance.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The name of the collection to create indexes on.</param>
    /// <param name="indexes">One or more expressions defining the properties to index.</param>
    /// <returns>A completed task.</returns>
    public Task EnsureIndex<T>(string collection, params Expression<Func<T, object>>[] indexes) where T : class;

    /// <summary>
    /// Checks whether the specified collection exists and contains any items.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collectionName">The name of the collection to check.</param>
    /// <returns>True if the collection exists and has items; otherwise, false.</returns>
    public bool CollectionExists<T>(string collectionName);
}