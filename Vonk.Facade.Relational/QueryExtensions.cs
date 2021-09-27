using Vonk.Core.Repository;

namespace Vonk.Facade.Relational
{
    public static class QueryExtensions
    {
        //TODO: Move these to Vonk.Core.Repository, they are also useful for MemoryQueryRepo and probably also DocumentQueryRepo
        public static Q CreateQuery<E, Q>(this ReferenceFromValue refFromValue, RelationalQueryFactory<E, Q> queryFactory) where E : class where Q : RelationalQuery<E>, new()
        {
            return refFromValue.Context.CreateQuery(queryFactory, refFromValue.Arguments, refFromValue.Options, refFromValue.Level);
        }

        public static Q CreateQuery<E, Q>(this ReferenceToValue refFromValue, RelationalQueryFactory<E, Q> queryFactory) where E : class where Q : RelationalQuery<E>, new()
        {
            return refFromValue.Context.CreateQuery(queryFactory, refFromValue.Arguments, refFromValue.Options, refFromValue.Level);
        }

    }
}
