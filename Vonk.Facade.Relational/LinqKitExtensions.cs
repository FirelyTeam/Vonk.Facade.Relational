using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using LinqKit;

namespace Vonk.Core.Support
{
	public static class LinqKitExtensions
	{
        public static Expression<Func<T, bool>> And<T>(params Expression<Func<T, bool>>[] expressions)
        {
            if (expressions == null)
                return null;
            var result = expressions[0];
            for (int i = 1; i < expressions.Length; i++)
            {
                if (result == null)
                    result = expressions[i];
                else if (expressions[i] != null)
                    result = result.And(expressions[i]);
            }
            return result;
        }

        public static Expression<Func<T, bool>> Or<T>(params Expression<Func<T, bool>>[] expressions)
        {
            if (expressions == null)
                return null;
            var result = expressions[0];
            for (int i = 1; i < expressions.Length; i++)
            {
                if (result == null)
                    result = expressions[i];
                else if (expressions[i] != null)
                    result = result.Or(expressions[i]);
            }
            return result;
        }
    }

}
