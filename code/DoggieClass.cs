using Sandbox;
using System.Linq;
using TerrorTown;
using TTT_Classes;

namespace SmartMario1Classes
{
    public class DoggieClass: TTT_Class
    {
		public override string Name { get; set; } = "Doggie";
		public override string Description { get; set; } = "Get along little doggie! You have a dog that will avenge you should the worst come to pass!";
		public override float Frequency { get; set; } = 1f;
		public override Color Color { get; set; } = Color.FromRgb( 0xf7c472 );

		//Run on start
		public override void RoundStartAbility()
		{
			var dog = new DogEntity();
			dog.Position = Entity.Position + new Vector3( 10, 10, 0 );
			dog.SetOwner( Entity );
		}
	}

	public class DogEntity: AnimatedEntity
	{
		public TerrorTown.Player DogOwner { get; set; }

		public TerrorTown.Player OwnerKiller { get; set; }

		public RealTimeSince LastAttack { get; set; }
		public RealTimeSince LastSound { get; set; }

		public float SoundCooldown { get; set; }

		protected string State;
		protected Vector3[] Path;
		protected int CurrentPathSegment;
		protected TimeSince TimeSinceGeneratedPath = 0;

		const float MOVEMENT_SPEED = 1.85f;
		const float ATTACK_RANGE = 50f;
		const float ATTACK_COOLDOWN = 2.0f;
		const float ATTACK_DAMAGE = 25f;

		[GameEvent.Tick.Server]
		private void Tick()
		{
			switch ( State )
			{
				case "follow_owner":
					PerformStateFollowOwner();
					break;
				case "attacking_player":
					PerformStateAttackingPlayer();
					break;
				case "crying":
					PerformStateCrying();
					break;
				default:
					Log.Error( "Invalid dog state" );
					break;
			}
		}

		protected void PerformStateCrying()
		{
			if ( DogOwner == null ) { return; }
			var crp = DogOwner.Corpse;
			if ( crp == null ) { DogPlaySound(); return; }
			if ( crp.Position.Distance( Position ) <= 50 )
			{
				Vector3 diff = (crp.Position - Position);
				Vector3 normdiff = diff.Normal;
				Rotation = Rotation.LookAt( normdiff );
				PerformGroundCheck();
				DogPlaySound();
				return;
			}
			if ( TimeSinceGeneratedPath >= 0.25 )
			{
				GeneratePath( crp );
			}

			TraversePath( crp );
		}

		protected void PerformStateFollowOwner()
		{
			if ( DogOwner == null ) { return; }		
			if ( DogOwner.LifeState != LifeState.Alive) { Log.Error( "Dog missed owner death" ); State = "crying"; return; }
			if ( DogOwner.Position.Distance( Position ) <= 50 )
			{
				Vector3 diff = (DogOwner.Position - Position);
				Vector3 normdiff = diff.Normal;
				Rotation = Rotation.LookAt( normdiff );
				PerformGroundCheck();
				DogPlaySound();
				return;
			}
			if ( TimeSinceGeneratedPath >= 0.25 )
			{
				GeneratePath( DogOwner );
			}

			DogPlaySound();
			TraversePath(DogOwner);
		}
		protected void PerformStateAttackingPlayer()
		{
			if ( OwnerKiller == null ) { Log.Error( "Mismanaged dog state!" ); return; }
			if ( OwnerKiller.Position.Distance( Position ) <= ATTACK_RANGE )
			{
				if ( LastAttack > ATTACK_COOLDOWN )
				{
					PlaySound( "classdogattack" );
					OwnerKiller.TakeDamage( new DamageInfo { Damage = ATTACK_DAMAGE } );
					LastAttack = 0;
				}
			}
			if ( OwnerKiller.Position.Distance( Position ) <= 20 )
			{
				Vector3 diff = (OwnerKiller.Position - Position);
				Vector3 normdiff = diff.Normal;
				Rotation = Rotation.LookAt( normdiff );
				PerformGroundCheck();
				return;
			}
			if ( TimeSinceGeneratedPath >= 0.25 )
			{
				GeneratePath( OwnerKiller );
			}

			DogPlaySound();
			TraversePath( OwnerKiller );
		}

		protected void DogPlaySound()
		{
			if ( LastSound < SoundCooldown ) return;
			LastSound = 0;
			SoundCooldown = 5f + Game.Random.Int( 5 );
			switch ( State )
			{
				case "follow_owner":
					PlaySound( "classhappydog" );
					break;
				case "attacking_player":
					PlaySound( "classangrydog" );
					SoundCooldown += 1.3f;
					break;
				case "crying":
					PlaySound( "classsaddog" );
					break;
				default:
					Log.Error( "Invalid dog state" );
					break;
			}
		}

		protected void PerformGroundCheck()
		{
			var groundTrace = Trace.Ray( Position + Vector3.Up * 30f, Position + 2f * Vector3.Down );
			groundTrace.StaticOnly();
			TraceResult tr = groundTrace.Run();
			if (tr.Hit)
			{
				if (tr.Entity.IsWorld)
				{
					Position = tr.EndPosition;
				}
			}
		}


		protected void GeneratePath( ModelEntity target )
		{
			TimeSinceGeneratedPath = 0;

			if (!NavMesh.IsLoaded)
			{
				//Log.Info( "No navmesh!" );
				return;
			}

			Path = NavMesh.PathBuilder( Position )
				.WithMaxClimbDistance( 16f )
				.WithMaxDropDistance( 16f )
				.WithStepHeight( 16f )
				.WithMaxDistance( 99999999 )
				.WithPartialPaths()
				.Build( target.Position )
				?.Segments
				?.Select( x => x.Position )
				?.ToArray();

			CurrentPathSegment = 0;
		}

		protected void TraversePath( ModelEntity target )
		{
			// No navmesh on this map.
			if ( Path == null )
			{
				Vector3 diff = (target.Position - Position);
				Vector3 normdiff = diff.Normal;
				Rotation = Rotation.LookAt( normdiff );
				if ( diff.Length >= 800 && target is TerrorTown.Player tmpply && tmpply == DogOwner)
				{
					Position = target.Position;
				}
				if ( diff.Length >= 50 )
				{
					Position += normdiff * MOVEMENT_SPEED;
				}
				PerformGroundCheck();
				return;
			}
				
			// Yes navmesh on this map.
			var distanceToTravel = MOVEMENT_SPEED;

			while ( distanceToTravel > 0 )
			{
				var currentTarget = Path[CurrentPathSegment];
				var distanceToCurrentTarget = Position.Distance( currentTarget );

				if ( distanceToCurrentTarget > distanceToTravel )
				{
					var direction = (currentTarget - Position).Normal;
					Position += direction * distanceToTravel;
					return;
				}
				else
				{
					var direction = (currentTarget - Position).Normal;
					Rotation = Rotation.LookAt( direction );
					Position += direction * distanceToCurrentTarget;
					distanceToTravel -= distanceToCurrentTarget;
					CurrentPathSegment++;
				}

				if ( CurrentPathSegment == Path.Count() )
				{
					Path = null;
					return;
				}
			}
		}

		public override void Spawn()
		{
			base.Spawn();
			// CC Attribution (https://skfb.ly/6WxVW) Shiba by zixisun02.
			SetModel( "models/dog.vmdl" );
			State = "follow_owner";
			LastSound = 0f;
			SoundCooldown = 5f + Game.Random.Int( 5 );
		}

		public void SetOwner(TerrorTown.Player ply)
		{
			DogOwner = ply;
		}

		[Event("Player.PostOnKilled")]
		public void UpdateAttack( DamageInfo LastDamage, TerrorTown.Player ply ) 
		{
			if (ply == DogOwner && State == "follow_owner")
			{
				Log.Info( "My owner " + DogOwner.Owner.Name + " died! :(" );
				var attacker = LastDamage.Attacker;
				if (attacker is TerrorTown.Player attackply && DogOwner != attackply)
				{
					OwnerKiller = attackply;
					SetMaterialOverride( "materials/angrydog.vmat" );
					State = "attacking_player";
					LastAttack = 0;
				}
				else
				{
					SetMaterialOverride( "materials/saddog.vmat" );
					State = "crying";
				}
				return;
			}
			if (ply == OwnerKiller && State == "attacking_player")
			{
				SetMaterialOverride( "materials/saddog.vmat" );
				State = "crying";
				return;
			}
		}
	}
}
