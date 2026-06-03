using Draw.Model.Connectors;

namespace Draw.Diagramming.Routing;

/// <summary>Routes a connector, dispatching to the strategy for the requested <see cref="RouteStyle"/>.</summary>
public interface IConnectorRouter
{
    ConnectorRoute Route(ConnectorRouteRequest request);
}

/// <summary>A routing strategy for a single <see cref="RouteStyle"/>.</summary>
public interface IConnectorRouteStrategy
{
    RouteStyle Style { get; }

    ConnectorRoute Route(ConnectorRouteRequest request);
}
