#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GameProgrammingI_MonoGameLibrary;
using GameProgrammingI_MonoGameLibrary.Sprite;
using Microsoft.Xna.Framework;

namespace GameProgrammingI_FinalProject.Sprites
{
    public class Character1 : DrawableSprite
    {
        // static
        public static readonly Character1 Null = new Character1(new Game(), 0, 0);

        public static readonly CharacterStatus NoneStatus = CharacterStatus.None;
        public static readonly CharacterStatus NormalStatus = CharacterStatus.Normal;
        public static readonly CharacterStatus DeadStatus = CharacterStatus.Dead;


        // tags
        public CharacterStatus Status { get; private set; }
        public Team Team { get; private set; }
        public Role Role { get; private set; }


        // on spawn
        public Character1 LastAttacker { get; private set; }
        public float FacingAngle { get; private set; }


        // initalizers
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        
        public float TurnRate { get; private set; }


        // accessors
        public bool IsNull => (object.ReferenceEquals(this, Character1.Null));

        public bool IsInitalized => (this.Status != NoneStatus);

        public bool IsNoneStatus => (this.Status == NoneStatus);
        public bool IsNormalStatus => (this.Status == NormalStatus);
        public bool IsDeadStatus => (this.Status == DeadStatus);

        public bool IsNoneTeam => (this.Team == Team.None);
        public bool IsHumanTeam => (this.Team == Team.Humans);
        public bool IsZombieTeam => (this.Team == Team.Zombies);

        public bool IsNoneRole => (this.Role == Role.None);
        public bool IsPlayerRole => (this.Role == Role.Player);
        public bool IsZombieRole => (this.Role == Role.Zombie);



        public Character1(Game game, short health, float turnRate) : base(game)
        {
            // tags
            this.Status = NoneStatus;
            this.Team = Team.None;
            this.Role = Role.None;


            // on spawn
            this.LastAttacker = Character1.Null;
            this.FacingAngle = 0;


            // initalizers
            this.Health = this.MaxHealth = (float)(health);
            this.TurnRate = turnRate;
        }
        
        public float TakeDamage(float damage, Character1? attacker = null)
        {
            this.LastAttacker = ((attacker is not null) ? (attacker) : (Character1.Null));

            float newHealth = (this.Health - damage);

            if (newHealth <= 0)
            {
                this.Die();
                return (damage + newHealth);
            }

            this.Health = newHealth;
            return damage;
        }

        public float DealDamage(Character1 other, float damage)
        {
            return other.TakeDamage(damage, this);
        }

        public void Die()
        {
            this.Health = 0;

            this.Status = CharacterStatus.Dead;
        }
    }
}
