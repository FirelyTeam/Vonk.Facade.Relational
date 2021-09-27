using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Linq.Expressions;
using Vonk.Core.Common;
using Vonk.Core.Repository;
using Vonk.Core.Repository.ResultShaping;
using Vonk.Core.Support;

namespace Vonk.Facade.Relational
{
    public abstract class RelationalQueryFactory<E, Q> : IRepoQueryFactory<Q> where E : class where Q : RelationalQuery<E>, new()
    {
        protected string ForResourceType { get; }
        protected DbContext OnContext { get; }

        protected RelationalQueryFactory(string forResourceType, DbContext onContext)
        {
            Check.NotNull(forResourceType, nameof(forResourceType));
            Check.NotNull(onContext, nameof(onContext));
            ForResourceType = forResourceType;
            OnContext = onContext;
        }

        /// <summary>
        /// Default implementation of And, can be used on any subquery.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public virtual Q And(Q left, Q right)
        {
            if (left == null)
                return right;
            if (right == null)
                return left;

            return new Q()
            {
                InternalShapes = left.Shapes.SafeUnion(right.Shapes)?.ToArray(),
                Predicate = LinqKitExtensions.And(left.Predicate, right.Predicate)
            };
        }

        /// <summary>
        /// Default implementation of Or, only valid for combining values from a choice.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public virtual Q Or(Q left, Q right)
        {
            if (left == null)
                return right;
            if (right == null)
                return left;
            if (left.Shapes.HasAny() || right.Shapes.HasAny())
                throw new NotSupportedException("Or is only allowed for choice values");
            return new Q() { Predicate = LinqKitExtensions.Or(left?.Predicate, right?.Predicate) };
        }

        public Q Not(Q query)
        {
            return new Q() { Predicate = Expression.Lambda<Func<E, bool>>(Expression.Not(query.Predicate.Body), query.Predicate.Parameters) };
        }

        /// <summary>
        /// Default implementation for filtering on <seealso cref="ResourceContained"/>.
        /// The assumption is that most Facades will not support Contained resources. 
        /// Therefore it throws a <seealso cref="NotSupportedException"/> if only <seealso cref="ResourceContained.Contained"/> is specified.
        /// Does not apply a filter otherwise.
        /// </summary>
        /// <param name="currency"></param>
        /// <returns></returns>
        public virtual Q EntryContained(ResourceContained contained)
        {
            if (contained == ResourceContained.Container || contained == ResourceContained.All)
            {
                return default(Q);
            }
            {
                throw new NotSupportedException("System does not support contained resources.");
            }
        }

        /// <summary>
        /// Default implementation for filtering on <seealso cref="ResourceCurrency"/>.
        /// The assumption is that most Facades will not support old versions of resources.
        /// Therefore it throws a <seealso cref="NotSupportedException"/> if only <seealso cref="ResourceCurrency.NotCurrent"/> is specified.
        /// Does not apply a filter otherwise.
        /// </summary>
        /// <param name="currency"></param>
        /// <returns></returns>
        public virtual Q EntryCurrency(ResourceCurrency currency)
        {
            if (currency == ResourceCurrency.Current || currency == ResourceCurrency.All)
            {
                return default(Q);
            }
            else
            {
                throw new NotSupportedException("System does not support selection of only history items");
            }
        }

        /// <summary>
        /// Default implementation for filtering on <seealso cref="ResourceChange"/>.
        /// The assumption is that most Facades will not support deleted resources, and cannot distinguish created from updated resources.
        /// Therefore it throws a <seealso cref="NotSupportedException"/> if only <seealso cref="ResourceChange.Deleted"/> is specified.
        /// Does not apply a filter otherwise.
        /// </summary>
        /// <param name="changes"></param>
        /// <returns></returns>
        public virtual Q EntryChange(ResourceChange[] changes)
        {
            if (changes.Contains(ResourceChange.Created) || changes.Contains(ResourceChange.Updated))
            {
                return default(Q);
            }
            else
            {
                throw new NotSupportedException("System does not support selection of deleted items");
            }
        }

        /// <summary>
        /// Repositories can in theory support >1 versions of FHIR. (Firely Server itself does that.)
        /// This method should return a <typeparamref name="Q"/> that filters on the FHIR version.
        /// If your facade only supports 1 FHIR version, you only need to check whether 
        /// the provided value of <paramref name="informationModel"/> represents that version.
        /// You can check it against the values in <seealso cref="VonkConstants.Model"/>. Example:
        /// <code>
        /// if (VonkConstants.Model.FhirR3.Equals(informationModel))
        /// {
        ///     return default;
        /// }
        /// </code>
        /// </summary>
        /// <example>
        /// <code>
        /// if (VonkConstants.Model.FhirR3.Equals(informationModel))
        /// {
        ///     return default;
        /// }
        /// </code>
        /// </example>
        /// <param name="informationModel">FHIR version that the resource must have.</param>
        /// <returns></returns>
        public virtual Q EntryInformationModel(string informationModel)
        {
            throw new NotImplementedException($"Implement this method in your QueryFactory. If your implementation only supports 1 FHIR version, check whether the parameter matches that (check against VonkConstants.Model.*) and return 'default'. Otherwise use the informationModel parameter in your WHERE clause.");
        }

        public virtual Q Filter(string parameterName, IFilterValue value)
        {
            try
            {
                return AddValueFilter(parameterName, (dynamic)value);
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                throw new NotSupportedException($"{this.GetType().Name} cannot add a filter of type {value.GetType().Name} for parameter {parameterName}");
            }
        }

        /// <summary>
        /// Default implementation of Shape handling. Handles count and skip.
        /// Override this method for handling other shapes, call base() at the end to let this handle count and skip.
        /// </summary>
        /// <param name="shape"></param>
        /// <returns></returns>
        public virtual Q ResultShape(IShapeValue shape)
        {
            try
            {
                return AddResultShape((dynamic)shape);
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                throw new NotSupportedException($"{this.GetType().Name} cannot add a shape of type {shape.GetType().Name}");
            }
        }

        protected virtual Q AddResultShape(CountShape count)
        {
            return ShapeQuery(count);
        }

        protected virtual Q AddResultShape(SkipShape skip)
        {
            return ShapeQuery(skip);
        }

        protected virtual Q AddResultShape(SortShape sort)
        {
            return ShapeQuery(sort);
        }

        /// <summary>
        /// Override this method to create <see cref="Q"/> queries for all the parameters of type String that you want to support.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual Q AddValueFilter(string parameterName, ResourceTypesValue value)
        {
            if (value.ResourceTypes?.Count() != 1 || value.ResourceTypes.First() != ForResourceType)
                throw new NotSupportedException($"Query of type {this.GetType().Name} can only be used for {ForResourceType}, not for {value.ResourceTypes?.CommaSeparated(true)}.");
            return new Q();
        }

        /// <summary>
        /// Override this method to create <see cref="Q"/> queries for all the parameters of type String that you want to support.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual Q AddValueFilter(string parameterName, StringValue value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Override this method to create <see cref="Q"/> queries for all the parameters of type Date, DateTime and Instant that you want to support.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual Q AddValueFilter(string parameterName, DateTimeValue value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Override this method to create <see cref="Q"/> queries for all the parameters of type Token that you want to support.
        /// This is the method to implement for supporting search on _id.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>

        public virtual Q AddValueFilter(string parameterName, TokenValue value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Override this method to create <see cref="Q"/> queries for all the parameters of type Number that you want to support.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual Q AddValueFilter(string parameterName, NumberValue value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Override this method to create <see cref="Q"/> queries for all the parameters of type Quantity that you want to support.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual Q AddValueFilter(string parameterName, QuantityValue value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Override this method to create <see cref="Q"/> queries for all the parameters of type Uri that you want to support.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual Q AddValueFilter(string parameterName, UriValue value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Override this method to create <see cref="Q"/> queries for all the parameters of type Reference that you want to support.
        /// This method is only for direct references in the form of parameter=Resourcetype/id.
        /// Use <see cref="AddValueFilter(string, ReferenceToValue)"/> for chaining,
        /// and <see cref="AddValueFilter(string, ReferenceFromValue)"/> for reverse chaining.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual Q AddValueFilter(string parameterName, ReferenceValue value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Override this method to create <see cref="Q"/> queries for all the parameters that link to another Resourcetype through a reference, called chaining (a.b.c=xyz).
        /// You will want to store the neccessary data in <see cref="Q"/> to perform a join later on.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual Q AddValueFilter(string parameterName, ReferenceToValue value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Override this method to create <see cref="Q"/> queries for all the parameters that are linked to this through a reference, called reverse chaining (_has:Resourcetype.a=xyz).
        /// You will want to store the neccessary data in <see cref="Q"/> to perform a join later on.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual Q AddValueFilter(string parameterName, ReferenceFromValue value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Override this method to create <see cref="Q"/> queries for parameters for which no SearchParameter is known to Vonk, and hence no SearchParameterType.
        /// This allows you to support custom parameters without explicitly defining them. Remember to register such parameters in an IConformanceContributor though, otherwise they don't get mentioned in the CapabilityStatement.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual Q AddValueFilter(string parameterName, RawValue value)
        {
            throw new NotSupportedException();
        }

        protected virtual Q PredicateQuery(Expression<Func<E, bool>> predicate)
        {
            return new Q() { Predicate = predicate };
        }

        protected virtual Q ShapeQuery(IShapeValue shape)
        {
            return new Q() { InternalShapes = new[] { shape } };
        }

        protected virtual Q SortQuery<P>(SortShape sort, Expression<Func<E, P>> fieldSelector)
        {
            return ShapeQuery(new RelationalSortShape<E>(sort,
            (IQueryable<E> source) =>
                sort.Direction == SortDirection.ascending
                    ? source.OrderBy(fieldSelector)
                    : source.OrderByDescending(fieldSelector)
            ));
        }
        protected virtual Q SortQuery<P>(SortShape sort, Func<IQueryable<E>, IQueryable<E>> sortFunction)
        {
            return ShapeQuery(new RelationalSortShape<E>(sort, sortFunction));
        }
    }



}
