﻿#region Copyright & License Information
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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[Desc("This actor can mind control other actors.")]
	public class MindControllerInfo : PausableConditionalTraitInfo, Requires<ArmamentInfo>, Requires<HealthInfo>
	{
		[Desc("Name of the armaments that grant this condition.")]
		public readonly HashSet<string> ArmamentNames = new HashSet<string>() { "primary" };

		[Desc("Up to how many units can this unit control?",
			"Use 0 or negative numbers for infinite.")]
		public readonly int Capacity = 1;

		[Desc("If the capacity is reached, discard the oldest mind controlled unit and control the new one",
			"If false, controlling new units is forbidden after capacity is reached.")]
		public readonly bool DiscardOldest = true;

		[Desc("Condition to grant to self when controlling actors. Can stack up by the number of enslaved actors. You can use this to forbid firing of the dummy MC weapon.")]
		[GrantedConditionReference]
		public readonly string ControllingCondition;

		[Desc("The sound played when the unit is mindcontrolled.")]
		public readonly string[] Sounds = { };

		public override object Create(ActorInitializer init) { return new MindController(init.Self, this); }
	}

	public class MindController : PausableConditionalTrait<MindControllerInfo>, INotifyAttack, INotifyKilled, INotifyActorDisposing, INotifyCreated
	{
		readonly MindControllerInfo info;
		readonly List<Actor> slaves = new List<Actor>();
		readonly Stack<int> controllingTokens = new Stack<int>();

		public IEnumerable<Actor> Slaves { get { return slaves; } }

		public MindController(Actor self, MindControllerInfo info)
			: base(info)
		{
			this.info = info;
		}

		void StackControllingCondition(Actor self, string condition)
		{
			if (string.IsNullOrEmpty(condition))
				return;

			controllingTokens.Push(self.GrantCondition(condition));
		}

		void UnstackControllingCondition(Actor self, string condition)
		{
			if (string.IsNullOrEmpty(condition))
				return;

			self.RevokeCondition(controllingTokens.Pop());
		}

		public void UnlinkSlave(Actor self, Actor slave)
		{
			if (slaves.Contains(slave))
			{
				slaves.Remove(slave);
				UnstackControllingCondition(self, info.ControllingCondition);
			}
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel) { }

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			if (IsTraitDisabled || IsTraitPaused)
				return;

			if (!info.ArmamentNames.Contains(a.Info.Name))
				return;

			if (target.Actor == null || !target.IsValidFor(self))
				return;

			if (self.Owner.RelationshipWith(target.Actor.Owner) == PlayerRelationship.Ally)
				return;

			var mindControllable = target.Actor.TraitOrDefault<MindControllable>();

			if (mindControllable == null)
			{
				throw new InvalidOperationException(
					"`{0}` tried to mindcontrol `{1}`, but the latter does not have the necessary trait!"
					.F(self.Info.Name, target.Actor.Info.Name));
			}

			if (mindControllable.IsTraitDisabled || mindControllable.IsTraitPaused)
				return;

			if (info.Capacity > 0 && !info.DiscardOldest && slaves.Count() >= info.Capacity)
				return;

			slaves.Add(target.Actor);
			StackControllingCondition(self, info.ControllingCondition);
			mindControllable.LinkMaster(target.Actor, self);

			if (info.Sounds.Any())
				Game.Sound.Play(SoundType.World, info.Sounds.Random(self.World.SharedRandom), self.CenterPosition);

			if (info.Capacity > 0 && info.DiscardOldest && slaves.Count() > info.Capacity)
				slaves[0].Trait<MindControllable>().RevokeMindControl(slaves[0]);
		}

		void ReleaseSlaves(Actor self)
		{
			foreach (var s in slaves)
			{
				if (s.IsDead || s.Disposed)
					continue;

				s.Trait<MindControllable>().RevokeMindControl(s);
			}

			slaves.Clear();
			while (controllingTokens.Any())
				UnstackControllingCondition(self, info.ControllingCondition);
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			ReleaseSlaves(self);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			ReleaseSlaves(self);
		}

		protected override void TraitDisabled(Actor self)
		{
			ReleaseSlaves(self);
		}
	}
}
