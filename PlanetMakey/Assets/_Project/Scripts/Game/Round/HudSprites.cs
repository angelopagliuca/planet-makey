using UnityEngine;
using UnityEngine.UI;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// Helpers for the round's on-screen display: small placeholder icons (a cow, a moon, a sun) drawn into
    /// textures at run time, so the display needs no art, and a way to make a UI Image in code. The views accept
    /// real sprites and only draw these when none is given. Textures and sprites made here are the caller's to
    /// destroy with <see cref="DestroySprite"/>.
    /// </summary>
    internal static class HudSprites
    {
        private const int Size = 64;
        private const int CapsuleWidth = 96;
        private const int CapsuleHeight = 64;
        private const float SpritePixelsPerUnit = 100f;

        private delegate Color Painter(Vector2 point, float pixelSize);

        /// <summary>A cow's face: white with a dark patch, a pink muzzle, small ears and horns.</summary>
        public static Sprite CreateCow()
        {
            return Build(PaintCow);
        }

        /// <summary>A pale crescent moon.</summary>
        public static Sprite CreateMoon()
        {
            return Build(PaintMoon);
        }

        /// <summary>A gold sun with eight short rays.</summary>
        public static Sprite CreateSun()
        {
            return Build(PaintSun);
        }

        /// <summary>
        /// A white bar with fully round ends, for a rounded bar of any length. It is cut into three parts
        /// (the two ends and a stretchable middle), so the Image using it must be of type Sliced and have
        /// <see cref="CapsuleScaleMultiplier"/> as its pixels per unit multiplier, which keeps the ends round.
        /// </summary>
        public static Sprite CreateCapsule()
        {
            Texture2D texture = new Texture2D(CapsuleWidth, CapsuleHeight, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.DontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            // A line along the middle from the end of one cap to the start of the other; a pixel is covered by
            // how close it is to that line, within the cap's radius.
            float radius = CapsuleHeight * 0.5f;
            Color[] pixels = new Color[CapsuleWidth * CapsuleHeight];
            for (int y = 0; y < CapsuleHeight; y++)
            {
                for (int x = 0; x < CapsuleWidth; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    float nearestX = Mathf.Clamp(point.x, radius, CapsuleWidth - radius);
                    float distance = Vector2.Distance(point, new Vector2(nearestX, radius)) - radius;
                    pixels[y * CapsuleWidth + x] = new Color(1f, 1f, 1f, Cover(distance, 1f));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

            Vector4 border = new Vector4(radius, 0f, radius, 0f);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, CapsuleWidth, CapsuleHeight), new Vector2(0.5f, 0.5f),
                SpritePixelsPerUnit, 0u, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>
        /// The pixels per unit multiplier that makes the ends of the capsule round for a bar of a given height:
        /// each end is then as wide as it is tall.
        /// </summary>
        /// <param name="barHeight">The bar's height, in canvas pixels.</param>
        /// <param name="referencePixelsPerUnit">The Canvas's reference pixels per unit (100 by default).</param>
        public static float CapsuleScaleMultiplier(float barHeight, float referencePixelsPerUnit)
        {
            float capWidth = Mathf.Max(0.01f, barHeight * 0.5f);
            return CapsuleHeight * 0.5f * referencePixelsPerUnit / (SpritePixelsPerUnit * capWidth);
        }

        /// <summary>Destroys a sprite made here, and its texture.</summary>
        public static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            Texture2D texture = sprite.texture;
            Object.Destroy(sprite);
            if (texture != null)
            {
                Object.Destroy(texture);
            }
        }

        /// <summary>
        /// Makes a UI Image under a parent, with the anchors and pivot given, that ignores clicks. The sprite may be
        /// null for a plain colored rectangle.
        /// </summary>
        public static Image CreateImage(RectTransform parent, string name, Sprite sprite, Vector2 size, Vector2 anchor, Vector2 pivot)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.hideFlags = HideFlags.DontSave;

            RectTransform rect = (RectTransform)imageObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite Build(Painter paint)
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.DontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            // The point is in the range -1 to 1 across the icon, with y up; a pixel is this wide in that range.
            float pixelSize = 2f / Size;
            Color[] pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Vector2 point = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                    pixels[y * Size + x] = paint(point, pixelSize);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), SpritePixelsPerUnit);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static Color PaintCow(Vector2 p, float pixel)
        {
            Color dark = new Color(0.15f, 0.12f, 0.18f, 1f);
            Color white = Color.white;
            Color tan = new Color(0.92f, 0.80f, 0.55f, 1f);
            Color pink = new Color(1f, 0.64f, 0.74f, 1f);
            Color deepPink = new Color(0.78f, 0.32f, 0.42f, 1f);

            // Transparent, but in the color of the outline, so the soft edge does not show a pale fringe.
            Color c = new Color(dark.r, dark.g, dark.b, 0f);

            // Horns and ears behind the head.
            for (int side = -1; side <= 1; side += 2)
            {
                c = Over(c, tan, Cover(Ellipse(p, new Vector2(0.42f * side, 0.82f), new Vector2(0.11f, 0.18f)), pixel));
                c = Over(c, dark, Cover(Ellipse(p, new Vector2(0.70f * side, 0.40f), new Vector2(0.27f, 0.19f)), pixel));
                c = Over(c, white, Cover(Ellipse(p, new Vector2(0.70f * side, 0.40f), new Vector2(0.22f, 0.14f)), pixel));
                c = Over(c, pink, Cover(Ellipse(p, new Vector2(0.70f * side, 0.40f), new Vector2(0.12f, 0.07f)), pixel));
            }

            // The head, with an outline.
            Vector2 headCenter = new Vector2(0f, 0.08f);
            c = Over(c, dark, Cover(Ellipse(p, headCenter, new Vector2(0.70f, 0.78f)), pixel));
            float head = Cover(Ellipse(p, headCenter, new Vector2(0.65f, 0.73f)), pixel);
            c = Over(c, white, head);

            // A dark patch over one eye, kept inside the head.
            c = Over(c, dark, Cover(Ellipse(p, new Vector2(-0.30f, 0.45f), new Vector2(0.27f, 0.22f)), pixel) * head);

            // Muzzle and nostrils.
            c = Over(c, pink, Cover(Ellipse(p, new Vector2(0f, -0.36f), new Vector2(0.42f, 0.27f)), pixel));
            c = Over(c, deepPink, Cover(Ellipse(p, new Vector2(-0.15f, -0.36f), new Vector2(0.06f, 0.08f)), pixel));
            c = Over(c, deepPink, Cover(Ellipse(p, new Vector2(0.15f, -0.36f), new Vector2(0.06f, 0.08f)), pixel));

            // Eyes.
            c = Over(c, white, Cover(Ellipse(p, new Vector2(-0.28f, 0.24f), new Vector2(0.10f, 0.10f)), pixel));
            c = Over(c, dark, Cover(Ellipse(p, new Vector2(-0.28f, 0.24f), new Vector2(0.065f, 0.065f)), pixel));
            c = Over(c, dark, Cover(Ellipse(p, new Vector2(0.28f, 0.24f), new Vector2(0.075f, 0.075f)), pixel));
            return c;
        }

        private static Color PaintMoon(Vector2 p, float pixel)
        {
            Color pale = new Color(0.88f, 0.91f, 1f, 1f);
            Color c = new Color(pale.r, pale.g, pale.b, 0f);

            // A disc with a bite taken out of it by a second disc.
            float disc = Cover(Ellipse(p, Vector2.zero, new Vector2(0.80f, 0.80f)), pixel);
            float bite = Cover(Ellipse(p, new Vector2(0.36f, 0.24f), new Vector2(0.68f, 0.68f)), pixel);
            return Over(c, pale, disc * (1f - bite));
        }

        private static Color PaintSun(Vector2 p, float pixel)
        {
            Color gold = new Color(1f, 0.83f, 0.30f, 1f);
            Color c = new Color(gold.r, gold.g, gold.b, 0f);

            c = Over(c, gold, Cover(Ellipse(p, Vector2.zero, new Vector2(0.40f, 0.40f)), pixel));

            // Eight rays: short bars along the directions at multiples of 45 degrees.
            float radius = p.magnitude;
            float sector = 2f * Mathf.PI / 8f;
            float angle = Mathf.Atan2(p.y, p.x);
            float fromRay = Mathf.Abs(Mathf.Repeat(angle + sector * 0.5f, sector) - sector * 0.5f);
            float across = fromRay * radius;
            float ray = Cover(across - 0.06f, pixel) * Cover(Mathf.Abs(radius - 0.70f) - 0.17f, pixel);
            return Over(c, gold, ray);
        }

        // How much of a pixel is inside a shape, from its signed distance (negative inside).
        private static float Cover(float distance, float pixel)
        {
            return Mathf.Clamp01(0.5f - distance / pixel);
        }

        // The distance to an ellipse's edge, roughly; exact enough for soft edges at this size.
        private static float Ellipse(Vector2 point, Vector2 center, Vector2 radii)
        {
            Vector2 scaled = new Vector2((point.x - center.x) / radii.x, (point.y - center.y) / radii.y);
            return (scaled.magnitude - 1f) * Mathf.Min(radii.x, radii.y);
        }

        // Lays a color over another by the usual "over" rule, for colors that are not premultiplied.
        private static Color Over(Color under, Color over, float coverage)
        {
            float overAlpha = over.a * coverage;
            float alpha = overAlpha + under.a * (1f - overAlpha);
            if (alpha <= 0.0001f)
            {
                return under;
            }

            float underWeight = under.a * (1f - overAlpha);
            return new Color(
                (over.r * overAlpha + under.r * underWeight) / alpha,
                (over.g * overAlpha + under.g * underWeight) / alpha,
                (over.b * overAlpha + under.b * underWeight) / alpha,
                alpha);
        }
    }
}
