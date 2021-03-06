﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Reinforced.Tecture.Cloning;
using Reinforced.Tecture.Features.SqlStroke.Reveal.LanguageInterpolate;

namespace Reinforced.Tecture.Features.SqlStroke.Reveal.SchemaInterpolate
{
    class SchemaInterpolatedQuery : InterpolatedQuery
    {
        /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
        public SchemaInterpolatedQuery(string query, object[] parameters, Type[] usedTypes) : base(query, parameters, usedTypes)
        {
        }

        internal override InterpolatedQuery Clone()
        {
            return new SchemaInterpolatedQuery(Query, Parameters.Select(x => x.DeepClone()).ToArray(), UsedTypes);
        }
    }
}
