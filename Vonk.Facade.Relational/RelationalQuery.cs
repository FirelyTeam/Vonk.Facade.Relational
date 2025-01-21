using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using Vonk.Core.Repository;
using Vonk.Core.Repository.ResultShaping;
using Vonk.Core.Support;

namespace Vonk.Facade.Relational;

public class RelationalSortShape<E> : SortShape
{
    public RelationalSortShape(SortShape originalSort, Func<IQueryable<E>, IQueryable<E>> sortFunction) : base(originalSort.ParameterCode, originalSort.ParameterType, originalSort.Direction, originalSort.Priority)
    {
        Sort = sortFunction;
    }

    public Func<IQueryable<E>, IQueryable<E>> Sort { get; }
}

public class RelationalQuery<E> : BaseQuery where E : class
{
    public Expression<Func<E, bool>> Predicate { get; internal set; }
    internal IShapeValue[] InternalShapes { get; set; }
    public override IShapeValue[] Shapes { get => InternalShapes; }

    /// <summary>
    /// Get the entities of type <see cref="E"/> that adhere to the <see cref="Predicate"/>, with any provided Skip and Count applied.
    /// </summary>
    /// <param name="dbContext">The <seealso cref="DbContext"/> containing a <seealso cref="DbSet"/> of <see cref="E"/>. We use <seealso cref="DbContext"/> so it can also contain <seealso cref="DbSet"/>s for entities that are referenced to or from.</param>.
    /// <returns></returns>
    public virtual IQueryable<E> Execute(DbContext dbContext)
    {
        Check.NotNull(dbContext);
        return HandleShapes(Filter(dbContext));
    }

    /// <summary>
    /// Get the total number of entities that adhere to the <see cref="Predicate"/>, irrespective of the Skip and Count.
    /// </summary>
    /// <param name="dbContext">The <seealso cref="DbContext"/> containing a <seealso cref="DbSet"/> of <see cref="E"/>. We use <seealso cref="DbContext"/> so it can also contain <seealso cref="DbSet"/>s for entities that are referenced to or from.</param>.
    /// <returns></returns>
    public virtual async Task<long> ExecuteCount(DbContext dbContext)
    {
        var entries = Filter(dbContext);
        if (entries == null)
            return 0;
        else
            return await entries.CountAsync();
    }

    /// <summary>
    /// Apply the <see cref="Predicate"/> on entities of type <see cref="E"/> in <paramref name="dbContext"/>.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <returns></returns>
    protected virtual IQueryable<E> Filter(DbContext dbContext)
    {
        var entities = GetEntitySet(dbContext).AsExpandable();
        if (Predicate != null)
            return entities.Where(LinqKitExtensions.And(Predicate));
        return entities;
    }

    /// <summary>
    /// By default this will return dbContext.Set<typeparamref name="E"/>, without change tracking.
    /// Override it to add custom .Include() statements or other adjustments.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <returns>A set of entities that will be used for filtering and counting</returns>
    protected virtual IQueryable<E> GetEntitySet(DbContext dbContext)
    {
        return dbContext.Set<E>().AsNoTracking();
    }

    /// <summary>
    /// Apply the <see cref="Shapes"/> on <paramref name="source"/>.
    /// By default only Skip, Count and Sort are applied.
    /// <see cref="SortByDefault(IQueryable{E})"/> is always applied.
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual IQueryable<E> HandleShapes(IQueryable<E> source)
    {
        if (Shapes.HasAny())
            return HandleCount(
                HandleSkip(
                    HandleSort(
                        SortByDefault(source)
                )));
        return SortByDefault(source);
    }

    /// <summary>
    /// Apply the <seealso cref="SkipShape"/> on <paramref name="source"/>, if provided in <see cref="Shapes"/>.
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual IQueryable<E> HandleSkip(IQueryable<E> source)
    {
        var skip = Shapes.OfType<SkipShape>().FirstOrDefault();
        if (skip != null)
            return source.Skip(skip.Skip);
        return source;
    }

    /// <summary>
    /// Apply the <seealso cref="CountShape"/> on <paramref name="source"/>, if provided in <see cref="Shapes"/>.
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual IQueryable<E> HandleCount(IQueryable<E> source)
    {
        var count = Shapes.OfType<CountShape>().FirstOrDefault();
        if (count != null)
            return source.Take(count.Count);
        return source;
    }

    /// <summary>
    /// Apply al the <seealso cref="SortShape"/>s on <paramref name="source"/>.
    /// Take into account the <seealso cref="SortShape.Priority"/> and apply them in the right order.
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual IQueryable<E> HandleSort(IQueryable<E> source)
    {
        if (Shapes.HasAny())
        {
            foreach (var relationalSort in Shapes.OfType<RelationalSortShape<E>>().OrderByDescending(rss => rss.Priority))
            {
                source = relationalSort.Sort(source);
            }
        }
        return source;
    }

    /// <summary>
    /// Lets you supply an OrderBy on e.g. a technical id so at least the order of the results is predictable.
    /// Also, EFCore complains if there is no sorting at all.
    /// Example: return source.OrderBy(e => e.SomeId);
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual IQueryable<E> SortByDefault(IQueryable<E> source)
    {
        return source;
    }
}
