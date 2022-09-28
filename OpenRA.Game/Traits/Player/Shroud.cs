#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;

namespace OpenRA.Traits
{
	[TraitLocation(SystemActors.Player | SystemActors.EditorPlayer)]
	[Desc("Required for shroud and fog visibility checks. Add this to the player actor.")]
	public class ShroudInfo : TraitInfo, ILobbyOptions
	{
		[Desc("Descriptive label for the fog checkbox in the lobby.")]
		public readonly string FogCheckboxLabel = "Fog of War";

		[Desc("Tooltip description for the fog checkbox in the lobby.")]
		public readonly string FogCheckboxDescription = "Line of sight is required to view enemy forces";

		[Desc("Default value of the fog checkbox in the lobby.")]
		public readonly bool FogCheckboxEnabled = true;

		[Desc("Prevent the fog enabled state from being changed in the lobby.")]
		public readonly bool FogCheckboxLocked = false;

		[Desc("Whether to display the fog checkbox in the lobby.")]
		public readonly bool FogCheckboxVisible = true;

		[Desc("Display order for the fog checkbox in the lobby.")]
		public readonly int FogCheckboxDisplayOrder = 0;

		[Desc("Descriptive label for the explored map checkbox in the lobby.")]
		public readonly string ExploredMapCheckboxLabel = "Explored Map";

		[Desc("Tooltip description for the explored map checkbox in the lobby.")]
		public readonly string ExploredMapCheckboxDescription = "Initial map shroud is revealed";

		[Desc("Default value of the explore map checkbox in the lobby.")]
		public readonly bool ExploredMapCheckboxEnabled = false;

		[Desc("Prevent the explore map enabled state from being changed in the lobby.")]
		public readonly bool ExploredMapCheckboxLocked = false;

		[Desc("Whether to display the explore map checkbox in the lobby.")]
		public readonly bool ExploredMapCheckboxVisible = true;

		[Desc("Display order for the explore map checkbox in the lobby.")]
		public readonly int ExploredMapCheckboxDisplayOrder = 0;

		IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(MapPreview map)
		{
			yield return new LobbyBooleanOption("explored", ExploredMapCheckboxLabel, ExploredMapCheckboxDescription,
				ExploredMapCheckboxVisible, ExploredMapCheckboxDisplayOrder, ExploredMapCheckboxEnabled, ExploredMapCheckboxLocked);
			yield return new LobbyBooleanOption("fog", FogCheckboxLabel, FogCheckboxDescription,
				FogCheckboxVisible, FogCheckboxDisplayOrder, FogCheckboxEnabled, FogCheckboxLocked);
		}

		public override object Create(ActorInitializer init) { return new Shroud(init.Self, this); }
	}

	public class Shroud : ISync, INotifyCreated, ITick
	{
		public enum SourceType : byte { PassiveVisibility, Shroud, Visibility }
		public event Action<MPos> OnShroudChanged;

		enum ShroudCellType : byte { Shroud, Fog, Visible }
		class ShroudSource
		{
			public readonly SourceType Type;
			public readonly CPos[] ProjectedCells;

			public ShroudSource(SourceType type, CPos[] projectedCells)
			{
				Type = type;
				ProjectedCells = projectedCells;
			}
		}

		// Visible is not a super set of Explored. IsExplored may return false even if IsVisible returns true.
		[Flags]
		public enum CellVisibility : byte { Hidden = 0x0, Explored = 0x1, Visible = 0x2 }

		readonly ShroudInfo info;
		readonly Map map;

		// Individual shroud modifier sources (type and area)
		readonly Dictionary<object, ShroudSource> sources = new Dictionary<object, ShroudSource>();

		// Per-cell count of each source type, used to resolve the final cell type
		readonly CellLayer<short> passiveVisibleCount;
		readonly CellLayer<short> visibleCount;
		readonly CellLayer<short> generatedShroudCount;
		readonly CellLayer<bool> explored;
		readonly CellLayer<bool> touched;
		bool anyCellTouched;

		// Per-cell cache of the resolved cell type (shroud/fog/visible)
		readonly CellLayer<ShroudCellType> resolvedType;

		bool disabledChanged;
		[Sync]
		bool disabled;
		public bool Disabled
		{
			get => disabled;

			set
			{
				if (disabled == value)
					return;

				disabled = value;
				disabledChanged = true;
			}
		}

		bool fogEnabled;
		public bool FogEnabled => !Disabled && fogEnabled;
		public bool ExploreMapEnabled { get; private set; }

		public int Hash { get; private set; }

		// Enabled at runtime on first use
		bool shroudGenerationEnabled;
		bool passiveVisibilityEnabled;

		public Shroud(Actor self, ShroudInfo info)
		{
			this.info = info;
			map = self.World.Map;

			passiveVisibleCount = new CellLayer<short>(map);
			visibleCount = new CellLayer<short>(map);
			generatedShroudCount = new CellLayer<short>(map);
			explored = new CellLayer<bool>(map);
			touched = new CellLayer<bool>(map);
			anyCellTouched = true;

			// Defaults to 0 = Shroud
			resolvedType = new CellLayer<ShroudCellType>(map);
		}

		void INotifyCreated.Created(Actor self)
		{
			var gs = self.World.LobbyInfo.GlobalSettings;
			fogEnabled = gs.OptionOrDefault("fog", info.FogCheckboxEnabled);

			ExploreMapEnabled = gs.OptionOrDefault("explored", info.ExploredMapCheckboxEnabled);
			if (ExploreMapEnabled)
				self.World.AddFrameEndTask(w => ExploreAll());
		}

		void ITick.Tick(Actor self)
		{
			if (!anyCellTouched && !disabledChanged)
				return;

			anyCellTouched = false;

			if (OnShroudChanged == null)
			{
				disabledChanged = false;
				return;
			}

			// PERF: Parts of this loop are very hot.
			// We loop over the direct index that represents the PPos in
			// the ProjectedCellLayers, converting to a PPos only if
			// it is needed (which is the uncommon case.)
			var maxIndex = touched.Size.Width * touched.Size.Height;
			for (var index = 0; index < maxIndex; index++)
			{
				// PERF: Most cells are not touched
				if (!touched[index] && !disabledChanged)
					continue;

				touched[index] = false;

				var type = ShroudCellType.Shroud;

				if (explored[index])
				{
					var count = visibleCount[index];
					if (!shroudGenerationEnabled || count > 0 || generatedShroudCount[index] == 0)
					{
						if (passiveVisibilityEnabled)
							count += passiveVisibleCount[index];

						type = count > 0 ? ShroudCellType.Visible : ShroudCellType.Fog;
					}
				}

				// PERF: Most cells are unchanged
				var oldResolvedType = resolvedType[index];
				if (type != oldResolvedType || disabledChanged)
				{
					resolvedType[index] = type;
					var uv = touched.IndexToMPos(index);
					if (map.Contains(uv))
						OnShroudChanged(uv);
				}
			}

			Hash = Sync.HashPlayer(self.Owner) + self.World.WorldTick;
			disabledChanged = false;
		}

		public static IEnumerable<CPos> ProjectedCellsInRange(Map map, WPos pos, WDist minRange, WDist maxRange, int maxHeightDelta = -1)
		{
			// Account for potential extra half-cell from odd-height terrain
			var r = (maxRange.Length + 1023 + 512) / 1024;
			var minLimit = minRange.LengthSquared;
			var maxLimit = maxRange.LengthSquared;

			// Project actor position into the shroud plane
			// the terrian height is not 724!!!
			var projectedPos = pos;// - new WVec(0, (int)(pos.Z * MapGrid.MapHeightToYPos), pos.Z);
			var projectedCell = map.CellContaining(projectedPos);
			var projectedHeight = pos.Z;

			foreach (var c in map.FindTilesInAnnulus(projectedCell, minRange.Length / 1024, r, true))
			{
				var dist = (map.CenterOfCell(c) - projectedPos).HorizontalLengthSquared;
				if (dist <= maxLimit && (dist == 0 || dist > minLimit))
				{
					//var puv = (PPos)c.ToMPos(map);
					if (maxHeightDelta < 0 || map.HeightOfCell(c) < projectedHeight + maxHeightDelta)
						yield return c;
				}
			}
		}

		public static IEnumerable<CPos> ProjectedCellsInRange(Map map, CPos cell, WDist range, int maxHeightDelta = -1)
		{
			return ProjectedCellsInRange(map, map.CenterOfCell(cell), WDist.Zero, range, maxHeightDelta);
		}

		public void AddSource(object key, SourceType type, CPos[] projectedCells)
		{
			if (sources.ContainsKey(key))
				throw new InvalidOperationException("Attempting to add duplicate shroud source");

			sources[key] = new ShroudSource(type, projectedCells);

			foreach (var cell in projectedCells)
			{
				// Force cells outside the visible bounds invisible
				if (!map.Contains(cell))
					continue;

				var index = touched.Index(cell);
				touched[index] = true;
				anyCellTouched = true;
				switch (type)
				{
					case SourceType.PassiveVisibility:
						passiveVisibilityEnabled = true;
						passiveVisibleCount[index]++;
						explored[index] = true;
						break;
					case SourceType.Visibility:
						visibleCount[index]++;
						explored[index] = true;
						break;
					case SourceType.Shroud:
						shroudGenerationEnabled = true;
						generatedShroudCount[index]++;
						break;
				}
			}
		}

		public void RemoveSource(object key)
		{
			if (!sources.TryGetValue(key, out var state))
				return;

			foreach (var cell in state.ProjectedCells)
			{
				// Cells outside the visible bounds don't increment visibleCount
				if (map.Contains(cell))
				{
					var index = touched.Index(cell);
					touched[index] = true;
					anyCellTouched = true;
					switch (state.Type)
					{
						case SourceType.PassiveVisibility:
							passiveVisibleCount[index]--;
							break;
						case SourceType.Visibility:
							visibleCount[index]--;
							break;
						case SourceType.Shroud:
							generatedShroudCount[index]--;
							break;
					}
				}
			}

			sources.Remove(key);
		}

		public void ExploreProjectedCells(IEnumerable<CPos> cells)
		{
			foreach (var puv in cells)
			{
				if (map.Contains(puv))
				{
					var index = touched.Index(puv);
					if (!explored[index])
					{
						touched[index] = true;
						anyCellTouched = true;
						explored[index] = true;
					}
				}
			}
		}

		public void Explore(Shroud s)
		{
			if (map.Bounds != s.map.Bounds)
				throw new ArgumentException("The map bounds of these shrouds do not match.", nameof(s));

			var maxIndex = touched.Size.Width * touched.Size.Height;
			for (var index = 0; index < maxIndex; index++)
			{
				if (!explored[index] && s.explored[index])
				{
					touched[index] = true;
					anyCellTouched = true;
					explored[index] = true;
				}
			}
		}

		public void ExploreAll()
		{
			var maxIndex = touched.Size.Width * touched.Size.Height;
			for (var index = 0; index < maxIndex; index++)
			{
				// keep the cell out side of the bounds invisible
				if (!map.Contains(touched.IndexToMPos(index)))
					continue;

				if (!explored[index])
				{
					touched[index] = true;
					anyCellTouched = true;
					explored[index] = true;
				}
			}
		}

		public void ResetExploration()
		{
			var maxIndex = touched.Size.Width * touched.Size.Height;
			for (var index = 0; index < maxIndex; index++)
			{
				touched[index] = true;
				explored[index] = (visibleCount[index] + passiveVisibleCount[index]) > 0;
			}

			//foreach (var puv in map.ProjectedCells)
			//{
			//	var index = touched.Index(puv);
			//	touched[index] = true;
			//	explored[index] = (visibleCount[index] + passiveVisibleCount[index]) > 0;
			//}

			anyCellTouched = true;
		}

		public bool IsExplored(WPos pos)
		{
			return IsExplored(map.CellContaining(pos));
		}

		public bool IsExplored(CPos cell)
		{
			return IsExplored(cell.ToMPos(map));
		}

		public bool IsExplored(MPos uv)
		{
			//if (!map.Contains(uv))
			//	return false;

			//foreach (var puv in map.ProjectedCellsCovering(uv))
			//	if (IsExplored(puv))
			//		return true;

			//return false;

			if (Disabled)
				return map.Contains(uv);

			return resolvedType.Contains(uv) && resolvedType[uv] > ShroudCellType.Shroud;
		}

		//public bool IsExplored(PPos puv)
		//{
		//	if (Disabled)
		//		return map.Contains(puv);

		//	return resolvedType.Contains((MPos)puv) && resolvedType[(MPos)puv] > ShroudCellType.Shroud;
		//}

		public bool IsVisible(WPos pos)
		{
			return IsVisible(map.CellContaining(pos));
		}

		public bool IsVisible(CPos cell)
		{
			return IsVisible(cell.ToMPos(map));
		}

		public bool IsVisible(MPos uv)
		{
			//foreach (var puv in map.ProjectedCellsCovering(uv))
			//	if (IsVisible(puv))
			//		return true;

			//return false;

			if (!FogEnabled)
				return map.Contains(uv);

			return resolvedType.Contains(uv) && resolvedType[uv] == ShroudCellType.Visible;
		}

		//// In internal shroud coords
		//public bool IsVisible(PPos puv)
		//{
		//	if (!FogEnabled)
		//		return map.Contains(puv);

		//	return resolvedType.Contains(puv) && resolvedType[puv] == ShroudCellType.Visible;
		//}

		public bool Contains(MPos uv)
		{
			// Check that uv is inside the map area. There is nothing special
			// about explored here: any of the CellLayers would have been suitable.
			return explored.Contains(uv);
		}

		public CellVisibility GetVisibility(WPos pos)
		{
			return GetVisibility(map.CellContaining(pos).ToMPos(map));
		}

		// PERF: Combine IsExplored and IsVisible.
		public CellVisibility GetVisibility(MPos uv)
		{
			var state = CellVisibility.Hidden;

			if (Disabled)
			{
				if (fogEnabled)
				{
					// Shroud disabled, Fog enabled
					if (resolvedType.Contains(uv))
					{
						state |= CellVisibility.Explored;

						if (resolvedType[uv] == ShroudCellType.Visible)
							state |= CellVisibility.Visible;
					}
				}
				else if (map.Contains(uv))
					state |= CellVisibility.Explored | CellVisibility.Visible;
			}
			else
			{
				if (fogEnabled)
				{
					// Shroud and Fog enabled
					if (resolvedType.Contains(uv))
					{
						var rt = resolvedType[uv];
						if (rt == ShroudCellType.Visible)
							state |= CellVisibility.Explored | CellVisibility.Visible;
						else if (rt > ShroudCellType.Shroud)
							state |= CellVisibility.Explored;
					}
				}
				else if (resolvedType.Contains(uv))
				{
					// We do not set Explored since IsExplored may return false.
					state |= CellVisibility.Visible;

					if (resolvedType[uv] > ShroudCellType.Shroud)
						state |= CellVisibility.Explored;
				}
			}

			return state;
		}
	}
}
