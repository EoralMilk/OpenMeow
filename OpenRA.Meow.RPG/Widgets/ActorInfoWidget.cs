using System;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Meow.RPG.Mechanics.Display;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Meow.RPG.Widgets
{
	public class BasicUnitInfo
	{
		public readonly Actor Actor;
		public readonly ActorInfo ActorInfo;
		public readonly TooltipInfo TooltipInfo;
		public readonly ActorDisplayInfomation ActorDisplaying;
		public readonly BuildableInfo BuildableInfo;
		readonly string palette;
		public PaletteReference Palette;
		public readonly float Scale = 1;

		public string Description;
		public int Count { get; set; }

		public Actor GetValidActor()
		{
			if (Actor == null || Actor.IsDead || !Actor.IsInWorld)
				return null;
			else
				return Actor;
		}

		public BasicUnitInfo(Actor actor)
		{
			Actor = actor;
			ActorInfo = actor.Info;
			ActorDisplaying = actor.TraitOrDefault<ActorDisplayInfomation>();
			BuildableInfo = actor.Info.TraitInfoOrDefault<BuildableInfo>();

			if (ActorDisplaying != null)
			{
				Scale = ActorDisplaying.Info.IconScale;
				palette = ActorDisplaying.Info.IconPaletteIsPlayerPalette ? ActorDisplaying.Info.IconPalette + actor.Owner.InternalName : ActorDisplaying.Info.IconPalette;
			}
			else if (BuildableInfo != null)
			{
				palette = BuildableInfo.IconPaletteIsPlayerPalette ? BuildableInfo.IconPalette + actor.Owner.InternalName : BuildableInfo.IconPalette;
			}

			TooltipInfo = actor.Info.TraitInfos<TooltipInfo>().FirstOrDefault(info => info.EnabledByDefault);

			if (ActorDisplaying != null && ActorDisplaying.Info.Description != null)
				Description = ActorDisplaying.Info.Description;
			else if (BuildableInfo != null)
				Description = BuildableInfo.Description;
		}

		public Animation GetDisplayIcon(WorldRenderer wr)
		{
			if (Actor == null || Actor.IsDead || !Actor.IsInWorld)
			{
				Palette = null;
				return null;
			}

			var rs = Actor.TraitOrDefault<RenderSprites>();
			if (rs == null)
				return null;

			if (palette != null)
				Palette = wr.Palette(palette);

			if (ActorDisplaying != null)
			{
				var icon = new Animation(wr.World, rs.GetImage(ActorDisplaying.Self));
				icon.Play(ActorDisplaying.Info.IconSequence);
				return icon;
			}
			else if (BuildableInfo != null)
			{
				var icon = new Animation(wr.World, rs.GetImage(Actor));
				icon.Play(BuildableInfo.Icon);
				return icon;
			}

			return null;
		}
	}

	public class ActorInfoWidget : Widget, IAddFactionSuffixWidget
	{
		public int2 MaxInventorySize = new int2(Skin.InventoryWidth, 200);
		public int2 MaxSlotsSize = new int2(Skin.InventoryWidth, 200);

		public int InventoryPadding = 5;

		public bool ClickThrough = true;
		public string Background = "dialog";
		public void AddFactionSuffix(string suffix)
		{
			Background += suffix;
		}

		readonly WorldRenderer worldRenderer;
		int selectionHash;
		public BasicUnitInfo TooltipUnit { get; private set; }
		public bool ShouldRender => TooltipUnit != null;
		public Func<BasicUnitInfo> GetTooltipUnit;

		readonly InventoryWidget currentInventory;
		InventoryWidget lootInventory;
		readonly EquipmentSlotsWidget slotsWidget;

		readonly Player player;
		readonly World world;
		readonly ModData modData;

		/// <summary>
		/// Align the bottom, place it on the left and space it by padding
		/// </summary>
		Rectangle CalculateInventoryBounds()
		{
			return new Rectangle(-MaxInventorySize.X - InventoryPadding,
				Bounds.Height - MaxInventorySize.Y,
				MaxInventorySize.X, MaxInventorySize.Y);
		}

		Rectangle CalculateEquipmentSlotsBounds()
		{
			return new Rectangle(-MaxSlotsSize.X - MaxInventorySize.X - InventoryPadding,
					Bounds.Height - MaxSlotsSize.Y,
					MaxSlotsSize.X, MaxSlotsSize.Y);
		}

		[ObjectCreator.UseCtor]
		public ActorInfoWidget(ModData modData, World world, WorldRenderer worldRenderer)
		{
			this.modData = modData;
			this.world = world;
			this.worldRenderer = worldRenderer;
			player = world.LocalPlayer;
			GetTooltipUnit = () => TooltipUnit;
			currentInventory = new InventoryWidget(world, worldRenderer);
			slotsWidget = new EquipmentSlotsWidget(world, worldRenderer);
		}

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);

			currentInventory.Bounds = CalculateInventoryBounds();
			slotsWidget.Bounds = CalculateEquipmentSlotsBounds();
			AddChild(currentInventory);
			AddChild(slotsWidget);

			var iconWidth = Bounds.Width * 5 / 10;
			var iconHeight = 100;
			var iconY = Skin.ActorNameHeight + 14;
			var iconX = Bounds.Width - iconWidth - Skin.SpacingSmall;

			AddChild(
				new ActorInfoIconWidget(modData, world, worldRenderer)
				{
					Background = Background,
					Logic = new string[] { "AddFactionSuffixLogic" },
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(iconX, iconY, iconWidth, iconHeight),
				}
			);

			var fontsmall = Skin.Fontsmall;

			var y = 14;
			var x = 14;
			var valueX = x + 50;

			var labelwidth = iconX - 12 - Skin.SpacingSmall;
			var valueWidth = labelwidth - 50;

			AddChild(
				new LabelWidget
				{
					GetText = () =>
					{
						var name = TooltipUnit?.TooltipInfo.Name;

						return name == null ? "-" : name;
					},
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(x, y, labelwidth + iconWidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont,
					FontsForScale = fontsmall,
				}
			);

			y += Skin.ActorNameHeight;

			AddChild(
				new LabelWidget
				{
					Text = "HP",
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(x, y, labelwidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont,
				}
			);

			AddChild(
				new LabelWidget
				{
					GetText = () =>
					{
						var health = TooltipUnit.GetValidActor()?.TraitOrDefault<Health>();

						return health == null ? "-" : $"{health.DisplayHP / Skin.ActorHealthDiv} / {health.MaxHP / Skin.ActorHealthDiv}";
					},
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(valueX, y, valueWidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont,
					Align = TextAlign.Right,
					FontsForScale = fontsmall,
				}
			);

			y += Skin.CharacterLabelHeight;

			AddChild(
				new LabelWidget
				{
					Text = "AMR",
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(x, y, labelwidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont
				}
			);

			AddChild(
				new LabelWidget
				{
					GetText = () =>
					{
						var armors = TooltipUnit.GetValidActor()?.TraitsImplementing<Armor>().Where(armor => armor.IsTraitEnabled()).ToArray();

						return armors == null || armors.Length == 0 ? "-" : armors.First().ArmorType;
					},
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(valueX, y, valueWidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont,
					Align = TextAlign.Right,
					FontsForScale = fontsmall,
				}
			);

			y += Skin.CharacterLabelHeight;

			AddChild(
				new LabelWidget
				{
					Text = "SPD",
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(x, y, labelwidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont
				}
			);

			AddChild(
				new LabelWidget
				{
					GetText = () =>
					{
						var mbspeed = TooltipUnit.GetValidActor()?.TraitsImplementing<Mobile>().Where(mb => mb.IsTraitEnabled()).ToArray();
						var airspeed = TooltipUnit.GetValidActor()?.TraitsImplementing<Aircraft>().Where(mb => mb.IsTraitEnabled()).ToArray();

						return ((mbspeed == null || mbspeed.Length == 0) && (airspeed == null || airspeed.Length == 0)) ? "-" :
									mbspeed != null && mbspeed.Length == 1 ? $"{mbspeed.First().Info.Speed}" :
									airspeed != null && airspeed.Length == 1 ? $"{airspeed.First().Info.Speed}" : "-";
					},
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(valueX, y, valueWidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont,
					Align = TextAlign.Right,
					FontsForScale = fontsmall,
				}
			);

			y += Skin.CharacterLabelHeight;

			AddChild(
				new LabelWidget
				{
					Text = "FOV",
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(x, y, labelwidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont
				}
			);

			AddChild(
				new LabelWidget
				{
					GetText = () =>
					{
						var range = 0;
						var sights = TooltipUnit.GetValidActor()?.TraitsImplementing<RevealsShroud>().Where(s => s.IsTraitEnabled()).ToArray();

						if (sights != null && sights.Length > 0)
							foreach (var s in sights)
							{
								if (range < s.Range.Length)
									range = s.Range.Length;
							}

						return range == 0 ? "-" : $"{(float)range / 1024}";
					},
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(valueX, y, valueWidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont,
					Align = TextAlign.Right,
					FontsForScale = fontsmall,
				}
			);

			y += Skin.CharacterLabelHeight;

			AddChild(
				new LabelWidget
				{
					Text = "LVL",
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(x, y, labelwidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont
				}
			);

			AddChild(
				new LabelWidget
				{
					GetText = () =>
					{
						var gainsExperiences = TooltipUnit.GetValidActor()?.TraitsImplementing<GainsExperience>().Where(g => g.IsTraitEnabled()).ToArray();

						return gainsExperiences == null || gainsExperiences.Length == 0 ? "-" : $"{gainsExperiences.First().Level}";
					},
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(valueX, y, valueWidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont,
					Align = TextAlign.Right,
					FontsForScale = fontsmall,
				}
			);

			y += Skin.CharacterLabelHeight;

			AddChild(
				new LabelWidget
				{
					Text = "XP",
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(x, y, labelwidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont
				}
			);

			AddChild(
				new LabelWidget
				{
					GetText = () =>
					{
						var gainsExperiences = TooltipUnit.GetValidActor()?.TraitsImplementing<GainsExperience>().Where(g => g.IsTraitEnabled()).ToArray();

						return gainsExperiences == null || gainsExperiences.Length == 0 ? "-" : $"{gainsExperiences.First().Experience / Skin.ActorExpDiv}";
					},
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(valueX, y, valueWidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont,
					Align = TextAlign.Right,
					FontsForScale = fontsmall,
				}
			);
		}

		public override string GetCursor(int2 pos) { return null; }

		public Func<KeyInput, bool> OnKeyPress = _ => false;

		public override bool HandleKeyPress(KeyInput e) { return OnKeyPress(e); }

		void UpdateTooltipActor(Actor actor)
		{
			TooltipUnit = actor == null ? null : new BasicUnitInfo(actor);
			currentInventory.InventoryActor = actor;
			currentInventory.Inventory = actor == null ? null : actor?.TraitOrDefault<Inventory>();
			slotsWidget?.UpdateActor(actor);
		}

		public void RefreshActorDisplaying()
		{
			if (world == null || world.Selection == null || world.Selection.Actors == null)
			{
				UpdateTooltipActor(null);
				return;
			}

			Actor iconA = null;

			foreach (var a in world.Selection.Actors)
			{
				if (!a.IsInWorld || a.IsDead || a.Disposed)
					continue;
				if (iconA == null || a.GetSellValue() > iconA.GetSellValue())
					iconA = a;
			}

			if (iconA == null || !iconA.IsInWorld || iconA.IsDead || iconA.Disposed)
			{
				UpdateTooltipActor(null);
				return;
			}

			UpdateTooltipActor(iconA);
		}

		public override void Draw()
		{
			if (TooltipUnit == null)
			{
				return;
			}

			WidgetUtils.DrawPanel(Background, RenderBounds);
		}

		public override void Tick()
		{
			//if (currentInventory.Render)
			//	currentInventory.Bounds = CalculateInventoryBounds();
			//else
			//	currentInventory.Bounds = Rectangle.Empty;

			//if (slotsWidget.Render)
			//	slotsWidget.Bounds = CalculateEquipmentSlotsBounds();
			//else
			//	slotsWidget.Bounds = Rectangle.Empty;

			if (selectionHash == world.Selection.Hash)
				return;

			RefreshActorDisplaying();
			selectionHash = world.Selection.Hash;
		}
	}
}
