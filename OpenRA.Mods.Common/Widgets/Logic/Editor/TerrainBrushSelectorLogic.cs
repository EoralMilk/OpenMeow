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
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class TerrainBrushSelectorLogic : CommonSelectorLogic
	{
		class TerrainBrushSelectorTemplate
		{
			public readonly MaskBrush Brush;
			public readonly string[] Categories;
			public readonly string[] SearchTerms;

			public TerrainBrushSelectorTemplate(MaskBrush brush)
			{
				Brush = brush;
				Categories = brush.Categories;
				SearchTerms = new[] { Brush.Name };
			}
		}

		readonly MapTextureCache textureCache;
		readonly TerrainBrushSelectorTemplate[] allTemplates;
		readonly EditorCursorLayer editorCursor;

		readonly SliderWidget alphaSlider;
		readonly SliderWidget sizeSlider;
		readonly LabelWidget alphaLabel;
		readonly LabelWidget sizeLabel;

		protected readonly HashSet<string> SelectedLayers = new HashSet<string>();

		readonly string[] allLayers;

		public readonly Dictionary<string, int> LayerIndex = new Dictionary<string, int>();

		protected readonly ScrollPanelWidget TypePanel;
		protected readonly ScrollItemWidget TypeItemTemplate;
		protected readonly TerrainTypeInfo[] allTypes;

		[ObjectCreator.UseCtor]
		public TerrainBrushSelectorLogic(Widget widget, ModData modData, World world, WorldRenderer worldRenderer)
			: base(widget, modData, world, worldRenderer, "TERRAINBRUSH_TEMPLATELIST", "TERRAINBRUSH_TEMPLATE")
		{
			textureCache = world.Map.TextureCache;
			if (textureCache == null)
				throw new InvalidDataException("TerrainBrushSelectorLogic requires a MapTextureCache.");

			var settings = widget.Get<ContainerWidget>("BRUSHSETTING_CONTAINER");
			alphaSlider = settings.Get<SliderWidget>("ALPHA_SLIDER");
			sizeSlider = settings.Get<SliderWidget>("SIZE_SLIDER");
			alphaLabel = settings.Get<LabelWidget>("ALPHA_LABEL");
			sizeLabel = settings.Get<LabelWidget>("SIZE_LABEL");
			alphaLabel.GetText = () => { return " Alpha : " + GetBrushAlpha().ToString(); };
			sizeLabel.GetText = () => { return " Size : " + GetBrushSize().ToString(); };

			// type list
			TypePanel = settings.Get<ScrollPanelWidget>("CELLTYPE_TEMPLATELIST");
			TypeItemTemplate = TypePanel.Get<ScrollItemWidget>("CELLTYPE_TEMPLATE");
			allTypes = world.Map.Rules.TerrainInfo.TerrainTypes;
			for (int i = 0; i < allTypes.Length; i++)
			{
				int index = i;
				var item = ScrollItemWidget.Setup(TypeItemTemplate,
					() => editorCursor.Type == EditorCursorType.Brush && editorCursor.CellType == index,
					() => editorCursor.CellType = index);

				var typeLabel = item.Get<LabelWidget>("CELLTYPE");
				typeLabel.GetText = () => allTypes[index].Type;
				item.IsVisible = () => true;
				item.GetTooltipText = () => allTypes[index].Type;

				TypePanel.AddChild(item);
			}

			allTemplates = textureCache.AllBrushes.Values.Select(t => new TerrainBrushSelectorTemplate(t)).ToArray();
			editorCursor = world.WorldActor.Trait<EditorCursorLayer>();

			editorCursor.GetBrushSize = GetBrushSize;
			editorCursor.GetBrushAlpha = GetBrushAlpha;

			allCategories = allTemplates.SelectMany(t => t.Categories)
				.Distinct()
				.OrderBy(CategoryOrder)
				.ToArray();

			foreach (var c in allCategories)
			{
				SelectedCategories.Add(c);
				FilteredCategories.Add(c);
			}

			allLayers = new string[]{
				"0 Water",
				"1 Cliff",
				"2 Conc",
				"3 Grass",
				"4 Sand ",
				"5 Dirt",
				"6 Gravel",
				"7 Crack",
				"8 Clear",
			};

			for (int i = 0; i < allLayers.Length; i++)
			{
				LayerIndex.Add(allLayers[i], i);
			}

			var none = ModData.Translation.GetString(None);
			var searchResults = ModData.Translation.GetString(SearchResults);
			var all = ModData.Translation.GetString(All);
			var multiple = ModData.Translation.GetString(Multiple);

			var layerSelector = widget.Get<DropDownButtonWidget>("MASKLAYER_DROPDOWN");
			layerSelector.GetText = () =>
			{
				if (SelectedLayers.Count == 0)
					return none;

				if (!string.IsNullOrEmpty(searchFilter))
					return searchResults;

				if (SelectedLayers.Count == 1)
					return SelectedLayers.First();

				if (SelectedLayers.Count == allLayers.Length)
					return all;

				return multiple;
			};

			layerSelector.OnMouseDown = _ =>
			{
				SearchTextField?.YieldKeyboardFocus();

				layerSelector.RemovePanel();
				layerSelector.AttachPanel(CreateLayerPanel(Panel));
			};

			SearchTextField.OnTextEdited = () =>
			{
				searchFilter = SearchTextField.Text.Trim();
				FilteredCategories.Clear();

				if (!string.IsNullOrEmpty(searchFilter))
					FilteredCategories.AddRange(
						allTemplates.Where(t => t.SearchTerms.Any(
							s => s.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0))
						.SelectMany(t => t.Categories)
						.Distinct()
						.OrderBy(CategoryOrder));
				else
					FilteredCategories.AddRange(allCategories);

				InitializePreviews();
			};

			InitializePreviews();
		}

		protected Widget CreateLayerPanel(ScrollPanelWidget panel)
		{
			var layerPanel = Ui.LoadWidget("MASKLAYER_PANEL", null, new WidgetArgs());
			var layerTemplate = layerPanel.Get<CheckboxWidget>("LAYER_CHECKBOX");

			var selectButtons = layerPanel.Get<ContainerWidget>("SELECT_LAYER_BUTTONS");
			layerPanel.AddChild(selectButtons);

			var selectAll = selectButtons.Get<ButtonWidget>("SELECT_ALL");
			selectAll.OnClick = () =>
			{
				SelectedLayers.Clear();
				foreach (var c in allLayers)
					SelectedLayers.Add(c);

				InitializePreviews();
			};

			var selectNone = selectButtons.Get<ButtonWidget>("SELECT_NONE");
			selectNone.OnClick = () =>
			{
				SelectedLayers.Clear();
				InitializePreviews();
			};

			var layerHeight = 5 + selectButtons.Bounds.Height;
			foreach (var layer in allLayers)
			{
				var checkbox = (CheckboxWidget)layerTemplate.Clone();
				checkbox.GetText = () => layer;
				checkbox.IsChecked = () => SelectedLayers.Contains(layer);
				checkbox.IsVisible = () => true;
				checkbox.OnClick = () =>
				{
					if (!SelectedLayers.Remove(layer))
						SelectedLayers.Add(layer);

					InitializePreviews();
				};

				layerPanel.AddChild(checkbox);
				layerHeight += layerTemplate.Bounds.Height;
			}

			layerPanel.Bounds.Height = Math.Min(layerHeight, panel.Bounds.Height);

			return layerPanel;
		}

		int[] GetActiveLayers()
		{
			int[] layers = new int[SelectedLayers.Count];
			int i = 0;
			foreach (var layername in SelectedLayers)
			{
				layers[i] = LayerIndex[layername];
				i++;
			}

			return layers;
		}

		int GetBrushAlpha()
		{
			return (int)(alphaSlider.GetValue() * 255);
		}

		float GetBrushSize()
		{
			return sizeSlider.GetValue();
		}

		int CategoryOrder(string category)
		{
			// var i = terrainInfo.EditorTemplateOrder.IndexOf(category);
			// return i >= 0 ? i : int.MaxValue;
			return 0;
		}

		protected override void InitializePreviews()
		{
			Panel.RemoveChildren();
			if (SelectedCategories.Count == 0)
				return;

			foreach (var t in allTemplates)
			{
				if (!SelectedCategories.Overlaps(t.Categories))
					continue;

				if (!string.IsNullOrEmpty(searchFilter) && !t.SearchTerms.Any(s => s.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0))
					continue;

				var brushId = t.Brush.Id;
				var item = ScrollItemWidget.Setup(ItemTemplate,
					() => editorCursor.Type == EditorCursorType.Brush && editorCursor.Brush.Id == brushId,
					() => Editor.SetBrush(new EditorTerrainMaskBrush(GetActiveLayers, GetBrushAlpha, GetBrushSize, Editor, t.Brush, WorldRenderer)));

				var preview = item.Get<TerrainBrushPreviewWidget>("BRUSH_PREVIEW");
				var bounds = t.Brush.TextureSize;

				// Scale templates to fit within the panel
				var scale = 1f;
				while (scale * bounds.X > ItemTemplate.Bounds.Width)
					scale /= 2;

				preview.Brush = t.Brush;
				preview.GetScale = () => scale;
				preview.Bounds.Width = (int)(scale * bounds.X);
				preview.Bounds.Height = (int)(scale * bounds.Y);

				item.Bounds.Width = preview.Bounds.Width + 2 * preview.Bounds.X;
				item.Bounds.Height = preview.Bounds.Height + 2 * preview.Bounds.Y;
				item.IsVisible = () => true;
				item.GetTooltipText = () => t.Brush.Name;

				Panel.AddChild(item);
			}
		}
	}
}
