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
using Eluant;
using OpenRA.Mods.RA2.Traits;
using OpenRA.Scripting;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Scripting
{
	[ScriptPropertyGroup("EnergyPool")]
	public class EnergyPoolProperties : ScriptActorProperties, Requires<EnergyPoolInfo>
	{
		readonly Actor self;
		readonly EnergyPool[] ammoPools;

		public EnergyPoolProperties(ScriptContext context, Actor self)
			: base(context, self)
		{
			this.self = self;
			ammoPools = self.TraitsImplementing<EnergyPool>().ToArray();
		}

		[Desc("Returns the count of the actor's specified ammopool.")]
		public int AmmoCount(string poolName = "primary")
		{
			var pool = ammoPools.FirstOrDefault(a => a.Info.Name == poolName);
			if (pool == null)
				throw new LuaException("Invalid ammopool name {0} queried on actor {1}.".F(poolName, self));

			return pool.CurrentAmmoCount;
		}

		[Desc("Returns the maximum count of ammo the actor can load.")]
		public int MaximumAmmoCount(string poolName = "primary")
		{
			var pool = ammoPools.FirstOrDefault(a => a.Info.Name == poolName);
			if (pool == null)
				throw new LuaException("Invalid ammopool name {0} queried on actor {1}.".F(poolName, self));

			return pool.Info.Ammo;
		}

		[Desc("Adds the specified amount of ammo to the specified ammopool.",
			"(Use a negative amount to remove ammo.)")]
		public void Reload(string poolName = "primary", int amount = 1)
		{
			var pool = ammoPools.FirstOrDefault(a => a.Info.Name == poolName);
			if (pool == null)
				throw new LuaException("Invalid ammopool name {0} queried on actor {1}.".F(poolName, self));

			if (amount > 0)
				pool.GiveAmmo(self, amount);
			else
				pool.TakeAmmo(self, -amount);
		}
	}
}
