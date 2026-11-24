using System;

using GameProgrammingI_FinalProject.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System.Collections.Generic;

namespace GameProgrammingI_FinalProject
{
    /// <summary>
    /// The military camp in the middle of the map: the player's spawn, the helicopter landing pad and,
    /// later, the evac zone survivors are led to. Purely decorative for now. GameWorld owns and draws it.
    /// </summary>
    public class Camp
    {
        private readonly List<Rectangle> keepOut = new List<Rectangle>();

        public const int Size = 640;
        private const float GateWidth = 130f;

        public Rectangle Zone { get; private set; }
        public Vector2 Center => new Vector2(this.Zone.Center.X, this.Zone.Center.Y);

        public Camp(Point center)
        {
            this.Zone = new Rectangle(center.X - (Size / 2), center.Y - (Size / 2), Size, Size);

            // where the tents and crate piles are drawn (same positions as in Draw), so survivors don't stand on them
            int cx = center.X;
            int cy = center.Y;

            this.keepOut.Add(new Rectangle((cx - 270), (cy - 255), 140, 90));
            this.keepOut.Add(new Rectangle((cx + 130), (cy - 255), 140, 90));
            this.keepOut.Add(new Rectangle((cx - 270), (cy + 165), 140, 90));
            this.keepOut.Add(new Rectangle((cx + 130), (cy + 165), 140, 90));
            this.keepOut.Add(new Rectangle((cx + 205), (cy - 60),  56,  54));
            this.keepOut.Add(new Rectangle((cx - 250), (cy + 30),  26,  56));
        }

        public bool Contains(Vector2 point)
        {
            return this.Zone.Contains(point.ToPoint());
        }

        public void Draw(SpriteBatch sb, Texture2D pixel, Texture2D disc)
        {
            Vector2 c = this.Center;
            int cx = (int)c.X;
            int cy = (int)c.Y;

            // trampled dirt
            Shapes.DrawRect(sb, pixel, this.Zone, new Color(88, 80, 60));
            Rectangle ground = this.Zone;
            ground.Inflate(-10, -10);
            Shapes.DrawRect(sb, pixel, ground, new Color(102, 93, 70));

            // helipad: painted ring, concrete, then the H
            DrawDisc(sb, disc, c, 252f, new Color(235, 235, 235));
            DrawDisc(sb, disc, c, 236f, new Color(74, 78, 84));
            Color paint = new Color(235, 235, 235);
            Shapes.DrawRect(sb, pixel, new Rectangle(cx - 42, cy - 55, 16, 110), paint);
            Shapes.DrawRect(sb, pixel, new Rectangle(cx + 26, cy - 55, 16, 110), paint);
            Shapes.DrawRect(sb, pixel, new Rectangle(cx - 34, cy - 8, 68, 16), paint);

            // tents in the four corners (top-right is the medical tent)
            Color canvas = new Color(78, 96, 52);
            Color canvasEdge = new Color(48, 60, 32);
            DrawTent(sb, pixel, new Rectangle(cx - 270, cy - 255, 140, 90), canvas, canvasEdge, false);
            DrawTent(sb, pixel, new Rectangle(cx + 130, cy - 255, 140, 90), new Color(205, 210, 205), new Color(140, 145, 140), true);
            DrawTent(sb, pixel, new Rectangle(cx - 270, cy + 165, 140, 90), canvas, canvasEdge, false);
            DrawTent(sb, pixel, new Rectangle(cx + 130, cy + 165, 140, 90), canvas, canvasEdge, false);

            // supply crates
            DrawCrate(sb, pixel, cx + 205, cy - 60);
            DrawCrate(sb, pixel, cx + 235, cy - 60);
            DrawCrate(sb, pixel, cx + 220, cy - 32);
            DrawCrate(sb, pixel, cx - 250, cy + 30);
            DrawCrate(sb, pixel, cx - 250, cy + 60);

            // chain-link fence with a gate in the middle of each side
            Rectangle fence = this.Zone;
            fence.Inflate(-16, -16);
            Vector2 topLeft = new Vector2(fence.Left, fence.Top);
            Vector2 topRight = new Vector2(fence.Right, fence.Top);
            Vector2 bottomRight = new Vector2(fence.Right, fence.Bottom);
            Vector2 bottomLeft = new Vector2(fence.Left, fence.Bottom);
            DrawFence(sb, pixel, topLeft, topRight);
            DrawFence(sb, pixel, topRight, bottomRight);
            DrawFence(sb, pixel, bottomRight, bottomLeft);
            DrawFence(sb, pixel, bottomLeft, topLeft);

            // sandbags flanking each gate
            DrawSandbags(sb, pixel, new Rectangle(cx - 125, fence.Top + 18, 50, 16));
            DrawSandbags(sb, pixel, new Rectangle(cx + 75, fence.Top + 18, 50, 16));
            DrawSandbags(sb, pixel, new Rectangle(cx - 125, fence.Bottom - 34, 50, 16));
            DrawSandbags(sb, pixel, new Rectangle(cx + 75, fence.Bottom - 34, 50, 16));
            DrawSandbags(sb, pixel, new Rectangle(fence.Left + 18, cy - 125, 16, 50));
            DrawSandbags(sb, pixel, new Rectangle(fence.Left + 18, cy + 75, 16, 50));
            DrawSandbags(sb, pixel, new Rectangle(fence.Right - 34, cy - 125, 16, 50));
            DrawSandbags(sb, pixel, new Rectangle(fence.Right - 34, cy + 75, 16, 50));

            // floodlights at the corners
            DrawFloodlight(sb, disc, topLeft + new Vector2(24f, 24f));
            DrawFloodlight(sb, disc, topRight + new Vector2(-24f, 24f));
            DrawFloodlight(sb, disc, bottomRight + new Vector2(-24f, -24f));
            DrawFloodlight(sb, disc, bottomLeft + new Vector2(24f, -24f));
        }

        // drawing helpers
        private static void DrawDisc(SpriteBatch sb, Texture2D disc, Vector2 center, float diameter, Color color)
        {
            Vector2 origin = new Vector2(Shapes.CircleSize / 2f, Shapes.CircleSize / 2f);
            sb.Draw(disc, center, null, color, 0f, origin, diameter / Shapes.CircleSize, SpriteEffects.None, 0f);
        }

        private static void DrawTent(SpriteBatch sb, Texture2D pixel, Rectangle tent, Color body, Color edge, bool medical)
        {
            Shapes.DrawRect(sb, pixel, tent, edge);
            Rectangle fill = tent;
            fill.Inflate(-3, -3);
            Shapes.DrawRect(sb, pixel, fill, body);

            // ridge line along the length
            Shapes.DrawRect(sb, pixel, new Rectangle(tent.X + 3, tent.Center.Y - 1, tent.Width - 6, 3), edge);

            if (medical)
            {
                Color red = new Color(190, 40, 40);
                Shapes.DrawRect(sb, pixel, new Rectangle(tent.Center.X - 4, tent.Center.Y - 16, 8, 32), red);
                Shapes.DrawRect(sb, pixel, new Rectangle(tent.Center.X - 16, tent.Center.Y - 4, 32, 8), red);
            }
            else
            {
                Shapes.DrawRect(sb, pixel, new Rectangle(tent.Center.X - 12, tent.Bottom - 16, 24, 13), edge);   // door flap
            }
        }

        private static void DrawCrate(SpriteBatch sb, Texture2D pixel, int x, int y)
        {
            Shapes.DrawRect(sb, pixel, new Rectangle(x, y, 26, 26), new Color(86, 60, 34));
            Shapes.DrawRect(sb, pixel, new Rectangle(x + 3, y + 3, 20, 20), new Color(128, 92, 52));
        }

        private static void DrawSandbags(SpriteBatch sb, Texture2D pixel, Rectangle area)
        {
            Shapes.DrawRect(sb, pixel, area, new Color(178, 152, 102));

            // stitch lines so it reads as a row of bags
            Color seam = new Color(130, 108, 70);
            if (area.Width >= area.Height)
            {
                for (int x = area.X + 12; x < area.Right - 4; x += 12)
                {
                    Shapes.DrawRect(sb, pixel, new Rectangle(x, area.Y, 1, area.Height), seam);
                }
            }
            else
            {
                for (int y = area.Y + 12; y < area.Bottom - 4; y += 12)
                {
                    Shapes.DrawRect(sb, pixel, new Rectangle(area.X, y, area.Width, 1), seam);
                }
            }
        }

        private static void DrawFence(SpriteBatch sb, Texture2D pixel, Vector2 a, Vector2 b)
        {
            Vector2 edge = (b - a);
            float length = edge.Length();
            Vector2 direction = (edge / length);
            float half = ((length - GateWidth) / 2f);

            // two pieces with the gate gap between them
            DrawFenceSegment(sb, pixel, a, a + (direction * half));
            DrawFenceSegment(sb, pixel, b - (direction * half), b);
        }

        private static void DrawFenceSegment(SpriteBatch sb, Texture2D pixel, Vector2 a, Vector2 b)
        {
            Shapes.DrawLine(sb, pixel, a, b, new Color(170, 175, 180), 2f);

            Vector2 edge = (b - a);
            float length = edge.Length();
            Vector2 direction = (edge / length);

            for (float distance = 0f; distance < length; distance += 40f)
            {
                DrawPost(sb, pixel, a + (direction * distance));
            }
            DrawPost(sb, pixel, b);
        }

        private static void DrawPost(SpriteBatch sb, Texture2D pixel, Vector2 point)
        {
            Shapes.DrawRect(sb, pixel, new Rectangle((int)point.X - 3, (int)point.Y - 3, 6, 6), new Color(215, 215, 220));
        }

        private static void DrawFloodlight(SpriteBatch sb, Texture2D disc, Vector2 point)
        {
            DrawDisc(sb, disc, point, 130f, new Color(255, 235, 160) * 0.10f);      // soft glow
            DrawDisc(sb, disc, point, 14f, new Color(60, 60, 66));                  // pole
            DrawDisc(sb, disc, point, 7f, new Color(255, 240, 170));                // lamp
        }

        /// <summary>A random spot inside the camp that isn't on the helipad, a tent or a crate.</summary>
        public Vector2 RandomRestPoint(Random random)
        {
            Rectangle area = this.Zone;
            area.Inflate(-90, -90);

            for (int attempt = 0; attempt < 50; attempt++)
            {
                Vector2 point = new Vector2(random.Next(area.Left, area.Right), random.Next(area.Top, area.Bottom));

                if (Vector2.Distance(point, this.Center) < 150f)
                {
                    continue;       // keep the helipad clear
                }

                bool blocked = false;
                foreach (Rectangle rect in this.keepOut)
                {
                    Rectangle padded = rect;
                    padded.Inflate(20, 20);
                    if (padded.Contains(point.ToPoint()))
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked)
                {
                    return point;
                }
            }

            return (this.Center + new Vector2(0f, 120f - (Size / 2f)));
        }
    }
}