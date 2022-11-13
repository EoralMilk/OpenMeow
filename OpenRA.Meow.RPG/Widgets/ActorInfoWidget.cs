using System;
using System.Linq;
using System.Reflection;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Meow.RPG.Mechanics.Display;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;
using OpenRA.Widgets;
using static OpenRA.Network.Session;
using System.Numerics;
using TagLib.Ape;

namespace OpenRA.Meow.RPG.Widgets
{
	public class BasicUnitInfo
	{
		public readonly Actor Actor;
		public readonly ActorInfo ActorInfo;
		public readonly TooltipInfo TooltipInfo;
		public readonly ActorDisplayInfomation ActorDisplaying;
		public readonly BuildableInfo BuildableInfo;
		public readonly bool HasInventory;
		public readonly bool HasEquipmentSlot;
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

			if (actor.TraitsImplementing<Inventory>().Any())
				HasInventory = true;
			if (actor.TraitsImplementing<EquipmentSlot>().Any())
				HasEquipmentSlot = true;
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

	public enum InfoDisplaying
	{
		None = 0,
		Equipment,
		Inventory,
		Upgrade,
		Skill,
	}

	public class ActorInfoWidget : Widget, IAddFactionSuffixWidget
	{
		public int2 MaxInventorySize = new int2(150, 200);
		public int2 MaxSlotsSize = new int2(150, 200);

		public int InventoryPadding = 5;

		public string Background = "dialog";
		public void AddFactionSuffix(string suffix)
		{
			Background += suffix;
		}

		readonly WorldRenderer worldRenderer;
		int selectionHash;
		public BasicUnitInfo TooltipUnit { get; private set; }
		public Func<BasicUnitInfo> GetTooltipUnit;

		readonly InventoryWidget currentInventory;
		InventoryWidget lootInventory;
		readonly EquipmentSlotsWidget slotsWidget;

		ShadowProgressBarWidget hpBar;

		readonly Player player;
		readonly World world;
		readonly ModData modData;

		InfoDisplaying displayState;
		public InfoDisplaying DisplayState {
			get
			{
				return displayState;
			}
			set
			{
				currentInventory.ActiveToggle = false;
				slotsWidget.ActiveToggle = false;

				switch (value)
				{
					case InfoDisplaying.Equipment:
						currentInventory.ActiveToggle = true;
						slotsWidget.ActiveToggle = true;

						toggleEquipment.SetActive = true;
						toggleInventory.SetActive = false;
						break;
					case InfoDisplaying.Inventory:
						currentInventory.ActiveToggle = true;
						slotsWidget.ActiveToggle = false;

						toggleEquipment.SetActive = false;
						toggleInventory.SetActive = true;
						break;
					default:
						currentInventory.ActiveToggle = false;
						slotsWidget.ActiveToggle = false;

						toggleEquipment.SetActive = false;
						toggleInventory.SetActive = false;
						break;
				}

				displayState = value;
			}
		}

		public readonly Skin Skin;

		[ObjectCreator.UseCtor]
		public ActorInfoWidget(ModData modData, World world, WorldRenderer worldRenderer)
		{
			this.modData = modData;
			this.world = world;
			this.worldRenderer = worldRenderer;
			Skin = world.WorldActor.Trait<WidgetSkinManager>().DefaultSkin;

			player = world.LocalPlayer;
			GetTooltipUnit = () => TooltipUnit;
			currentInventory = new InventoryWidget(world, worldRenderer, Skin);
			slotsWidget = new EquipmentSlotsWidget(world, worldRenderer, Skin);
		}

		ToggleButtonWidget toggleEquipment;
		ToggleButtonWidget toggleInventory;

		public void InitializeStateTab()
		{
			var size = Bounds.Height / 5;
			var x = -size - Skin.SpacingSmall;

			toggleEquipment = new ToggleButtonWidget(worldRenderer, Skin)
			{
				IsVisible = () => TooltipUnit != null,
				Bounds = new Rectangle(x , Bounds.Height - size, size, size),
				IsEnabled = () => toggleEquipment.IsVisible() && TooltipUnit.HasEquipmentSlot,
				OnClick = () => {
					if (toggleEquipment.SetActive)
						DisplayState = InfoDisplaying.Equipment;
					else if (DisplayState == InfoDisplaying.Equipment)
						DisplayState = InfoDisplaying.None;
				}
			};
			toggleEquipment.AddChild(new LabelWidget
			{
				Text = "E",
				IsVisible = () => TooltipUnit != null,
				Bounds = new Rectangle(0, 0, toggleEquipment.Bounds.Width, toggleEquipment.Bounds.Height),
				Font = Skin.InGameUiFont,
				Align = TextAlign.Center,
				VAlign = TextVAlign.Middle,
				GetColor = () => TooltipUnit.HasEquipmentSlot ? Skin.FontColor : Skin.FontDisableColor,
			});

			x = x - size - Skin.SpacingSmall;

			toggleInventory = new ToggleButtonWidget(worldRenderer, Skin)
			{
				IsVisible = () => TooltipUnit != null,
				Bounds = new Rectangle(x, Bounds.Height - size, size, size),
				IsEnabled = () => toggleInventory.IsVisible() && TooltipUnit.HasInventory,
				OnClick = () => {
					if (toggleInventory.SetActive)
						DisplayState = InfoDisplaying.Inventory;
					else if (DisplayState == InfoDisplaying.Inventory)
						DisplayState = InfoDisplaying.None;
				}
			};
			toggleInventory.AddChild(new LabelWidget
			{
				Text = "I",
				IsVisible = () => TooltipUnit != null,
				Bounds = new Rectangle(0, 0, toggleEquipment.Bounds.Width, toggleEquipment.Bounds.Height),
				Font = Skin.InGameUiFont,
				Align = TextAlign.Center,
				VAlign = TextVAlign.Middle,
				GetColor = () => TooltipUnit.HasInventory ? Skin.FontColor : Skin.FontDisableColor,
			});

			AddChild(toggleEquipment);
			AddChild(toggleInventory);

			currentInventory.Bounds = new Rectangle(-MaxInventorySize.X - InventoryPadding,
					Bounds.Height - MaxInventorySize.Y - size - Skin.SpacingSmall,
					MaxInventorySize.X, MaxInventorySize.Y);
			slotsWidget.Bounds = new Rectangle(-MaxSlotsSize.X - MaxInventorySize.X - InventoryPadding,
					Bounds.Height - MaxSlotsSize.Y - size - Skin.SpacingSmall,
					MaxSlotsSize.X, MaxSlotsSize.Y);

			DisplayState = InfoDisplaying.None;
		}

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);

			var border = 12;

			AddChild(currentInventory);
			AddChild(slotsWidget);

			var iconWidth = Bounds.Width * 5 / 10;
			var iconY = Skin.ActorNameHeight + border + 2;
			var iconHeight = Bounds.Height - iconY - border - 2;
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

			var y = border + 2;
			var x = border + 2;
			var valueX = x + 50;

			var labelwidth = iconX - border - Skin.SpacingSmall;
			var valueWidth = labelwidth - 50;
			hpBar = new ShadowProgressBarWidget(Skin)
			{
				GetProgress = () =>
				{
					var health = TooltipUnit.GetValidActor()?.TraitOrDefault<Health>();
					return health == null ? 0 : (float)health.HP / health.MaxHP;
				},
				GetColor = () =>
				{
					return Color.Lerp(Skin.HPMinColor, Skin.HPMaxColor, hpBar.GetProgress());
				},
				IsVisible = () => TooltipUnit != null,
				Bounds = new Rectangle(x - 2, y - 2, Bounds.Width - border * 2, Skin.CharacterLabelHeight + 4),
			};

			AddChild(
				hpBar
			);

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

			AddChild(
				new LabelWidget
				{
					GetText = () =>
					{
						string click = controlMode ? "[" : "";
						click += moveUp ? " U" : "";
						click += moveDown ? " D" : "";
						click += moveLeft ? " L" : "";
						click += moveRight ? " R" : "";
						click += controlMode ? " ]" : "";

						return click;
					},
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(0, -Skin.CharacterLabelHeight - 20, labelwidth + iconWidth, Skin.CharacterLabelHeight),
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
					Text = "Kill",
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

						return gainsExperiences == null || gainsExperiences.Length == 0 ? "-" : $"{gainsExperiences.First().KillsCount}";
					},
					IsVisible = () => TooltipUnit != null,
					Bounds = new Rectangle(valueX, y, valueWidth, Skin.CharacterLabelHeight),
					Font = Skin.InGameUiFont,
					Align = TextAlign.Right,
					FontsForScale = fontsmall,
				}
			);

			InitializeStateTab();
		}

		void UpdateTooltipActor(Actor actor)
		{
			TooltipUnit = actor == null ? null : new BasicUnitInfo(actor);
			currentInventory.InventoryActor = actor;
			currentInventory.Inventory = actor == null ? null : actor?.TraitOrDefault<Inventory>();
			slotsWidget?.UpdateActor(actor);

			switch (DisplayState)
			{
				case InfoDisplaying.Equipment:
					if (!toggleEquipment.IsEnabled())
						DisplayState = InfoDisplaying.None;
					break;
				case InfoDisplaying.Inventory:
					if (!toggleInventory.IsEnabled())
						DisplayState = InfoDisplaying.None;
					break;
			}
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

		public override bool HandleMouseInput(MouseInput mi)
		{
			return TooltipUnit != null && EventBounds.Contains(mi.Location);
		}

		public override void Draw()
		{
			if (TooltipUnit == null)
				return;

			WidgetUtils.DrawPanel(Background, RenderBounds);
		}

		bool lastMove;
		public override void Tick()
		{
			if (TooltipUnit == null || TooltipUnit.Actor == null || TooltipUnit.Actor.IsDead || !TooltipUnit.Actor.IsInWorld)
			{
				controlMode = false;
			}

			if (controlMode)
			{
				Ui.KeyboardFocusWidget = this;

				var mover = TooltipUnit.Actor.TraitOrDefault<Mobile>();

				if (mover == null)
				{
					controlMode = false;
				}

				if (controlMode)
				{
					WVec mVec = WVec.Zero;

					if (moveUp)
					{
						mVec += new WVec(0, -1024, 0);
					}

					if (moveDown)
					{
						mVec += new WVec(0, 1024, 0);
					}

					if (moveLeft)
					{
						mVec += new WVec(-1024, 0, 0);
					}

					if (moveRight)
					{
						mVec += new WVec(1024, 0, 0);
					}

					if (mVec != WVec.Zero)
					{
						var tPos = mover.CenterPosition + mVec;
						var order = new Order("Mover:Move", TooltipUnit.Actor, Target.FromPos(tPos), false);
						TooltipUnit.Actor.World.IssueOrder(order);
						lastMove = true;
					}
					else if (lastMove)
					{
						lastMove = false;
						var order = new Order("Mover:Stop", TooltipUnit.Actor, false);
						TooltipUnit.Actor.World.IssueOrder(order);
					}
				}
			}
			else
			{
				if (Ui.KeyboardFocusWidget == this)
					Ui.KeyboardFocusWidget = null;
				lastMove = false;
				moveUp = false;
				moveDown = false;
				moveLeft = false;
				moveRight = false;
			}

			if (selectionHash == world.Selection.Hash)
				return;

			RefreshActorDisplaying();
			selectionHash = world.Selection.Hash;
		}

		public Action<KeyInput> OnKeyPress = _ => { };
		public HotkeyReference ToggleControlKey = new HotkeyReference();
		bool controlMode = false;

		public HotkeyReference MoveUpKey = new HotkeyReference();
		public HotkeyReference MoveDownKey = new HotkeyReference();
		public HotkeyReference MoveLeftKey = new HotkeyReference();
		public HotkeyReference MoveRightKey = new HotkeyReference();

		bool moveUp;
		bool moveDown;
		bool moveLeft;
		bool moveRight;

		public override bool HandleKeyPress(KeyInput e)
		{
			if (ToggleControlKey.IsActivatedBy(e) && e.Event == KeyInputEvent.Down && !e.IsRepeat)
				controlMode = !controlMode;

			if (!controlMode || TooltipUnit == null || TooltipUnit.Actor == null || TooltipUnit.Actor.IsDead || !TooltipUnit.Actor.IsInWorld)
			{
				controlMode = false;
			}

			if (controlMode)
			{
				OnKeyPress(e);
				if (MoveUpKey.IsActivatedBy(e) && e.Event == KeyInputEvent.Down)
					moveUp = true;
				else if (MoveUpKey.IsActivatedBy(e) && e.Event == KeyInputEvent.Up)
					moveUp = false;

				if (MoveDownKey.IsActivatedBy(e) && e.Event == KeyInputEvent.Down)
					moveDown = true;
				else if (MoveDownKey.IsActivatedBy(e) && e.Event == KeyInputEvent.Up)
					moveDown = false;

				if (MoveLeftKey.IsActivatedBy(e) && e.Event == KeyInputEvent.Down)
					moveLeft = true;
				else if (MoveLeftKey.IsActivatedBy(e) && e.Event == KeyInputEvent.Up)
					moveLeft = false;

				if (MoveRightKey.IsActivatedBy(e) && e.Event == KeyInputEvent.Down)
					moveRight = true;
				else if (MoveRightKey.IsActivatedBy(e) && e.Event == KeyInputEvent.Up)
					moveRight = false;

				return true;
			}

			return false;
		}
	}
}
