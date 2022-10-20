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

		public string Description;
		public int Count { get; set; }

		public BasicUnitInfo(Actor actor)
		{
			Actor = actor;
			ActorInfo = actor.Info;
			ActorDisplaying = actor.TraitOrDefault<ActorDisplayInfomation>();
			BuildableInfo = actor.Info.TraitInfoOrDefault<BuildableInfo>();

			if (ActorDisplaying != null)
			{
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
		public int2 MaxInventorySize = new int2(300, 200);
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

		readonly Player player;
		readonly World world;

		/// <summary>
		/// Align the bottom, place it on the left and space it by padding
		/// </summary>
		Rectangle CalculateInventoryBounds()
		{
			return new Rectangle(-MaxInventorySize.X - InventoryPadding,
				Bounds.Height - MaxInventorySize.Y,
				MaxInventorySize.X, MaxInventorySize.Y);
		}

		[ObjectCreator.UseCtor]
		public ActorInfoWidget(ModData modData, World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			player = world.LocalPlayer;
			GetTooltipUnit = () => TooltipUnit;

			AddChild(currentInventory = new InventoryWidget(world, worldRenderer) {
				Bounds = CalculateInventoryBounds()
			});
		}

		public override string GetCursor(int2 pos) { return null; }

		public Func<KeyInput, bool> OnKeyPress = _ => false;

		public override bool HandleKeyPress(KeyInput e) { return OnKeyPress(e); }

		public void RefreshActorDisplaying()
		{
			if (world == null || world.Selection == null || world.Selection.Actors == null)
			{
				TooltipUnit = null;
				currentInventory.InventoryActor = null;
				currentInventory.Inventory = null;
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
				TooltipUnit = null;
				currentInventory.InventoryActor = null;
				currentInventory.Inventory = null;
				return;
			}

			TooltipUnit = new BasicUnitInfo(iconA);
			currentInventory.InventoryActor = iconA;
			currentInventory.Inventory = iconA?.TraitOrDefault<Inventory>();
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
			currentInventory.Render = currentInventory.InventoryActor != null && currentInventory.Inventory != null;

			if (currentInventory.Render)
				currentInventory.Bounds = CalculateInventoryBounds();
			else
				currentInventory.Bounds = new Rectangle(0, 0, 0, 0);

			if (selectionHash == world.Selection.Hash)
				return;

			RefreshActorDisplaying();
			selectionHash = world.Selection.Hash;
		}
	}
}
