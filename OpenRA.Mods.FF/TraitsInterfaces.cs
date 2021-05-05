#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using OpenRA.GameRules;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[RequireExplicitImplementation]
	public interface ISmokeParticleInfo
	{
		string Image { get; }
		string[] Sequences { get; }
		string Palette { get; }

		bool IsPlayerPalette { get; }

		int[] Duration { get; }

		WDist[] Speed { get; }

		WDist[] Gravity { get; }

		WeaponInfo Weapon { get; }

		int TurnRate { get; }

		int RandomRate { get; }
	}

	[RequireExplicitImplementation]
	public interface IRemoveInfector
	{
		void RemoveInfector(Actor self, bool kill, AttackInfo e = null);
	}

	//[ RequireExplicitImplementation]
	// public interface IPerProducerProductionTimeModifierInfo : ITraitInfoInterface { int GetProductionTimeModifier(Actor prod, string queue); }
}
