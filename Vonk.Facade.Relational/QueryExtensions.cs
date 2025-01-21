using Vonk.Core.Repository;

namespace Vonk.Facade.Relational;

public static class QueryExtensions
{
    public static Q CreateQuery<E, Q>(this ReferenceFromValue refFromValue, RelationalQueryFactory<E, Q> queryFactory) where E : class where Q : RelationalQuery<E>, new()
    {
        return refFromValue.Context.CreateQuery(queryFactory, refFromValue.Arguments, refFromValue.Options, refFromValue.Level);
    }

    public static Q CreateQuery<E, Q>(this ReferenceToValue refFromValue, RelationalQueryFactory<E, Q> queryFactory) where E : class where Q : RelationalQuery<E>, new()
    {
        return refFromValue.Context.CreateQuery(queryFactory, refFromValue.Arguments, refFromValue.Options, refFromValue.Level);
    }

}
