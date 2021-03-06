﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace NeinLinq
{
    /// <summary>
    /// Expression visitor for making member access null-safe.
    /// </summary>
    /// <remarks>
    /// Use <see cref="NullsafeQueryBuilder" /> to make a query null-safe.
    /// </remarks>
    public class NullsafeQueryRewriter : ExpressionVisitor
    {
        /// <inheritdoc />
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node == null || node.Expression == null)
                return node;

            var fallback = Fallback(node.Type);

            // check both, expression's value and expression's member's value, if not default
            if (fallback.NodeType != ExpressionType.Default)
            {
                return Expression.Condition(
                    Expression.OrElse(
                        Expression.Equal(Visit(node.Expression), Expression.Default(node.Expression.Type)),
                        Expression.Equal(node, Expression.Default(node.Type))),
                    fallback,
                    node);
            }

            // just check expression's value...
            return Expression.Condition(
                Expression.Equal(Visit(node.Expression), Expression.Default(node.Expression.Type)),
                fallback,
                node);
        }

        static Expression Fallback(Type type)
        {
            // default values for generic collections
            if (type.IsConstructedGenericType())
            {
                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition == typeof(IEnumerable<>) ||
                    typeDefinition == typeof(ICollection<>))
                {
                    var typeArguments = type.GetGenericTypeArguments();
                    return Expression.Convert(
                        Expression.New(typeof(List<>).MakeGenericType(typeArguments)),
                        type);
                }
                if (typeDefinition == typeof(ISet<>))
                {
                    var typeArguments = type.GetGenericTypeArguments();
                    return Expression.Convert(
                        Expression.New(typeof(HashSet<>).MakeGenericType(typeArguments)),
                        type);
                }
            }

            // default value
            return Expression.Default(type);
        }
    }
}
