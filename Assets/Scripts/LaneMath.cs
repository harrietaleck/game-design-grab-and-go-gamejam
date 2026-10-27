using UnityEngine;

public static class LaneMath
{
    // ---- Authoritative lane configuration (Task 1.3) ----
    // MainScene was authored with 4 lanes, 3.0 apart, centred on X = 0.
    // Every lane-aware component uses these values when its "useSharedLaneSettings" flag is on (default).
    public const int SharedLaneCount = 4;
    public const float SharedLaneDistance = 3f;
    public const float SharedLaneCenterX = 0f;

    public static int ClampLaneCount(int laneCount)
    {
        return Mathf.Max(1, laneCount);
    }

    public static float GetLaneX(int laneIndex, int laneCount, float laneDistance, float laneCenterX)
    {
        int safeLaneCount = ClampLaneCount(laneCount);
        int safeLaneIndex = Mathf.Clamp(laneIndex, 0, safeLaneCount - 1);
        return laneCenterX + (safeLaneIndex - (safeLaneCount - 1) * 0.5f) * laneDistance;
    }

    public static int GetNearestLaneIndex(float worldX, int laneCount, float laneDistance, float laneCenterX)
    {
        int safeLaneCount = ClampLaneCount(laneCount);
        if (Mathf.Approximately(laneDistance, 0f))
            return 0;

        float laneFloat = (worldX - laneCenterX) / laneDistance + (safeLaneCount - 1) * 0.5f;
        return Mathf.Clamp(Mathf.RoundToInt(laneFloat), 0, safeLaneCount - 1);
    }

    public static float SharedLaneX(int laneIndex) =>
        GetLaneX(laneIndex, SharedLaneCount, SharedLaneDistance, SharedLaneCenterX);

    public static int SharedNearestLane(float worldX) =>
        GetNearestLaneIndex(worldX, SharedLaneCount, SharedLaneDistance, SharedLaneCenterX);
}
