using System;
using System.Collections.Generic;
using Jcl.Draw.Model.Connectors;

namespace Jcl.Draw.Diagramming.Routing;

/// <summary>Dispatches routing to the strategy registered for the request's <see cref="RouteStyle"/>.</summary>
public sealed class ConnectorRouter : IConnectorRouter
{
    private readonly IReadOnlyDictionary<RouteStyle, IConnectorRouteStrategy> _strategies;

    public ConnectorRouter(IEnumerable<IConnectorRouteStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        Dictionary<RouteStyle, IConnectorRouteStrategy> map = new();
        foreach (IConnectorRouteStrategy strategy in strategies)
        {
            map[strategy.Style] = strategy;
        }

        if (!map.ContainsKey(RouteStyle.Straight))
        {
            map[RouteStyle.Straight] = new StraightRouter();
        }

        _strategies = map;
    }

    public ConnectorRoute Route(ConnectorRouteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        IConnectorRouteStrategy strategy = _strategies.TryGetValue(request.Style, out IConnectorRouteStrategy? found)
            ? found
            : _strategies[RouteStyle.Straight];
        return strategy.Route(request);
    }
}
