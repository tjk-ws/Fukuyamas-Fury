#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[Desc("This actor can be mind controlled by other actors.")]
	public class AddonableInfo : PausableConditionalTraitInfo
	{
		[FieldLoader.Require]
		[Desc("Conditions to grant for each accepted plug type.",
			"Key is the plug type.",
			"Value is the condition that is granted when the plug is enabled.")]
		[GrantedConditionReference]
		public readonly Dictionary<string, string> Conditions = null;

		[Desc("The sound played when the mindcontrol is revoked.")]
		public readonly string[] RevokeControlSounds = { };

		public override object Create(ActorInitializer init) { return new Addonable(init.Self, this); }
	}

	public class Addonable : PausableConditionalTrait<AddonableInfo>, INotifyKilled, INotifyActorDisposing, INotifyOwnerChanged, INotifyTransform
	{
		readonly AddonableInfo info;

		int token = Actor.InvalidConditionToken;

		public Actor Master { get; private set; }

		public Addonable(Actor self, AddonableInfo info)
			: base(info)
		{
			this.info = info;
		}

		public void LinkMaster(Actor self, Actor master)
		{
			UnlinkMaster(self, Master);
			Master = master;

			if (!Info.Conditions.TryGetValue(master.Info.Name, out var condition))
				return;

			if (token == Actor.InvalidConditionToken)
				token = self.GrantCondition(condition);
		}

		public void UnlinkMaster(Actor self, Actor master)
		{
			if (master == null)
				return;

			self.World.AddFrameEndTask(_ =>
				{
					if (master.IsDead || master.Disposed)
						return;

					master.Trait<Addon>().UnlinkSlave(master, self);
				});

			Master = null;

			if (token != Actor.InvalidConditionToken)
				token = self.RevokeCondition(token);
		}

		public void RevokeMindControl(Actor self)
		{
			UnlinkMaster(self, Master);

			if (info.RevokeControlSounds.Any())
				Game.Sound.Play(SoundType.World, info.RevokeControlSounds.Random(self.World.SharedRandom), self.CenterPosition);
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			UnlinkMaster(self, Master);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			UnlinkMaster(self, Master);
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			UnlinkMaster(self, Master);
		}

		void INotifyTransform.BeforeTransform(Actor self)
		{
			UnlinkMaster(self, Master);
		}

		void INotifyTransform.OnTransform(Actor self) { }

		void INotifyTransform.AfterTransform(Actor toActor) { }

		protected override void TraitDisabled(Actor self)
		{
			if (Master != null)
				RevokeMindControl(self);
		}
	}
}
