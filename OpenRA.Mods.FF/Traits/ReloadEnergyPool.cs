#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	[Desc("Reloads an ammo pool.")]
	public class ReloadEnergyPoolInfo : PausableConditionalTraitInfo
	{
		[Desc("Reload ammo pool with this name.")]
		public readonly string AmmoPool = "primary";

		[Desc("Reload time in ticks per Count.")]
		public readonly int Delay = 50;

		[Desc("How much ammo is reloaded after Delay.")]
		public readonly int Count = 1;

		[Desc("Whether or not reload timer should be reset when ammo has been fired.")]
		public readonly bool ResetOnFire = false;

		[Desc("Play this sound each time ammo is reloaded.")]
		public readonly string Sound = null;

		public override object Create(ActorInitializer init) { return new ReloadEnergyPool(this); }

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (ai.TraitInfos<EnergyPoolInfo>().Count(ap => ap.Name == AmmoPool) != 1)
				throw new YamlException("ReloadsEnergyPool.EnergyPool requires exactly one EnergyPool with matching Name!");

			base.RulesetLoaded(rules, ai);
		}
	}

	public class ReloadEnergyPool : PausableConditionalTrait<ReloadEnergyPoolInfo>, ITick, INotifyAttack, ISync
	{
		EnergyPool ammoPool;
		IReloadAmmoModifier[] modifiers;

		[Sync]
		int remainingTicks;

		public ReloadEnergyPool(ReloadEnergyPoolInfo info)
			: base(info) { }

		protected override void Created(Actor self)
		{
			ammoPool = self.TraitsImplementing<EnergyPool>().Single(ap => ap.Info.Name == Info.AmmoPool);
			modifiers = self.TraitsImplementing<IReloadAmmoModifier>().ToArray();
			base.Created(self);

			self.World.AddFrameEndTask(w =>
			{
				remainingTicks = Util.ApplyPercentageModifiers(Info.Delay, modifiers.Select(m => m.GetReloadAmmoModifier()));
			});
		}

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			if (Info.ResetOnFire)
				remainingTicks = Util.ApplyPercentageModifiers(Info.Delay, modifiers.Select(m => m.GetReloadAmmoModifier()));
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel) { }

		void ITick.Tick(Actor self)
		{
			if (IsTraitPaused || IsTraitDisabled)
				return;

			Reload(self, Info.Delay, Info.Count, Info.Sound);
		}

		protected virtual void Reload(Actor self, int reloadDelay, int reloadCount, string sound)
		{
			if ((!ammoPool.HasFullAmmo || reloadCount < 0) && --remainingTicks == 0)
			{
				remainingTicks = Util.ApplyPercentageModifiers(reloadDelay, modifiers.Select(m => m.GetReloadAmmoModifier()));
				if (!string.IsNullOrEmpty(sound))
					Game.Sound.PlayToPlayer(SoundType.World, self.Owner, sound, self.CenterPosition);

				ammoPool.GiveAmmo(self, reloadCount);
			}
		}
	}
}
