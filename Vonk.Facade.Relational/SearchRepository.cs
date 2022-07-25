using System;
using System.Linq;
using System.Threading.Tasks;
using Vonk.Core.Context;
using Vonk.Core.Repository;
using Vonk.Core.Repository.ResultShaping;
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

            // Argument "_total" is meant to be handled in the implementation of ISearchRepository of a facade,
            // however since "_total=accurate" was added as a default shape argument and it is always the case when we perform a search,
            // so we simply handle it here for a facade implementation.
            // For arguments "_total=none" and "_total=estimate", they still need to be handled in a facade.
            var totalArgument = arguments.GetArgument(ArgumentNames.total);
            if (totalArgument != null && TotalOptions.accurate.ToString().Equals(totalArgument.ArgumentValue))
            {
                totalArgument.Handled();
            }

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
