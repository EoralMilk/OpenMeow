using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
	public class AttackAttachmentTurretedInfo : AttackFollowInfo, Requires<TurretAttachmentInfo>
	{
		[Desc("Turret names")]
		public readonly string[] Turrets = { "turret" };

		public override object Create(ActorInitializer init) { return new AttackAttachmentTurreted(init.Self, this); }
	}

	public class AttackAttachmentTurreted : AttackFollow
	{
		protected TurretAttachment[] turrets;

		public AttackAttachmentTurreted(Actor self, AttackAttachmentTurretedInfo info)
			: base(self, info)
		{
			turrets = self.TraitsImplementing<TurretAttachment>().Where(t => info.Turrets.Contains(t.TurretInfo.Name)).ToArray();
		}

		protected override bool CanAttack(Actor self, in Target target)
		{
			if (target.Type == TargetType.Invalid)
				return false;

			// Don't break early from this loop - we want to bring all turrets to bear!
			var turretReady = false;
			foreach (var t in turrets)
				if (t.FacingTarget(target, GetTargetPosition(self.CenterPosition, target)))
					turretReady = true;

			return turretReady && base.CanAttack(self, target);
		}
	}
}
