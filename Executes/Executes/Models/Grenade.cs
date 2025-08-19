using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Executes.Enums;
using Executes.Models.JsonConverters;
using System.Text.Json.Serialization;

namespace Executes.Models
{
    public class Grenade
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public EGrenade Type { get; set; }
        
        [JsonConverter(typeof(VectorJsonConverter))]
        public Vector? Position { get; set; }

        [JsonConverter(typeof(QAngleJsonConverter))]
        public QAngle? Angle { get; set; }

        [JsonConverter(typeof(VectorJsonConverter))]
        public Vector? Velocity { get;  set; }

        public CsTeam Team { get; set; }

        public float Delay { get; set; }

        public void Throw()
        {
            if (Type == EGrenade.Smoke)
            {
                SmokeProjectile.Create(Position, Angle, Velocity, Team);
            }
            else
            {
                var entity = Utilities.CreateEntityByName<CBaseCSGrenadeProjectile>(Type.GetProjectileName());

                if (entity == null)
                {
                    Console.WriteLine($"[GrenadeThrownData Fatal] Failed to create entity!");
                    return;
                }

                if (Type == EGrenade.Molotov)
                {
                    entity.SetModel("weapons/models/grenade/incendiary/weapon_incendiarygrenade.vmdl");
                }

                //if (Type == EGrenade.Incendiary)
                //{
                //    // have to set IsIncGrenade after InitializeSpawnFromWorld as it forces it to false
                //    entity.IsIncGrenade = true;
                //    entity.SetModel("weapons/models/grenade/incendiary/weapon_incendiarygrenade.vmdl");
                //}

                entity.Elasticity = 0.33f;
                entity.IsLive = false;
                entity.DmgRadius = 350.0f;
                entity.Damage = 99.0f;
                entity.InitialPosition.X = Position.X;
                entity.InitialPosition.Y = Position.Y;
                entity.InitialPosition.Z = Position.Z;
                entity.InitialVelocity.X = Velocity.X;
                entity.InitialVelocity.Y = Velocity.Y;
                entity.InitialVelocity.Z = Velocity.Z;
                entity.Teleport(Position, Angle, Velocity);
                entity.DispatchSpawn();
                entity.Globalname = "custom";
                entity.AcceptInput("InitializeSpawnFromWorld");
            }
        }

        public override string ToString()
        {
            return $"Type: {Type} Position: {Position} Angle: {Angle} Velocity: {Velocity}Delay: {Delay}";
        }
    }
}
