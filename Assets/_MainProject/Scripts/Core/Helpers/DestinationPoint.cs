using UnityEngine;
using System.Collections.Generic;
namespace GameUp.Core.Helpers
{
    public class DestinationPoint : MonoBehaviour
    {
        private static readonly Dictionary<DestinationType, List<Transform>> _destinationPoints = new();
        [SerializeField] private DestinationType pointType;

        public static Transform GetFirstDestination(DestinationType type)
        {
            return TryGetValidPoints(type, out var points) ? points[0] : null;
        }

        public static Transform GetLastDestination(DestinationType type)
        {
            return TryGetValidPoints(type, out var points) ? points[^1] : null;
        }

        private void OnEnable()
        {
            if (!_destinationPoints.TryGetValue(pointType, out var points))
            {
                points = new List<Transform>(1);
                _destinationPoints.Add(pointType, points);
            }

            if (!points.Contains(transform))
                points.Add(transform);
        }

        private void OnDisable()
        {
            if (!_destinationPoints.TryGetValue(pointType, out var points))
                return;

            points.Remove(transform);
            if (points.Count == 0)
                _destinationPoints.Remove(pointType);
        }

        public static bool HasDestination(DestinationType type)
        {
            return TryGetValidPoints(type, out _);
        }

        private static bool TryGetValidPoints(DestinationType type, out List<Transform> points)
        {
            if (!_destinationPoints.TryGetValue(type, out points))
                return false;

            for (var i = points.Count - 1; i >= 0; i--)
            {
                if (points[i] == null)
                    points.RemoveAt(i);
            }

            if (points.Count == 0)
            {
                _destinationPoints.Remove(type);
                return false;
            }

            return true;
        }
    }

    public enum DestinationType
    {
        Tutorial_1,
        Tutorial_2,
        Tutorial_3,
        Tutorial_4,
        Tutorial_5,
        Tutorial_6,
        Tutorial_7,
        Tutorial_8,
        Tutorial_9,
    }
}