using System;
using System.Linq;
using System.Threading.Tasks;
using Vonk.Core.Context;
using Vonk.Core.Repository;
using Vonk.Core.Support;
using static Vonk.Core.Context.VonkOutcome;

namespace Vonk.Facade.Relational
{
    public abstract class SearchRepository : ISearchRepository
    {
        protected readonly QueryContext _queryContext;

        public SearchRepository(QueryContext queryContext)
        {
            Check.NotNull(queryContext, nameof(queryContext));

            _queryContext = queryContext;
        }

        public virtual Task<SearchResult> Search(IArgumentCollection arguments, SearchOptions options)
        {
            var types = arguments?.ResourceTypes(true);
            if (!types.HasAny() || types.Count() > 1)
                throw new NotSupportedException("Searching across multiple types is not supported.");

            var type = types.First();

            try
            {
                return Search(type, arguments, options);
            }
            catch (NotSupportedException nse)
            {
                foreach (var arg in arguments.ResourceTypeArguments())
                {
                    var issueComponent = new VonkIssue(IssueSeverity.Error, IssueType.NotSupported, null, nse.Message);
                    arg.Error(issueComponent);
                }
                throw;
            }
        }

        protected abstract Task<SearchResult> Search(string resourceType, IArgumentCollection arguments, SearchOptions options);
    }
}
