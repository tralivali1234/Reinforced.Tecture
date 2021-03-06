﻿namespace Reinforced.Tecture.Channels
{
    interface IQueryMultiplexer
    {
        TFeature GetFeature<TFeature>() where TFeature : QueryFeature;
    }

    interface ICommandMultiplexer
    {
        TFeature GetFeature<TFeature>() where TFeature : CommandFeature;
    }

}
