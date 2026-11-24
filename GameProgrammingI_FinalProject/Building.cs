using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

namespace GameProgrammingI_FinalProject
{
    public enum DoorSide
    {
        Top,
        Bottom,
        Left,
        Right
    }

    /// <summary>
    /// What a building holds. To add a new one (power-ups, ammo...): add a value here,
    /// a line in GameWorld.OutcomeRules, and a case in GameWorld.FinishSearch.
    /// </summary>
    public enum SearchOutcome
    {
        Nothing,
        Survivors,
        Zombies
    }

    /// <summary>
    /// A hollow rectangle of four walls with a boarded-up door in one of them.
    /// Not a component: GameWorld owns, draws and collides with buildings.
    /// </summary>
    public class Building
    {
        public const int WallThickness = 14;
        public const int DoorWidth = 80;

        public Rectangle Bounds { get; private set; }       // the whole footprint, walls included
        public Rectangle Interior { get; private set; }     // the floor inside the walls
        public DoorSide Door { get; private set; }

        /// <summary>
        /// Solid rectangles that block movement and shots. Includes the door while it is closed.
        /// </summary>
        public readonly List<Rectangle> Walls = new List<Rectangle>();

        public Rectangle DoorRect { get; private set; }
        public bool DoorOpen { get; private set; }

        public SearchOutcome Outcome { get; private set; }
        public int GroupSize { get; private set; }          // how many survivors/zombies the outcome holds
        public bool Searched { get; set; }

        public Vector2 Center => new Vector2(this.Interior.Center.X, this.Interior.Center.Y);

        public Building(Rectangle bounds, DoorSide door, SearchOutcome outcome, int groupSize)
        {
            this.Bounds = bounds;
            this.Door = door;
            this.Outcome = outcome;
            this.GroupSize = groupSize;

            int t = WallThickness;
            this.Interior = new Rectangle(bounds.X + t, bounds.Y + t, bounds.Width - (2 * t), bounds.Height - (2 * t));

            // top and bottom span the full width, left and right fit between them
            this.AddHorizontal(bounds.Left, bounds.Top, bounds.Width, (door == DoorSide.Top));
            this.AddHorizontal(bounds.Left, bounds.Bottom - t, bounds.Width, (door == DoorSide.Bottom));
            this.AddVertical(bounds.Left, bounds.Top + t, bounds.Height - (2 * t), (door == DoorSide.Left));
            this.AddVertical(bounds.Right - t, bounds.Top + t, bounds.Height - (2 * t), (door == DoorSide.Right));
        }

        public void OpenDoor()
        {
            if (this.DoorOpen)
            {
                return;
            }

            this.DoorOpen = true;
            this.Walls.Remove(this.DoorRect);
        }

        private void AddHorizontal(int x, int y, int width, bool hasDoor)
        {
            if (!hasDoor)
            {
                this.Walls.Add(new Rectangle(x, y, width, WallThickness));
                return;
            }

            int side = ((width - DoorWidth) / 2);
            this.Walls.Add(new Rectangle(x, y, side, WallThickness));
            this.Walls.Add(new Rectangle(x + width - side, y, side, WallThickness));

            this.DoorRect = new Rectangle(x + side, y, width - (2 * side), WallThickness);
            this.Walls.Add(this.DoorRect);
        }

        private void AddVertical(int x, int y, int height, bool hasDoor)
        {
            if (!hasDoor)
            {
                this.Walls.Add(new Rectangle(x, y, WallThickness, height));
                return;
            }

            int side = ((height - DoorWidth) / 2);
            this.Walls.Add(new Rectangle(x, y, WallThickness, side));
            this.Walls.Add(new Rectangle(x, y + height - side, WallThickness, side));

            this.DoorRect = new Rectangle(x, y + side, WallThickness, height - (2 * side));
            this.Walls.Add(this.DoorRect);
        }
    }
}