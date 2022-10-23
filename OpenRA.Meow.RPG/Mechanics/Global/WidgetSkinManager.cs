using OpenRA.Meow.RPG.Widgets;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class WidgetSkinManagerInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new WidgetSkinManager(init.World, this); }
	}

	public class WidgetSkinManager
	{
		WidgetSkinManagerInfo info;
		World world;
		public readonly Skin DefaultSkin;
		public WidgetSkinManager(World world, WidgetSkinManagerInfo info)
		{
			this.world = world;
			this.info = info;
			DefaultSkin = new Skin();
		}
	}
}
