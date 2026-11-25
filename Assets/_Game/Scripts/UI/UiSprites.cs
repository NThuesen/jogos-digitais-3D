using UnityEngine;

namespace SlipperyZlope.RingToss.UI
{
    /// <summary>
    /// Generates the handful of sprites the interface needs.
    ///
    /// The project has no imported art, and a UI built only from hard rectangles looks
    /// like a debug overlay no matter how well it is laid out. Rounded panels, circles
    /// and a star are a few lines of rasterisation each, and generating them keeps the
    /// interface in the same place as everything else: code.
    ///
    /// Every shape is supersampled, because a hard-edged generated texture scaled to a
    /// phone screen looks worse than no rounding at all.
    /// </summary>
    public static class UiSprites
    {
        private const int Supersample = 4;

        /// <summary>
        /// A rounded rectangle with a 9-slice border, so one texture serves buttons and
        /// panels at any size without the corners stretching.
        /// </summary>
        public static Sprite RoundedRect(int size = 64, int cornerRadius = 20)
        {
            Texture2D texture = CreateTexture(size, size, (x, y) =>
                RoundedRectCoverage(x, y, size, size, cornerRadius));

            float border = cornerRadius + 1;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        /// <summary>
        /// A painted fairground plaque: a rounded body shaded from top to bottom, with a
        /// brighter rim around it.
        ///
        /// The buttons were flat rounded rectangles, and against a booth built out of
        /// striped canvas, brass and warm bulbs they read as a different application
        /// sitting on top of the game — which is what the owner said about them. This is
        /// the cheapest honest fix: the same silhouette and the same layout, lit like the
        /// woodwork behind it.
        ///
        /// The whole thing is one tinted mask, so the trick is that the value can only
        /// ever be a multiplier on the button's own colour. A rim cannot be made brighter
        /// than the body, so the body is made darker than the rim instead: 0.72 to 0.96
        /// down the face, 1.0 around the edge. Every button therefore keeps its authored
        /// colour — the audit's contrast numbers are measured against that colour — while
        /// gaining an edge that catches the light.
        /// </summary>
        public static Sprite Plaque(int size = 96, int cornerRadius = 26, int rim = 5)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                // Texture space runs bottom-up, so this is the fraction of the way to the
                // top of the plaque — where the light is.
                float up = size <= 1 ? 1f : (float)y / (size - 1);
                float body = Mathf.Lerp(0.72f, 0.96f, up);

                for (int x = 0; x < size; x++)
                {
                    float distance = RoundedRectDistance(x + 0.5f, y + 0.5f, size, size, cornerRadius);

                    // One pixel of falloff across the outline, which is all the
                    // antialiasing a shape this size needs.
                    float alpha = Mathf.Clamp01(distance + 0.5f);
                    float rimAmount = Mathf.Clamp01(rim + 0.5f - distance);
                    float value = Mathf.Lerp(body, 1f, rimAmount);

                    pixels[y * size + x] = new Color(value, value, value, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // The border has to clear the rim as well as the corner, or a stretched
            // button pulls its own edge into a smear.
            float border = cornerRadius + rim + 2f;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        public static Sprite Circle(int size = 64)
        {
            Texture2D texture = CreateTexture(size, size, (x, y) =>
            {
                float centre = size * 0.5f;
                float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                return distance <= centre - 0.5f ? 1f : 0f;
            });

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>An outlined circle. Used for the rings-in-hand counter.</summary>
        public static Sprite Ring(int size = 64, float thicknessFraction = 0.22f)
        {
            Texture2D texture = CreateTexture(size, size, (x, y) =>
            {
                float centre = size * 0.5f;
                float outer = centre - 0.5f;
                float inner = outer * (1f - thicknessFraction * 2f);
                float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));

                return distance <= outer && distance >= inner ? 1f : 0f;
            });

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>A five-pointed star for the level results.</summary>
        public static Sprite Star(int size = 64)
        {
            Vector2[] points = new Vector2[10];
            float centre = size * 0.5f;
            float outerRadius = centre - 1f;
            float innerRadius = outerRadius * 0.42f;

            for (int i = 0; i < 10; i++)
            {
                // Start at the top and alternate between the tips and the valleys.
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? outerRadius : innerRadius;

                points[i] = new Vector2(
                    centre + Mathf.Cos(angle) * radius,
                    centre + Mathf.Sin(angle) * radius);
            }

            Texture2D texture = CreateTexture(size, size, (x, y) =>
                IsInsidePolygon(points, x, y) ? 1f : 0f);

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// A heart, for the endless run's lives.
        ///
        /// Rasterised from the implicit curve (u^2 + v^2 - 1)^3 - u^2 v^3 = 0 rather
        /// than assembled from two circles and a triangle: it is one expression, it is
        /// smooth where the lobes meet, and the supersampler antialiases it for free.
        /// </summary>
        public static Sprite Heart(int size = 64)
        {
            Texture2D texture = CreateTexture(size, size, (x, y) =>
            {
                // The curve spans about u in [-1.15, 1.15] and v in [-1, 1.25], so the
                // centre sits above the middle of the texture and the point hangs below.
                float radius = size * 0.42f;
                float u = (x - size * 0.5f) / radius;
                float v = (y - size * 0.46f) / radius;

                float body = u * u + v * v - 1f;

                return body * body * body - u * u * v * v * v <= 0f ? 1f : 0f;
            });

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// A fairground ticket stub: a rounded slip with a bite out of each end and a
        /// perforation a third of the way along. The bites and the perforation are what
        /// make it a ticket at forty pixels rather than a gold rectangle.
        /// </summary>
        public static Sprite Ticket(int width = 96, int height = 56)
        {
            float radius = height * 0.16f;
            float bite = height * 0.17f;
            float perforation = width * 0.3f;

            Texture2D texture = CreateTexture(width, height, (x, y) =>
            {
                if (RoundedRectCoverage(x, y, width, height, radius) <= 0f)
                {
                    return 0f;
                }

                float middle = height * 0.5f;

                if ((x * x + (y - middle) * (y - middle)) <= bite * bite
                    || ((x - width) * (x - width) + (y - middle) * (y - middle)) <= bite * bite)
                {
                    return 0f;
                }

                // Dashes, not a line: a solid cut would split the sprite in two.
                bool onPerforation = Mathf.Abs(x - perforation) < 1.2f;
                bool inGap = Mathf.Repeat(y, height / 7f) < height / 14f;

                return onPerforation && inGap ? 0f : 1f;
            });

            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// A price tag: pointed at the left, rounded at the right, with the hole the
        /// string goes through. The shape says "offer" before anything written on it does.
        /// </summary>
        public static Sprite Tag(int width = 128, int height = 64)
        {
            float point = height * 0.46f;
            float radius = height * 0.22f;
            float hole = height * 0.1f;

            Texture2D texture = CreateTexture(width, height, (x, y) =>
            {
                float middle = height * 0.5f;

                if (x < point)
                {
                    // The pointed end: the slanted edges run from the tip to the body.
                    float reach = (x / point) * middle;

                    if (Mathf.Abs(y - middle) > reach)
                    {
                        return 0f;
                    }
                }
                else if (RoundedRectCoverage(x, y, width, height, radius) <= 0f && x > width - radius)
                {
                    return 0f;
                }

                float holeX = point * 0.72f;

                return (x - holeX) * (x - holeX) + (y - middle) * (y - middle) <= hole * hole ? 0f : 1f;
            });

            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// A soft vertical gradient, white at the top fading to transparent. Used to
        /// keep HUD text legible over a bright sky without a solid bar.
        /// </summary>
        public static Sprite VerticalFade(int height = 64)
        {
            Texture2D texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "UI Vertical Fade"
            };

            Color[] pixels = new Color[height];

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                pixels[y] = new Color(1f, 1f, 1f, t * t);
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, 1f, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float RoundedRectCoverage(float x, float y, int width, int height, float radius)
        {
            float left = radius;
            float right = width - radius;
            float bottom = radius;
            float top = height - radius;

            float nearestX = Mathf.Clamp(x, left, right);
            float nearestY = Mathf.Clamp(y, bottom, top);

            float dx = x - nearestX;
            float dy = y - nearestY;

            return dx * dx + dy * dy <= radius * radius ? 1f : 0f;
        }

        /// <summary>
        /// Distance from a rounded rectangle's outline, positive inside it. Used to
        /// shade a border without a second texture.
        /// </summary>
        private static float RoundedRectDistance(float x, float y, int width, int height, float radius)
        {
            float left = radius;
            float right = width - radius;
            float bottom = radius;
            float top = height - radius;

            float dx = x - Mathf.Clamp(x, left, right);
            float dy = y - Mathf.Clamp(y, bottom, top);
            float corner = Mathf.Sqrt(dx * dx + dy * dy);

            if (corner > 0f)
            {
                return radius - corner;
            }

            return Mathf.Min(Mathf.Min(x, width - x), Mathf.Min(y, height - y));
        }

        private static bool IsInsidePolygon(Vector2[] points, float x, float y)
        {
            bool inside = false;

            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                if (points[i].y > y != points[j].y > y
                    && x < (points[j].x - points[i].x) * (y - points[i].y)
                    / (points[j].y - points[i].y) + points[i].x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Rasterises a coverage function into a white texture with a soft alpha edge.
        /// The colour stays white so the UI can tint it; only the alpha carries shape.
        /// </summary>
        private static Texture2D CreateTexture(int width, int height, System.Func<float, float, float> coverage)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[width * height];
            float step = 1f / Supersample;
            float weight = 1f / (Supersample * Supersample);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = 0f;

                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            alpha += coverage(
                                x + (sx + 0.5f) * step,
                                y + (sy + 0.5f) * step) * weight;
                        }
                    }

                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
