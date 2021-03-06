﻿using Microsoft.Data.Entity;
using NeinLinq.Tests.DbAsync;
using System;
using Xunit;

namespace NeinLinq.Tests
{
    public class DbAsyncTest : IDisposable
    {
        readonly Context db;

        public DbAsyncTest()
        {
            db = new Context();

            db.Dummies.RemoveRange(db.Dummies);
            db.SaveChanges();

            db.Dummies.AddRange(new[]
            {
                new Dummy
                {
                    Id = 1,
                    Name = "Asdf",
                    Number = 123.45m
                },
                new Dummy
                {
                    Id = 2,
                    Name = "Qwer",
                    Number = 67.89m
                },
                new Dummy
                {
                    Id = 3,
                    Name = "Narf",
                    Number = 3.14m
                }
            });
            db.SaveChanges();
        }

        [Fact]
        public async void ToListAsyncShouldWork()
        {
            await db.Dummies.ToListAsync();
        }

        [Fact]
        public async void ToListAsyncShouldSucceed()
        {
            var query = db.Dummies.Rewrite(new Rewriter());

            var result = await query.ToListAsync();

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async void SumAsyncShouldWork()
        {
            await db.Dummies.SumAsync(d => d.Number);
        }

        [Fact]
        public async void SumAsyncShouldSucceed()
        {
            var query = db.Dummies.Rewrite(new Rewriter());

            var result = await query.SumAsync(d => d.Number);

            Assert.Equal(194.48m, result, 2);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
