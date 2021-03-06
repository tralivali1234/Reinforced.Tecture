﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Reinforced.Tecture.Features.Orm.Commands.Add;

namespace Reinforced.Tecture.Runtimes.EFCore.Features.Orm.Query
{
    class EfCore_Orm_QueryFeature : Tecture.Features.Orm.Query
    {
        private readonly ILazyDisposable<DbContext> _context;

        public EfCore_Orm_QueryFeature(ILazyDisposable<DbContext> context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves queryable set
        /// </summary>
        /// <typeparam name="T">Entity</typeparam>
        /// <returns>Queryable set of entities</returns>
        protected override IQueryable<T> Set<T>()
        {
            return _context.Value.Set<T>();
        }

        /// <summary>
        /// Returns key of just added entity
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="addCommand">Addition command</param>
        /// <param name="keyProperties">Key property</param>
        /// <returns></returns>
        protected override IEnumerable<object> GetKey(Add addCommand, IEnumerable<PropertyInfo> keyProperties)
        {
            var e = addCommand.Entity;

            foreach (var propertyInfo in keyProperties)
            {
                yield return propertyInfo.GetValue(e);
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public override void Dispose()
        {
            _context.Dispose();
        }
    }
}
