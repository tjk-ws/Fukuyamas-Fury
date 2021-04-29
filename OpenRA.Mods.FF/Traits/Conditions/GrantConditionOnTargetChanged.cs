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

using System.Collections.Generic;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public class GrantConditionOnTargetChangedInfo : PausableConditionalTraitInfo
	{
		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("The condition type to grant.")]
		public readonly string Condition = null;

		[Desc("Name of the armaments that grant this condition.")]
		public readonly HashSet<string> ArmamentNames = new HashSet<string>() { "primary" };

		[Desc("Amount of ticks required to pass without firing to revoke an instance.")]
		public readonly int Duration = 15;

		[Desc("Show a selection bar while condition is applied if it has a duration.")]
		public readonly bool ShowSelectionBar = true;
		public readonly Color SelectionBarColor = Color.Magenta;

		public override object Create(ActorInitializer init) { return new GrantConditionOnTargetChanged(init, this); }
	}

	public class GrantConditionOnTargetChanged : PausableConditionalTrait<GrantConditionOnTargetChangedInfo>, INotifyCreated, ITick, INotifyAttack, ISync, ISelectionBar
	{
		int conditionToken = Actor.InvalidConditionToken;

		[Sync]
		int ticks;

		Target lastTarget = Target.Invalid;

		public GrantConditionOnTargetChanged(ActorInitializer init, GrantConditionOnTargetChangedInfo info)
			: base(info) { }

		void ITick.Tick(Actor self)
		{
			if (Info.Duration >= 0 && conditionToken != Actor.InvalidConditionToken && --ticks < 0)
				conditionToken = self.RevokeCondition(conditionToken);
		}

		bool TargetChanged(in Target lastTarget, in Target target)
		{
			// Invalidate reveal changing the target.
			if (lastTarget.Type == TargetType.FrozenActor && target.Type == TargetType.Actor)
				if (lastTarget.FrozenActor.Actor == target.Actor)
					return false;

			if (lastTarget.Type == TargetType.Actor && target.Type == TargetType.FrozenActor)
				if (target.FrozenActor.Actor == lastTarget.Actor)
					return false;

			if (lastTarget.Type != target.Type)
				return true;

			// Invalidate attacking different targets with shared target types.
			if (lastTarget.Type == TargetType.Actor && target.Type == TargetType.Actor)
				if (lastTarget.Actor != target.Actor)
					return true;

			if (lastTarget.Type == TargetType.FrozenActor && target.Type == TargetType.FrozenActor)
				if (lastTarget.FrozenActor != target.FrozenActor)
					return true;

			if (lastTarget.Type == TargetType.Terrain && target.Type == TargetType.Terrain)
				if (lastTarget.CenterPosition != target.CenterPosition)
					return true;

			return false;
		}

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			if (IsTraitDisabled || IsTraitPaused)
				return;

			if (!Info.ArmamentNames.Contains(a.Info.Name))
				return;

			if (TargetChanged(lastTarget, target))
				conditionToken = self.GrantCondition(Info.Condition);

				lastTarget = target;

			ticks = Info.Duration;
		}

		float ISelectionBar.GetValue()
		{
			if (!Info.ShowSelectionBar || Info.Duration < 0 || conditionToken == Actor.InvalidConditionToken)
				return 0;

			return (float)ticks / Info.Duration;
		}

		Color ISelectionBar.GetColor() { return Info.SelectionBarColor; }
		bool ISelectionBar.DisplayWhenEmpty => false;

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel) { }

		protected override void TraitDisabled(Actor self)
		{
			conditionToken = self.RevokeCondition(conditionToken);
		}
	}
}
