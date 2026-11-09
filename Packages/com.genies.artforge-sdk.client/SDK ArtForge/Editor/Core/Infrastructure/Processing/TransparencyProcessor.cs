using System.Collections.Generic;
using UnityEngine;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Processing
{
    public static class TransparencyProcessor
    {
        public class Settings
        {
            public bool useCornerSampling = true;
            public bool useNearestCornerMatch = true;
            public bool useFloodFillFromEdges = true;
            public bool useAutomaticThreshold = true;
            public bool useEdgeDecontamination = true;
            public bool invertAlpha = false;

            public Color manualBackgroundColor = Color.magenta;

            public float tolerance = 0.6f;
            public int edgeDecontaminationBandPixels = 3;
            public float edgeDecontaminationColorDistanceMax = 1f;
            public float edgeDecontaminationStrength = 4f;
            public float edgeDecontaminationAlphaReduction = 4f;
            public float edgeDecontaminationMinUnmatteAlpha = 0.1f;
            public float edgeDecontaminationBackgroundAlphaThreshold = 0.08f;
            public float automaticThresholdMin = 0.08f;
            public float automaticThresholdMax = 0.75f;
            public float automaticThresholdBias = 1f;
            public int cornerSampleSize = 16;
        }

        public class Result
        {
            public Texture2D texture;
            public Color[] cornerColors;
            public Color backgroundColor;
            public float appliedTolerance;
        }

        public static Result Process(Texture2D sourceTexture, Settings settings)
        {
            int width = sourceTexture.width;
            int height = sourceTexture.height;

            Color[] sourcePixels = sourceTexture.GetPixels();
            float[] alphaMask = new float[sourcePixels.Length];

            Color[] cornerColors = null;
            Color backgroundColor = settings.manualBackgroundColor;

            if (settings.useCornerSampling)
            {
                cornerColors = GetCornerColors(sourceTexture, settings.cornerSampleSize);

                if (!settings.useNearestCornerMatch)
                {
                    backgroundColor = AverageColors(cornerColors);
                }
            }

            float effectiveTolerance = settings.useAutomaticThreshold
                ? ComputeAutomaticTolerance(width, height, sourcePixels, cornerColors, backgroundColor, settings)
                : settings.tolerance;

            bool[] floodBackgroundMask = null;
            if (settings.useFloodFillFromEdges)
            {
                floodBackgroundMask = BuildFloodBackgroundMask(
                    width,
                    height,
                    sourcePixels,
                    cornerColors,
                    backgroundColor,
                    effectiveTolerance,
                    settings
                );
            }

            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color pixel = sourcePixels[i];

                float distance = ComputeBackgroundDistance(pixel, cornerColors, backgroundColor, settings);
                float alpha = EvaluateAlpha(distance, effectiveTolerance);

                if (settings.useFloodFillFromEdges && floodBackgroundMask != null && floodBackgroundMask[i])
                {
                    alpha = 0f;
                }

                if (settings.invertAlpha)
                {
                    alpha = 1f - alpha;
                }

                alphaMask[i] = Mathf.Clamp01(alpha);
            }

            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] outputPixels = new Color[sourcePixels.Length];

            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color c = sourcePixels[i];
                c.a = alphaMask[i];
                outputPixels[i] = c;
            }

            if (settings.useEdgeDecontamination)
            {
                ApplyEdgeDecontamination(
                    outputPixels,
                    alphaMask,
                    floodBackgroundMask,
                    width,
                    height,
                    cornerColors,
                    backgroundColor,
                    settings);
            }

            output.SetPixels(outputPixels);
            output.Apply();

            return new Result
            {
                texture = output,
                cornerColors = cornerColors,
                backgroundColor = backgroundColor,
                appliedTolerance = effectiveTolerance
            };
        }

        private static float ComputeAutomaticTolerance(
            int width,
            int height,
            Color[] pixels,
            Color[] cornerColors,
            Color backgroundColor,
            Settings settings)
        {
            if (pixels == null || pixels.Length == 0)
            {
                return settings.tolerance;
            }

            const float maxColorDistance = 1.7320508f;

            int total = width * height;
            var edgeDistances = new List<float>(Mathf.Max(8, (width + height) * 2));
            var allDistances = new float[total];

            for (int i = 0; i < total; i++)
            {
                float distance = ComputeBackgroundDistance(pixels[i], cornerColors, backgroundColor, settings);
                allDistances[i] = distance;

                int x = i % width;
                int y = i / width;
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    edgeDistances.Add(distance);
                }
            }

            if (edgeDistances.Count == 0)
            {
                return settings.tolerance;
            }

            edgeDistances.Sort();
            float edgeP85 = GetPercentile(edgeDistances, 0.85f);
            float otsuThreshold = ComputeOtsuThreshold(allDistances, maxColorDistance);
            float candidate = Mathf.Max(edgeP85, otsuThreshold * Mathf.Max(0.01f, settings.automaticThresholdBias));

            float min = Mathf.Min(settings.automaticThresholdMin, settings.automaticThresholdMax);
            float max = Mathf.Max(settings.automaticThresholdMin, settings.automaticThresholdMax);
            return Mathf.Clamp(candidate, min, max);
        }

        private static float GetPercentile(List<float> sortedValues, float percentile)
        {
            if (sortedValues == null || sortedValues.Count == 0)
            {
                return 0f;
            }

            float clampedPercentile = Mathf.Clamp01(percentile);
            float position = (sortedValues.Count - 1) * clampedPercentile;
            int lowerIndex = Mathf.FloorToInt(position);
            int upperIndex = Mathf.CeilToInt(position);
            if (lowerIndex == upperIndex)
            {
                return sortedValues[lowerIndex];
            }

            float t = position - lowerIndex;
            return Mathf.Lerp(sortedValues[lowerIndex], sortedValues[upperIndex], t);
        }

        private static float ComputeOtsuThreshold(float[] distances, float maxDistance)
        {
            if (distances == null || distances.Length == 0 || maxDistance <= 0f)
            {
                return 0f;
            }

            const int binCount = 256;
            int[] histogram = new int[binCount];

            for (int i = 0; i < distances.Length; i++)
            {
                float normalized = Mathf.Clamp01(distances[i] / maxDistance);
                int bin = Mathf.Clamp(Mathf.RoundToInt(normalized * (binCount - 1)), 0, binCount - 1);
                histogram[bin]++;
            }

            int total = distances.Length;
            double sum = 0d;
            for (int i = 0; i < binCount; i++)
            {
                sum += i * histogram[i];
            }

            int weightBackground = 0;
            double sumBackground = 0d;
            double maxVariance = -1d;
            int thresholdBin = 0;

            for (int i = 0; i < binCount; i++)
            {
                weightBackground += histogram[i];
                if (weightBackground == 0)
                {
                    continue;
                }

                int weightForeground = total - weightBackground;
                if (weightForeground == 0)
                {
                    break;
                }

                sumBackground += i * histogram[i];
                double meanBackground = sumBackground / weightBackground;
                double meanForeground = (sum - sumBackground) / weightForeground;
                double meanDelta = meanBackground - meanForeground;
                double variance = weightBackground * (double)weightForeground * meanDelta * meanDelta;

                if (variance > maxVariance)
                {
                    maxVariance = variance;
                    thresholdBin = i;
                }
            }

            float normalizedThreshold = thresholdBin / (float)(binCount - 1);
            return normalizedThreshold * maxDistance;
        }

        private static Color UnmatteInLinearSpace(Color observed, Color background, float alpha)
        {
            Color observedLinear = observed.linear;
            Color backgroundLinear = background.linear;
            float invAlpha = 1f - alpha;

            Color recoveredLinear = new Color(
                (observedLinear.r - backgroundLinear.r * invAlpha) / alpha,
                (observedLinear.g - backgroundLinear.g * invAlpha) / alpha,
                (observedLinear.b - backgroundLinear.b * invAlpha) / alpha,
                observed.a
            );

            recoveredLinear.r = Mathf.Clamp01(recoveredLinear.r);
            recoveredLinear.g = Mathf.Clamp01(recoveredLinear.g);
            recoveredLinear.b = Mathf.Clamp01(recoveredLinear.b);

            Color recovered = recoveredLinear.gamma;
            recovered.a = observed.a;
            recovered.r = Mathf.Clamp01(recovered.r);
            recovered.g = Mathf.Clamp01(recovered.g);
            recovered.b = Mathf.Clamp01(recovered.b);
            return recovered;
        }

        private static void ApplyEdgeDecontamination(
            Color[] pixels,
            float[] alphaMask,
            bool[] floodBackgroundMask,
            int width,
            int height,
            Color[] cornerColors,
            Color backgroundColor,
            Settings settings)
        {
            if (pixels == null || alphaMask == null || pixels.Length != alphaMask.Length || pixels.Length == 0)
            {
                return;
            }

            int bandPixels = Mathf.Clamp(settings.edgeDecontaminationBandPixels, 1, 32);
            float colorDistanceMax = Mathf.Max(0.0001f, settings.edgeDecontaminationColorDistanceMax);
            float decontaminateStrength = Mathf.Clamp(settings.edgeDecontaminationStrength, 0f, 4f);
            float alphaReductionStrength = Mathf.Clamp(settings.edgeDecontaminationAlphaReduction, 0f, 4f);
            float minUnmatteAlpha = Mathf.Clamp(settings.edgeDecontaminationMinUnmatteAlpha, 0.001f, 0.99f);
            float backgroundAlphaThreshold = Mathf.Clamp01(settings.edgeDecontaminationBackgroundAlphaThreshold);

            if (decontaminateStrength <= 0f)
            {
                return;
            }

            int[] inlandDistance = BuildInlandDistanceMap(
                alphaMask,
                floodBackgroundMask,
                width,
                height,
                bandPixels,
                backgroundAlphaThreshold);

            int total = pixels.Length;
            for (int i = 0; i < total; i++)
            {
                float alpha = alphaMask[i];
                if (alpha <= 0.001f)
                {
                    continue;
                }

                int distance = inlandDistance[i];
                if (distance <= 0 || distance > bandPixels)
                {
                    continue;
                }

                Color observed = pixels[i];
                Color pixelBackground = GetBackgroundColorForPixel(observed, cornerColors, backgroundColor, settings);

                float colorDistance = ColorDistance(observed, pixelBackground);
                float colorSimilarity = 1f - Mathf.Clamp01(colorDistance / colorDistanceMax);
                if (colorSimilarity <= 0f)
                {
                    continue;
                }

                float normalizedDistance = (distance - 1f) / Mathf.Max(1f, bandPixels - 1f);
                float distanceFalloff = 1f - Mathf.SmoothStep(0f, 1f, normalizedDistance);
                float contamination = Mathf.Clamp01(decontaminateStrength * colorSimilarity * distanceFalloff);
                if (contamination <= 0f)
                {
                    continue;
                }

                float safeAlpha = Mathf.Max(minUnmatteAlpha, alpha);
                Color recovered = UnmatteInLinearSpace(observed, pixelBackground, safeAlpha);
                observed.r = Mathf.Lerp(observed.r, recovered.r, contamination);
                observed.g = Mathf.Lerp(observed.g, recovered.g, contamination);
                observed.b = Mathf.Lerp(observed.b, recovered.b, contamination);

                float alphaReduction = Mathf.Clamp01(alphaReductionStrength * colorSimilarity * distanceFalloff);
                float adjustedAlpha = alpha * (1f - alphaReduction);
                alphaMask[i] = Mathf.Clamp01(Mathf.Min(alphaMask[i], adjustedAlpha));
                observed.a = alphaMask[i];
                pixels[i] = observed;
            }
        }

        private static int[] BuildInlandDistanceMap(
            float[] alphaMask,
            bool[] floodBackgroundMask,
            int width,
            int height,
            int maxDistance,
            float backgroundAlphaThreshold)
        {
            int total = alphaMask.Length;
            int[] distance = new int[total];
            Queue<int> queue = new Queue<int>(total);

            for (int i = 0; i < total; i++)
            {
                bool isBackgroundSeed = (floodBackgroundMask != null && floodBackgroundMask.Length == total && floodBackgroundMask[i])
                    || alphaMask[i] <= backgroundAlphaThreshold;

                if (isBackgroundSeed)
                {
                    distance[i] = 0;
                    queue.Enqueue(i);
                }
                else
                {
                    distance[i] = int.MaxValue;
                }
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int nextDistance = distance[index] + 1;
                if (nextDistance > maxDistance)
                {
                    continue;
                }

                int x = index % width;
                int y = index / width;
                TrySetDistance(x - 1, y, width, height, nextDistance, distance, queue);
                TrySetDistance(x + 1, y, width, height, nextDistance, distance, queue);
                TrySetDistance(x, y - 1, width, height, nextDistance, distance, queue);
                TrySetDistance(x, y + 1, width, height, nextDistance, distance, queue);
            }

            return distance;
        }

        private static void TrySetDistance(
            int x,
            int y,
            int width,
            int height,
            int nextDistance,
            int[] distance,
            Queue<int> queue)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            int index = y * width + x;
            if (nextDistance >= distance[index])
            {
                return;
            }

            distance[index] = nextDistance;
            queue.Enqueue(index);
        }

        private static Color GetBackgroundColorForPixel(
            Color pixel,
            Color[] cornerColors,
            Color backgroundColor,
            Settings settings)
        {
            if (!settings.useCornerSampling || cornerColors == null || cornerColors.Length == 0)
            {
                return backgroundColor;
            }

            if (!settings.useNearestCornerMatch)
            {
                return backgroundColor;
            }

            int nearestCornerIndex = 0;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < cornerColors.Length; i++)
            {
                float distance = ColorDistance(pixel, cornerColors[i]);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestCornerIndex = i;
                }
            }

            return cornerColors[nearestCornerIndex];
        }

        private static float EvaluateAlpha(float distance, float tolerance)
        {
            return distance <= tolerance ? 0f : 1f;
        }

        private static bool[] BuildFloodBackgroundMask(
            int width,
            int height,
            Color[] pixels,
            Color[] cornerColors,
            Color backgroundColor,
            float tolerance,
            Settings settings)
        {
            int total = width * height;
            bool[] visited = new bool[total];
            bool[] backgroundMask = new bool[total];
            Queue<int> queue = new Queue<int>();

            EnqueueEdgePixels(queue, visited, width, height);

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                float distance = ComputeBackgroundDistance(pixels[index], cornerColors, backgroundColor, settings);

                if (distance > tolerance)
                {
                    continue;
                }

                backgroundMask[index] = true;

                int x = index % width;
                int y = index / width;

                TryEnqueue(x - 1, y, width, height, visited, queue);
                TryEnqueue(x + 1, y, width, height, visited, queue);
                TryEnqueue(x, y - 1, width, height, visited, queue);
                TryEnqueue(x, y + 1, width, height, visited, queue);
            }

            return backgroundMask;
        }

        private static void EnqueueEdgePixels(Queue<int> queue, bool[] visited, int width, int height)
        {
            for (int x = 0; x < width; x++)
            {
                TryEnqueue(x, 0, width, height, visited, queue);
                TryEnqueue(x, height - 1, width, height, visited, queue);
            }

            for (int y = 0; y < height; y++)
            {
                TryEnqueue(0, y, width, height, visited, queue);
                TryEnqueue(width - 1, y, width, height, visited, queue);
            }
        }

        private static void TryEnqueue(int x, int y, int width, int height, bool[] visited, Queue<int> queue)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            int index = y * width + x;
            if (visited[index])
            {
                return;
            }

            visited[index] = true;
            queue.Enqueue(index);
        }

        private static Color[] GetCornerColors(Texture2D texture, int sampleSize)
        {
            int width = texture.width;
            int height = texture.height;

            int sampleWidth = Mathf.Min(sampleSize, width);
            int sampleHeight = Mathf.Min(sampleSize, height);

            return new[]
            {
                SampleBlock(texture, 0, 0, sampleWidth, sampleHeight),
                SampleBlock(texture, width - sampleWidth, 0, sampleWidth, sampleHeight),
                SampleBlock(texture, 0, height - sampleHeight, sampleWidth, sampleHeight),
                SampleBlock(texture, width - sampleWidth, height - sampleHeight, sampleWidth, sampleHeight)
            };
        }

        private static Color SampleBlock(Texture2D texture, int startX, int startY, int sampleWidth, int sampleHeight)
        {
            Color total = Color.black;
            int count = 0;

            for (int y = startY; y < startY + sampleHeight; y++)
            {
                for (int x = startX; x < startX + sampleWidth; x++)
                {
                    total += texture.GetPixel(x, y);
                    count++;
                }
            }

            if (count == 0)
            {
                return Color.white;
            }

            return total / count;
        }

        private static Color AverageColors(Color[] colors)
        {
            if (colors == null || colors.Length == 0)
            {
                return Color.white;
            }

            Color total = Color.black;
            for (int i = 0; i < colors.Length; i++)
            {
                total += colors[i];
            }

            return total / colors.Length;
        }

        private static float ComputeBackgroundDistance(
            Color pixel,
            Color[] cornerColors,
            Color backgroundColor,
            Settings settings)
        {
            return settings.useCornerSampling
                ? (settings.useNearestCornerMatch
                    ? DistanceToNearestCorner(pixel, cornerColors)
                    : ColorDistance(pixel, backgroundColor))
                : ColorDistance(pixel, settings.manualBackgroundColor);
        }

        private static float DistanceToNearestCorner(Color pixel, Color[] corners)
        {
            if (corners == null || corners.Length == 0)
            {
                return 1f;
            }

            float minDistance = float.MaxValue;

            for (int i = 0; i < corners.Length; i++)
            {
                float distance = ColorDistance(pixel, corners[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            return minDistance;
        }

        private static float ColorDistance(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}
