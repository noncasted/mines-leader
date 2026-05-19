using System;
using Internal;
using Shared;

namespace GamePlay.Players
{
    public interface IPlayerModifiers
    {
        IViewableDictionary<PlayerModifier, float> Values { get; }
        IViewableList<DurationalModifierOverview> Overviews { get; }

        void Set(PlayerModifier modifier, float value);
        void UpdateOverview(DurationalModifierOverview overview);
        void RemoveOverview(Guid sourceId);
    }

    public class PlayerModifiers : IPlayerModifiers
    {
        public PlayerModifiers()
        {
            foreach (var modifier in PlayerModifierExtensions.All)
                _values[modifier] = 0f;
        }

        private readonly ViewableDictionary<PlayerModifier, float> _values = new();
        private readonly ViewableList<DurationalModifierOverview> _overviews = new();

        public IViewableDictionary<PlayerModifier, float> Values => _values;
        public IViewableList<DurationalModifierOverview> Overviews => _overviews;

        public void Set(PlayerModifier modifier, float value)
        {
            _values[modifier] = value;
        }

        public void UpdateOverview(DurationalModifierOverview overview)
        {
            if (overview.TurnsToEnd == 0)
            {
                _overviews.Remove(overview);
                return;
            }

            var existingIndex = -1;
            for (var i = 0; i < _overviews.Count; i++)
            {
                if (_overviews[i].SourceId == overview.SourceId)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                var existing = _overviews[existingIndex];
                existing.TurnsToEnd = overview.TurnsToEnd;
                existing.Value = overview.Value;
                existing.Key = overview.Key;
                existing.Type = overview.Type;
                _overviews.NotifyChangedAt(existingIndex);
            }
            else
            {
                _overviews.Add(overview);
            }
        }

        public void RemoveOverview(Guid sourceId)
        {
            for (var i = 0; i < _overviews.Count; i++)
            {
                if (_overviews[i].SourceId == sourceId)
                {
                    _overviews.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
