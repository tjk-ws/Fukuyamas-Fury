#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[Desc("Explodes a weapon at the actor's position when enabled."
		+ "Reload/burstdelays are used as explosion intervals.")]
	public class ExplodeWeaponInfo : ConditionalTraitInfo, IRulesetLoaded
	{
		[WeaponReference]
		[FieldLoader.Require]
		[Desc("Has to be defined in weapons.yaml as well.")]
		public readonly string Weapon = null;

		public readonly bool ResetReloadWhenEnabled = true;

		[Desc("Which limited ammo pool (if present) should this weapon be assigned to.")]
		public readonly string AmmoPoolName = "";

		public WeaponInfo WeaponInfo { get; private set; }

		[Desc("Explosion offset relative to actor's position.")]
		public readonly WVec LocalOffset = WVec.Zero;

		public override object Create(ActorInitializer init) { return new ExplodeWeapon(init.Self, this); }

		[Desc("Ammo the weapon consumes per shot.")]
		public readonly int AmmoUsage = 1;

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			base.RulesetLoaded(rules, ai);

			WeaponInfo weaponInfo;

			var weaponToLower = Weapon.ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(weaponToLower, out weaponInfo))
				throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(weaponToLower));

			WeaponInfo = weaponInfo;
		}
	}

	class ExplodeWeapon : ConditionalTrait<ExplodeWeaponInfo>, ITick, INotifyCreated
	{
		readonly ExplodeWeaponInfo info;
		readonly WeaponInfo weapon;
		readonly BodyOrientation body;

		int fireDelay;
		int burst;
		AmmoPool ammoPool;

		List<(int Delay, Action Action)> delayedActions = new List<(int Delay, Action Action)>();

		public ExplodeWeapon(Actor self, ExplodeWeaponInfo info)
			: base(info)
		{
			this.info = info;

			weapon = info.WeaponInfo;
			burst = weapon.Burst;
			body = self.TraitOrDefault<BodyOrientation>();
		}

		protected override void Created(Actor self)
		{
			ammoPool = self.TraitsImplementing<AmmoPool>().FirstOrDefault(la => la.Info.Name == Info.AmmoPoolName);

			base.Created(self);
		}

		void ITick.Tick(Actor self)
		{
			for (var i = 0; i < delayedActions.Count; i++)
			{
				var x = delayedActions[i];
				if (--x.Delay <= 0)
					x.Action();
				delayedActions[i] = x;
			}

			delayedActions.RemoveAll(a => a.Delay <= 0);

			if (IsTraitDisabled)
				return;

			if (--fireDelay < 0)
			{
				if (ammoPool != null && !ammoPool.TakeAmmo(self, info.AmmoUsage))
					return;

				var localoffset = body != null
					? body.LocalToWorld(info.LocalOffset.Rotate(body.QuantizeOrientation(self, self.Orientation)))
					: info.LocalOffset;

				var args = new WarheadArgs
				{
					Weapon = weapon,
					DamageModifiers = self.TraitsImplementing<IFirepowerModifier>().Select(a => a.GetFirepowerModifier()).ToArray(),
					Source = self.CenterPosition,
					SourceActor = self,
					WeaponTarget = Target.FromPos(self.CenterPosition + localoffset)
				};

				weapon.Impact(Target.FromPos(self.CenterPosition + localoffset), args);

				if (weapon.Report != null && weapon.Report.Any())
					Game.Sound.Play(SoundType.World, weapon.Report.Random(self.World.SharedRandom), self.CenterPosition);

				if (burst == weapon.Burst && weapon.StartBurstReport != null && weapon.StartBurstReport.Any())
					Game.Sound.Play(SoundType.World, weapon.Report.Random(self.World.SharedRandom), self.CenterPosition);

				if (--burst > 0)
				{
					if (weapon.BurstDelays.Length == 1)
						fireDelay = weapon.BurstDelays[0];
					else
						fireDelay = weapon.BurstDelays[weapon.Burst - (burst + 1)];
				}
				else
				{
					var modifiers = self.TraitsImplementing<IReloadModifier>()
						.Select(m => m.GetReloadModifier());
					fireDelay = Util.ApplyPercentageModifiers(weapon.ReloadDelay, modifiers);
					burst = weapon.Burst;

					if (weapon.AfterFireSound != null && weapon.AfterFireSound.Any())
					{
						ScheduleDelayedAction(weapon.AfterFireSoundDelay, () =>
						{
							Game.Sound.Play(SoundType.World, weapon.Report.Random(self.World.SharedRandom), self.CenterPosition);
						});
					}
				}
			}
		}

		protected override void TraitEnabled(Actor self)
		{
			if (info.ResetReloadWhenEnabled)
			{
				burst = weapon.Burst;
				fireDelay = 0;
			}
		}

		protected void ScheduleDelayedAction(int t, Action a)
		{
			if (t > 0)
				delayedActions.Add((t, a));
			else
				a();
		}
	}
}
