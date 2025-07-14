using System;
using System.Collections.Generic;
using System.Linq;
using Global.Backend;
using Internal;
using Shared;
using UnityEngine;

namespace Meta
{
    public class BackendProjectionHub : OneWayCommand<BackendProjectionContext>
    {
        public BackendProjectionHub(IReadOnlyList<IBackendProjection> projections)
        {
            _projections = projections.ToDictionary(t => t.GetValueType());
        }

        private readonly Dictionary<Type, IBackendProjection> _projections;

        protected override void Execute(IReadOnlyLifetime lifetime, BackendProjectionContext context)
        {
            var projectionContext = context.Context;
            var type = projectionContext.GetType();

            if (_projections.TryGetValue(type, out var projection) == false)
            {
                Debug.Log($"[Projection] No projection for type: {type.FullName}");
                return;
            }

            projection.OnReceived(projectionContext);
        }
    }
}