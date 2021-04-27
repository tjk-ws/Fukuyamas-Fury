﻿#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using OpenRA.GameRules;
using OpenRA.Mods.AS.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[Desc("Spawn smoke particles when this actor is killed.")]
	public class SpawnSmokeParticleOnDeathInfo : ConditionalTraitInfo, ISmokeParticleInfo, IRulesetLoaded
	{
		[Desc("How many particles should spawn.")]
		public readonly int[] Amount = { 1 };

		[Desc("DeathType(s) that trigger spawning. Leave empty to always spawn.")]
		public readonly BitSet<DamageType> DeathTypes = default(BitSet<DamageType>);

		[FieldLoader.Require]
		[Desc("The duration of an individual particle. Two values mean actual lifetime will vary between them.")]
		public readonly int[] Duration;

		[Desc("Offset for the particle emitter.")]
		public readonly WVec[] Offset = { WVec.Zero };

		[Desc("Randomize particle forward movement.")]
		public readonly WDist[] Speed = { WDist.Zero };

		[Desc("Randomize particle gravity.")]
		public readonly WDist[] Gravity = { WDist.Zero };

		[Desc("Randomize particle turnrate.")]
		public readonly int TurnRate = 0;

		[Desc("Rate to reset particle movement properties.")]
		public readonly int RandomRate = 4;

		[Desc("Which image to use.")]
		public readonly string Image = "particles";

		[FieldLoader.Require]
		[Desc("Which sequence to use.")]
		[SequenceReference(nameof(Image))]
		public readonly string[] Sequences = null;

		[Desc("Which palette to use.")]
		[PaletteReference("IsPlayerPalette")]
		public readonly string Palette = null;

		public readonly bool IsPlayerPalette = false;

		[WeaponReference]
		[Desc("Has to be defined in weapons.yaml, if defined, as well.")]
		public readonly string Weapon = null;

		public WeaponInfo WeaponInfo { get; private set; }

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);

			if (string.IsNullOrEmpty(Weapon))
				return;

			WeaponInfo weaponInfo;

			var weaponToLower = Weapon.ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(weaponToLower, out weaponInfo))
				throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(weaponToLower));

			WeaponInfo = weaponInfo;
		}

		public override object Create(ActorInitializer init) { return new SpawnSmokeParticleOnDeath(init.Self, this); }

		string ISmokeParticleInfo.Image
		{
			get { return Image; }
		}

		string[] ISmokeParticleInfo.Sequences
		{
			get { return Sequences; }
		}

		string ISmokeParticleInfo.Palette
		{
			get { return Palette; }
		}

		bool ISmokeParticleInfo.IsPlayerPalette
		{
			get { return IsPlayerPalette; }
		}

		WDist[] ISmokeParticleInfo.Speed
		{
			get { return Speed; }
		}

		WDist[] ISmokeParticleInfo.Gravity
		{
			get { return Gravity; }
		}

		int[] ISmokeParticleInfo.Duration
		{
			get { return Duration; }
		}

		WeaponInfo ISmokeParticleInfo.Weapon
		{
			get { return WeaponInfo; }
		}

		int ISmokeParticleInfo.TurnRate
		{
			get { return TurnRate; }
		}

		int ISmokeParticleInfo.RandomRate
		{
			get { return RandomRate; }
		}
	}

	public class SpawnSmokeParticleOnDeath : ConditionalTrait<SpawnSmokeParticleOnDeathInfo>, INotifyKilled
	{
		public SpawnSmokeParticleOnDeath(Actor self, SpawnSmokeParticleOnDeathInfo info)
			: base(info) { }

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (IsTraitDisabled)
				return;

			if (!Info.DeathTypes.IsEmpty && !e.Damage.DamageTypes.Overlaps(Info.DeathTypes))
				return;

			var random = self.World.SharedRandom;

			var amount = Info.Amount.Length == 2
				? random.Next(Info.Amount[0], Info.Amount[1])
				: Info.Amount[0];

			for (int i = 0; i < amount; i++)
			{
				var offset = Info.Offset.Length == 2
				? new WVec(random.Next(Info.Offset[0].X, Info.Offset[1].X), random.Next(Info.Offset[0].Y, Info.Offset[1].Y), random.Next(Info.Offset[0].Z, Info.Offset[1].Z))
				: Info.Offset[0];

				self.World.AddFrameEndTask(w => w.Add(new SmokeParticle(e.Attacker, Info, self.CenterPosition + offset)));
			}
		}
	}
}
