﻿using System;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace NeinLinq
{
    /// <summary>
    /// Proxy for query provider.
    /// </summary>
    public class RewriteQueryProvider : IQueryProvider, IDbAsyncQueryProvider
    {
        private readonly IQueryProvider provider;
        private readonly ExpressionVisitor rewriter;

        /// <summary>
        /// Create a new rewrite query provider.
        /// </summary>
        /// <param name="provider">The actual query provider.</param>
        /// <param name="rewriter">The rewriter to rewrite the query.</param>
        public RewriteQueryProvider(IQueryProvider provider, ExpressionVisitor rewriter)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");
            if (rewriter == null)
                throw new ArgumentNullException("rewriter");

            this.provider = provider;
            this.rewriter = rewriter;
        }

        /// <inheritdoc />
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            // create query and make proxy again for rewritten query chaining
            return provider.CreateQuery<TElement>(expression).Rewrite(rewriter);
        }

        /// <inheritdoc />
        public IQueryable CreateQuery(Expression expression)
        {
            // create query and make proxy again for rewritten query chaining
            return provider.CreateQuery(expression).Rewrite(rewriter);
        }

        /// <inheritdoc />
        public TResult Execute<TResult>(Expression expression)
        {
            // execute query with rewritten expression
            return provider.Execute<TResult>(rewriter.Visit(expression));
        }

        /// <inheritdoc />
        public object Execute(Expression expression)
        {
            // execute query with rewritten expression
            return provider.Execute(rewriter.Visit(expression));
        }

        /// <inheritdoc />
        public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            // execute query with rewritten expression; async, if possible
            var asyncProvider = provider as IDbAsyncQueryProvider;
            if (asyncProvider != null)
                return asyncProvider.ExecuteAsync<TResult>(rewriter.Visit(expression), cancellationToken);
            return Task.FromResult(provider.Execute<TResult>(rewriter.Visit(expression)));
        }

        /// <inheritdoc />
        public Task<object> ExecuteAsync(Expression expression, CancellationToken cancellationToken)
        {
            // execute query with rewritten expression; async, if possible
            var asyncProvider = provider as IDbAsyncQueryProvider;
            if (asyncProvider != null)
                return asyncProvider.ExecuteAsync(rewriter.Visit(expression), cancellationToken);
            return Task.FromResult(provider.Execute(rewriter.Visit(expression)));
        }
    }
}