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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Cnc.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	class RecallPowerInfo : SupportPowerInfo
	{
		[FieldLoader.Require]
		[Desc("Size of the footprint of the affected area.")]
		public readonly CVec Dimensions = CVec.Zero;

		[FieldLoader.Require]
		[Desc("Actual footprint. Cells marked as x will be affected.")]
		public readonly string Footprint = string.Empty;

		[Desc("Ticks until returning after teleportation.")]
		public readonly int Duration = 750;

		[PaletteReference]
		public readonly string TargetOverlayPalette = TileSet.TerrainPaletteInternalName;

		public readonly string FootprintImage = "overlay";

		[SequenceReference(nameof(FootprintImage), prefix: true)]
		public readonly string ValidFootprintSequence = "target-valid";

		[SequenceReference(nameof(FootprintImage))]
		public readonly string InvalidFootprintSequence = "target-invalid";

		[SequenceReference(nameof(FootprintImage))]
		public readonly string SourceFootprintSequence = "target-select";

		public readonly bool KillCargo = true;

		[CursorReference]
		[Desc("Cursor to display when selecting targets for the chronoshift.")]
		public readonly string SelectionCursor = "chrono-select";

		[CursorReference]
		[Desc("Cursor to display when targeting an area for the chronoshift.")]
		public readonly string TargetCursor = "chrono-target";

		[CursorReference]
		[Desc("Cursor to display when the targeted area is blocked.")]
		public readonly string TargetBlockedCursor = "move-blocked";

		// TODO: make this an engine level modification instead
		[Desc("Which limited ammo pool (if present) should this power be assigned to.")]
		public readonly string AmmoPoolName = "";

		[Desc("Ammo the power consumes per use.")]
		public readonly int AmmoUsage = 1;

		public override object Create(ActorInitializer init) { return new RecallPower(init.Self, this); }
	}

	class RecallPower : SupportPower, INotifyCreated
	{
		readonly char[] footprint;
		readonly CVec dimensions;
		AmmoPool ammoPool;

		public RecallPower(Actor self, RecallPowerInfo info)
			: base(self, info)
		{
			footprint = info.Footprint.Where(c => !char.IsWhiteSpace(c)).ToArray();
			dimensions = info.Dimensions;
		}

		protected override void Created(Actor self)
		{
			var info = (RecallPowerInfo)Info;
			ammoPool = self.TraitsImplementing<AmmoPool>().FirstOrDefault(la => la.Info.Name == info.AmmoPoolName);

			base.Created(self);
		}

		public override void SelectTarget(Actor self, string order, SupportPowerManager manager)
		{
			self.World.OrderGenerator = new SelectChronoshiftTarget(Self.World, order, manager, this, self);
		}

		public override void Activate(Actor self, Order order, SupportPowerManager manager)
		{
			base.Activate(self, order, manager);
			PlayLaunchSounds();

			var info = (RecallPowerInfo)Info;
			
			foreach (var target in UnitsInRange(self.World.Map.CellContaining(order.Target.CenterPosition)))
			{
				var cs = target.TraitsImplementing<Chronoshiftable>()
					.FirstEnabledTraitOrDefault();

				if (cs == null)
					continue;

				var targetCell = self.Location + (target.Location - order.ExtraLocation);

				if (self.Owner.Shroud.IsExplored(targetCell) && cs.CanChronoshiftTo(target, targetCell))
					cs.Teleport(target, targetCell, info.Duration, info.KillCargo, self);
			}

			if (ammoPool != null)
				ammoPool.TakeAmmo(self, info.AmmoUsage);
		}

		public IEnumerable<Actor> UnitsInRange(CPos xy)
		{
			var tiles = CellsMatching(xy, footprint, dimensions);
			var units = new HashSet<Actor>();
			foreach (var t in tiles)
				units.UnionWith(Self.World.ActorMap.GetActorsAt(t));

			return units.Where(a => a.TraitsImplementing<Chronoshiftable>().Any(cs => !cs.IsTraitDisabled));
		}

		public bool SimilarTerrain(CPos xy, CPos sourceLocation)
		{
			if (!Self.Owner.Shroud.IsExplored(xy))
				return false;

			var sourceTiles = CellsMatching(xy, footprint, dimensions);
			var destTiles = CellsMatching(sourceLocation, footprint, dimensions);

			if (!sourceTiles.Any() || !destTiles.Any())
				return false;

			using (var se = sourceTiles.GetEnumerator())
			using (var de = destTiles.GetEnumerator())
				while (se.MoveNext() && de.MoveNext())
				{
					var a = se.Current;
					var b = de.Current;

					if (!Self.Owner.Shroud.IsExplored(a) || !Self.Owner.Shroud.IsExplored(b))
						return false;

					if (Self.World.Map.GetTerrainIndex(a) != Self.World.Map.GetTerrainIndex(b))
						return false;
				}

			return true;
		}

		class SelectChronoshiftTarget : OrderGenerator
		{
			readonly RecallPower power;
			readonly char[] footprint;
			readonly CVec dimensions;
			readonly Sprite tile;
			readonly float alpha;
			readonly SupportPowerManager manager;
			readonly string order;
			readonly Actor self;

			public SelectChronoshiftTarget(World world, string order, SupportPowerManager manager, RecallPower power, Actor self)
			{
				// Clear selection if using Left-Click Orders
				if (Game.Settings.Game.UseClassicMouseStyle)
					manager.Self.World.Selection.Clear();

				this.manager = manager;
				this.order = order;
				this.power = power;
				this.self = self;

				var info = (RecallPowerInfo)power.Info;
				var s = world.Map.Rules.Sequences.GetSequence(info.FootprintImage, info.SourceFootprintSequence);
				footprint = info.Footprint.Where(c => !char.IsWhiteSpace(c)).ToArray();
				dimensions = info.Dimensions;
				tile = s.GetSprite(0);
				alpha = s.GetAlpha(0);
			}

			protected override IEnumerable<Order> OrderInner(World world, CPos cell, int2 worldPixel, MouseInput mi)
			{
				world.CancelInputMode();
				if (mi.Button == MouseButton.Left && power.UnitsInRange(cell).Any() && IsValidTarget(cell))
					yield return new Order(order, manager.Self, Target.FromCell(world, cell), false)
					{
						ExtraLocation = cell,
						SuppressVisualFeedback = true
					};
			}

			protected override void Tick(World world)
			{
				// Cancel the OG if we can't use the power
				if (!manager.Powers.TryGetValue(order, out var p) || !p.Active || !p.Ready)
					world.CancelInputMode();
			}

			protected override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world) { yield break; }

			protected override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world)
			{
				var xy = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
				var targetUnits = power.UnitsInRange(xy).Where(a => !world.FogObscures(a));

				foreach (var unit in targetUnits)
				{
					if (unit.CanBeViewedByPlayer(manager.Self.Owner))
					{
						var decorations = unit.TraitsImplementing<ISelectionDecorations>().FirstEnabledTraitOrDefault();
						if (decorations != null)
							foreach (var d in decorations.RenderSelectionAnnotations(unit, wr, Color.Red))
								yield return d;
					}
				}
			}

			protected override IEnumerable<IRenderable> Render(WorldRenderer wr, World world)
			{
				var xy = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
				var tiles = power.CellsMatching(xy, footprint, dimensions);
				var palette = wr.Palette(((RecallPowerInfo)power.Info).TargetOverlayPalette);
				foreach (var t in tiles)
					yield return new SpriteRenderable(tile, wr.World.Map.CenterOfCell(t), WVec.Zero, -511, palette, 1f, alpha, float3.Ones, TintModifiers.IgnoreWorldTint, true);
			}

			bool IsValidTarget(CPos xy)
			{
				// Don't teleport if there are no units in range (either all moved out of range, or none yet moved into range)
				var unitsInRange = power.UnitsInRange(xy);
				if (!unitsInRange.Any())
					return false;

				var canTeleport = false;
				foreach (var unit in unitsInRange)
				{
					var targetCell = self.Location + (unit.Location - xy);
					if (manager.Self.Owner.Shroud.IsExplored(targetCell) && unit.Trait<Chronoshiftable>().CanChronoshiftTo(unit, targetCell))
					{
						canTeleport = true;
						break;
					}
				}

				if (!canTeleport)
				{
					// Check the terrain types. This will allow chronoshifts to occur on empty terrain to terrain of
					// a similar type. This also keeps the cursor from changing in non-visible property, alerting the
					// chronoshifter of enemy unit presence
					canTeleport = power.SimilarTerrain(xy, self.Location);
				}

				return canTeleport;
			}

			protected override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
			{
				var powerInfo = (RecallPowerInfo)power.Info;
				return IsValidTarget(cell) ? powerInfo.TargetCursor : powerInfo.TargetBlockedCursor;
			}
		}
	}
}
