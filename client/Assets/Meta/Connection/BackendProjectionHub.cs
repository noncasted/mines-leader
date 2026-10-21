using System;
using System.Collections.Generic;
using System.Linq;
using Internal;
using Shared;

namespace Meta
{
    public class BackendProjectionHub : OneWayCommand<SharedBackendProjection>
    {
        public BackendProjectionHub(IReadOnlyList<IBackendProjection> projections)
        {
            _projections = projections.ToDictionary(t => t.GetValueType());
        }

        private readonly Dictionary<Type, IBackendProjection> _projections;

        protected override void Execute(IReadOnlyLifetime lifetime, SharedBackendProjection context)
        {
            var projectionContext = context.Context;
            var type = projectionContext.GetType();

            // Every projection the backend sends has a registered receiver (MetaScopeExtensions): an unknown one
            // is a protocol mismatch, not something to drop.
            if (_projections.TryGetValue(type, out var projection) == false)
                throw new InvalidOperationException($"[Projection] No projection for type: {type.FullName}");

            projection.OnReceived(projectionContext);
        }
    }
}