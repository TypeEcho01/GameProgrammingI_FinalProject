using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameProgrammingI_FinalProject.Util
{
    /// <summary>
    /// Generates simple placeholder textures in code (circles, a 1x1 pixel) so the prototype
    /// needs no art assets, plus a couple of line/rect drawing helpers.
    /// </summary>
    public static class Shapes
    {
        public const int CircleSize = 64;

        private static Texture2D circle;
        private static Texture2D pixel;

        /// <summary>
        /// A white, anti-aliased circle that fills a CircleSize x CircleSize texture.
        /// Tint it with DrawColor and size it with Scale.
        /// </summary>
        public static Texture2D Circle(GraphicsDevice device)
        {
            if (circle == null || circle.IsDisposed || circle.GraphicsDevice != device)
            {
                Color[] data = new Color[CircleSize * CircleSize];
                float radius = CircleSize / 2f;

                for (int y = 0; y < CircleSize; y++)
                {
                    for (int x = 0; x < CircleSize; x++)
                    {
                        float dx = (x + 0.5f) - radius;
                        float dy = (y + 0.5f) - radius;
                        float distance = (float)Math.Sqrt((dx * dx) + (dy * dy));

                        // 1 inside, fades to 0 over the last pixel. Premultiplied alpha,
                        // which is what SpriteBatch's default blend state expects.
                        float alpha = MathHelper.Clamp(radius - distance, 0f, 1f);
                        data[x + (y * CircleSize)] = new Color(alpha, alpha, alpha, alpha);
                    }
                }

                circle = new Texture2D(device, CircleSize, CircleSize);
                circle.SetData(data);
            }

            return circle;
        }

        /// <summary>
        /// A single white pixel. Stretch it to draw rectangles and lines.
        /// </summary>
        public static Texture2D Pixel(GraphicsDevice device)
        {
            if (pixel == null || pixel.IsDisposed || pixel.GraphicsDevice != device)
            {
                pixel = new Texture2D(device, 1, 1);
                pixel.SetData(new Color[] { Color.White });
            }

            return pixel;
        }

        public static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 edge = (end - start);
            float angle = (float)Math.Atan2(edge.Y, edge.X);

            sb.Draw(pixel, start, null, color, angle,
                new Vector2(0f, 0.5f), new Vector2(edge.Length(), thickness),
                SpriteEffects.None, 0f);
        }

        public static void DrawRect(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color)
        {
            sb.Draw(pixel, rect, color);
        }
    }
}
