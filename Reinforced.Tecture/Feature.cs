﻿using System;
using Reinforced.Tecture.Query;

namespace Reinforced.Tecture
{
    /// <summary>
    /// Base class for all features
    /// </summary>
    public abstract class Feature : IDisposable
    {
        internal Auxilary _aux;

        /// <summary>
        /// Access to test data/query tooling
        /// </summary>
        protected Auxilary Aux
        {
            get { return _aux; }
        }

        internal void CallOnRegister()
        {
            OnRegister();
        }

        /// <summary>
        /// Being invoked right after feature registration in channel multiplexer
        /// </summary>
        protected virtual void OnRegister()
        {

        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public abstract void Dispose();
    }

    /// <summary>
    /// Query feature type
    /// </summary>
    public abstract class QueryFeature : Feature
    {
        
    }

    /// <summary>
    /// Command feature type
    /// </summary>
    public abstract class CommandFeature : Feature
    {
        
    }
}
