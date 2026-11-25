using UnityEngine;

namespace SlipperyZlope.RingToss.Utils
{
    /// <summary>The pattern printed on a ring.</summary>
    public enum RingPattern
    {
        /// <summary>No texture. The ring is its base colour and nothing else.</summary>
        Solid,

        /// <summary>Barber-pole stripes running diagonally around the ring.</summary>
        Candy,

        /// <summary>Bands straight across the tube, like a wrapped sweet.</summary>
        Bands,

        Checks,

        /// <summary>Polka dots on a staggered grid.</summary>
        Dots,

        /// <summary>Overlapping scallops, snake or fish scales.</summary>
        Scales,

        /// <summary>A zigzag running the whole way round.</summary>
        Chevron,

        /// <summary>Veined stone, from thresholded value noise.</summary>
        Marble,

        /// <summary>Sparse bright specks on a dark ground, with faint dust between.</summary>
        Starfield,

        /// <summary>A full hue wheel around the ring. Meant to be scrolled.</summary>
        Spectrum
    }

    /// <summary>
    /// Prints the ring's patterns.
    ///
    /// The shop's whole premise is that a skin is a row of numbers, and for nine of
    /// the ten that now includes a texture — because a skin that is only a different
    /// colour is not really a different skin. These are rasterised at load into small
    /// textures and handed to the ring through its property block, so the pool still
    /// shares one material.
    ///
    /// Every pattern is written against the torus's own UVs, where **u runs the whole
    /// way around the ring and v runs around the tube**. That is what makes the
    /// patterns read as printed on the object rather than projected onto it: bands in
    /// v wrap the tube like tape on a hoop, stripes in u+v spiral like a barber pole,
    /// and every one of them tiles seamlessly because both axes are already loops.
    /// </summary>
    public static class RingTextures
    {
        private const int Width = 256;
        private const int Height = 64;
        private const int Supersample = 3;

        /// <summary>
        /// A 1x1 white texture, for skins with no pattern.
        ///
        /// Needed because a property block keeps whatever was last written to it: a
        /// ring that wore a patterned skin and then a plain one would otherwise keep
        /// the old print. Clearing a texture property is not possible, so the plain
        /// skins set this instead.
        /// </summary>
        public static Texture2D White()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Ring Plain",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return texture;
        }

        public static Texture2D Create(RingPattern pattern, Color primary, Color secondary)
        {
            if (pattern == RingPattern.Solid)
            {
                return White();
            }

            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = $"Ring {pattern}",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };

            Color[] pixels = new Color[Width * Height];
            float step = 1f / Supersample;
            float weight = 1f / (Supersample * Supersample);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Color sum = Color.clear;

                    // Supersampled, because a hard-edged stripe on a ring this small on
                    // screen shimmers badly as it spins.
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            float u = (x + (sx + 0.5f) * step) / Width;
                            float v = (y + (sy + 0.5f) * step) / Height;

                            sum += Sample(pattern, u, v, primary, secondary) * weight;
                        }
                    }

                    pixels[y * Width + x] = sum;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Color Sample(RingPattern pattern, float u, float v, Color primary, Color secondary)
        {
            switch (pattern)
            {
                case RingPattern.Candy:
                    // Diagonal in u + v: a barber pole. The stripe count in u has to be
                    // a whole number or the seam shows where the ring closes on itself.
                    return Mathf.Repeat(u * 18f + v * 2f, 1f) < 0.5f ? primary : secondary;

                case RingPattern.Bands:
                    return Mathf.Repeat(v * 3f, 1f) < 0.42f ? secondary : primary;

                case RingPattern.Checks:
                    return (Mathf.FloorToInt(u * 24f) + Mathf.FloorToInt(v * 6f)) % 2 == 0
                        ? primary
                        : secondary;

                case RingPattern.Dots:
                {
                    // Staggered rows, so the dots read as scattered rather than as a grid.
                    float row = Mathf.Floor(v * 5f);
                    float cu = Mathf.Repeat(u * 20f + row * 0.5f, 1f) - 0.5f;
                    float cv = Mathf.Repeat(v * 5f, 1f) - 0.5f;

                    return cu * cu + cv * cv < 0.085f ? secondary : primary;
                }

                case RingPattern.Scales:
                {
                    float row = Mathf.Floor(v * 6f);
                    float cu = Mathf.Repeat(u * 22f + row * 0.5f, 1f) - 0.5f;
                    float cv = Mathf.Repeat(v * 6f, 1f);

                    // A disc whose lower edge is cut away leaves a scallop, and rows of
                    // scallops overlapping is exactly what a scale is.
                    float distance = Mathf.Sqrt(cu * cu + (cv - 0.9f) * (cv - 0.9f));
                    return distance > 0.62f ? secondary : primary;
                }

                case RingPattern.Chevron:
                {
                    float zig = Mathf.Abs(Mathf.Repeat(v * 2f, 1f) - 0.5f) * 2f;
                    return Mathf.Repeat(u * 16f + zig * 0.5f, 1f) < 0.45f ? primary : secondary;
                }

                case RingPattern.Marble:
                {
                    float n = Noise(u * 9f, v * 4f) * 0.6f
                              + Noise(u * 21f, v * 9f) * 0.3f
                              + Noise(u * 43f, v * 17f) * 0.1f;

                    // Veins rather than blobs: the interesting part of the noise is the
                    // narrow band around its midpoint, not either side of it.
                    float vein = 1f - Mathf.Clamp01(Mathf.Abs(n - 0.5f) * 9f);
                    return Color.Lerp(primary, secondary, vein);
                }

                case RingPattern.Starfield:
                {
                    float dust = Noise(u * 30f, v * 12f);
                    Color ground = Color.Lerp(primary, secondary, dust * 0.18f);

                    float star = Noise(u * 61f + 11.3f, v * 23f + 4.1f);

                    if (star > 0.93f)
                    {
                        return Color.Lerp(ground, secondary, (star - 0.93f) / 0.07f);
                    }

                    return ground;
                }

                case RingPattern.Spectrum:
                    // Full wheel around the ring. Scrolled at runtime, which is what
                    // makes this the one skin that visibly moves.
                    return Color.HSVToRGB(Mathf.Repeat(u, 1f), 0.78f, 1f);

                default:
                    return primary;
            }
        }

        /// <summary>
        /// Smoothed value noise, tiling on both axes.
        ///
        /// Written out rather than using Mathf.PerlinNoise because that one does not
        /// tile, and a ring is a loop in both directions — a seam would run right
        /// across the object once per revolution.
        /// </summary>
        private static float Noise(float x, float y)
        {
            int period = 64;

            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;

            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);

            float a = Hash(x0, y0, period);
            float b = Hash(x0 + 1, y0, period);
            float c = Hash(x0, y0 + 1, period);
            float d = Hash(x0 + 1, y0 + 1, period);

            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        private static float Hash(int x, int y, int period)
        {
            // Wrapped so the lattice repeats, which is what makes the noise tile.
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;

            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }
}
