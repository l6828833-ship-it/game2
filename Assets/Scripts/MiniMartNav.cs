using System.Collections.Generic;
using UnityEngine;

namespace MiniMart
{
    /// <summary>Shared shop floor geometry. The world builder and the navigation both read from here.</summary>
    public static class StoreLayout
    {
        /// <summary>X centres of the two starting shelf rows.</summary>
        public static readonly float[] ShelfColumns = { -5.5f, -2.5f, 0.5f, 3.5f };

        public const float BackRowZ = 4.6f;
        public const float FrontRowZ = 1.7f;
        public const float ShelfHalfWidth = 1.15f;

        /// <summary>Walkable lanes, front of store first. Shoppers only travel sideways inside a lane.</summary>
        public static readonly float[] Lanes = { -3.0f, 0.55f, 3.15f };

        /// <summary>Gaps between the starting shelves, used when a lane change has to dodge a row.</summary>
        public static readonly float[] Crossings = { -7.6f, -4.0f, -1.0f, 2.0f, 5.6f };

        /// <summary>Where shoppers enter and leave the shop floor. South of the left wall.</summary>
        public const float DoorwayX = -8.4f;
    }

    /// <summary>
    /// Lightweight aisle routing. Shoppers used to slide straight through the shelving; now they
    /// step into a lane, walk it, and only change lane where nothing is in the way.
    /// </summary>
    public static class MiniMartNav
    {
        public static bool IsInsideStore(Vector3 point) => point.x > -9.4f;

        public static int NearestLaneIndex(float z)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < StoreLayout.Lanes.Length; i++)
            {
                float distance = Mathf.Abs(StoreLayout.Lanes[i] - z);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        /// <summary>Fills <paramref name="path"/> with waypoints from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static void BuildPath(Vector3 from, Vector3 to, List<Vector3> path)
        {
            path.Clear();
            bool startsInside = IsInsideStore(from);
            bool endsInside = IsInsideStore(to);
            int fromLane = startsInside ? NearestLaneIndex(from.z) : 0;
            int toLane = endsInside ? NearestLaneIndex(to.z) : 0;

            float currentX = from.x;
            if (Mathf.Abs(from.z - StoreLayout.Lanes[fromLane]) > 0.75f)
                path.Add(new Vector3(currentX, 0f, StoreLayout.Lanes[fromLane]));

            // Coming in from the farm: use the doorway before turning into the shop.
            if (!startsInside && endsInside)
            {
                currentX = StoreLayout.DoorwayX;
                path.Add(new Vector3(currentX, 0f, StoreLayout.Lanes[0]));
            }

            int lane = fromLane;
            int step = toLane > fromLane ? 1 : -1;
            while (lane != toLane)
            {
                int next = lane + step;
                float laneZ = StoreLayout.Lanes[lane];
                float nextZ = StoreLayout.Lanes[next];
                if (SegmentBlocked(currentX, laneZ, nextZ))
                {
                    currentX = ClearCrossing((currentX + to.x) * 0.5f, laneZ, nextZ);
                    path.Add(new Vector3(currentX, 0f, laneZ));
                }
                path.Add(new Vector3(currentX, 0f, nextZ));
                lane = next;
            }

            path.Add(new Vector3(to.x, 0f, StoreLayout.Lanes[lane]));
            path.Add(new Vector3(to.x, 0f, to.z));
        }

        /// <summary>Crossing gap closest to <paramref name="preferredX"/> that no shelf is sitting in.</summary>
        private static float ClearCrossing(float preferredX, float zA, float zB)
        {
            float bestClear = float.NaN;
            float bestClearDistance = float.MaxValue;
            float fallback = StoreLayout.Crossings[0];
            float fallbackDistance = float.MaxValue;

            for (int i = 0; i < StoreLayout.Crossings.Length; i++)
            {
                float candidate = StoreLayout.Crossings[i];
                float distance = Mathf.Abs(candidate - preferredX);
                if (distance < fallbackDistance)
                {
                    fallbackDistance = distance;
                    fallback = candidate;
                }
                if (SegmentBlocked(candidate, zA, zB) || distance >= bestClearDistance) continue;
                bestClearDistance = distance;
                bestClear = candidate;
            }

            return float.IsNaN(bestClear) ? fallback : bestClear;
        }

        /// <summary>True when a shelf stands on the straight line between two lanes at this x.</summary>
        private static bool SegmentBlocked(float x, float zA, float zB)
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            if (game == null) return false;

            float low = Mathf.Min(zA, zB) - 0.55f;
            float high = Mathf.Max(zA, zB) + 0.55f;
            for (int i = 0; i < game.Shelves.Count; i++)
            {
                ShelfUnit shelf = game.Shelves[i];
                if (shelf == null) continue;
                Vector3 position = shelf.transform.position;
                if (position.z < low || position.z > high) continue;
                if (Mathf.Abs(position.x - x) > StoreLayout.ShelfHalfWidth + 0.25f) continue;
                return true;
            }
            return false;
        }
    }
}
