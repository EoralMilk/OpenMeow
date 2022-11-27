using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class WithEquipmentAnimationInfo : TraitInfo
	{
		[FieldLoader.Require]
		public readonly string TakeOutAnim = null;
		[FieldLoader.Require]
		public readonly string PutAwayAnim = null;

		public readonly int TakeOutFrame = 0;
		public readonly int PutAwayFrame = 0;

		[Desc("A dictionary of animation replace.")]
		public readonly Dictionary<string, string> AnimsReplace = new Dictionary<string, string>();

		public readonly Dictionary<string, int> ActionFrameReplace = new Dictionary<string, int>();

		public readonly string[] IdleActions = null;

		public override object Create(ActorInitializer init)
		{
			return new WithEquipmentAnimation(this);
		}
	}

	public class WithEquipmentAnimation
	{
		public readonly WithEquipmentAnimationInfo Info;
		public WithEquipmentAnimation(WithEquipmentAnimationInfo info)
		{
			Info = info;
		}

	}
}
