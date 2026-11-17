using System;

using Microsoft.Xna.Framework;

namespace GameProgrammingI_FinalProject.Sprites
{
    /// <summary>
    /// A hitscan gun. Firing instantly traces a ray from the shooter's muzzle along the
    /// direction the shooter is currently facing and damages the first enemy it touches.
    /// </summary>
    public class Weapon
    {
        public float Damage { get; private set; }
        public float Range { get; private set; }

        /// <summary>Seconds between shots.</summary>
        public float FireInterval { get; private set; }

        private float cooldown;

        public bool IsReady => (this.cooldown <= 0f);

        public Weapon(float damage, float range, float fireInterval)
        {
            this.Damage = damage;
            this.Range = range;
            this.FireInterval = fireInterval;
            this.cooldown = 0f;
        }

        public void Update(float dt)
        {
            if (this.cooldown > 0f)
            {
                this.cooldown -= dt;
            }
        }

        public bool TryFire(Character shooter)
        {
            if (!this.IsReady || !shooter.IsNormalStatus)
            {
                return false;
            }

            this.cooldown = this.FireInterval;

            Vector2 muzzle = shooter.Muzzle;
            RayHit hit = shooter.World.Raycast(muzzle, shooter.FacingDirection, this.Range, shooter);

            if (hit.DidHit)
            {
                shooter.DealDamage(hit.Character, this.Damage);
            }

            shooter.World.AddTracer(muzzle, hit.Point);
            return true;
        }
    }
}
