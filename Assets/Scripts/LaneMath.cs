using UnityEngine;

public static class LaneMath
{
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
}
