using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;
using TrueSync;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
	public class AttachManagerInfo : RejectsOrdersInfo
	{
		public readonly string MainSkeleton = null;

		/// <summary>
		/// currently useless
		/// </summary>
		public readonly string AttachingBone = null;

		[GrantedConditionReference]
		[Desc("Condition to grant when equiped.")]
		public readonly string Condition = null;
		public override object Create(ActorInitializer init) { return new AttachManager(init.Self, this); }
	}

	public class AttachManager: RejectsOrders
	{
		readonly Actor self;
		public WithSkeleton MainSkeleton { get; private set; }
		readonly AttachManagerInfo info;

		readonly List<Actor> attachments = new List<Actor>();
		Actor parent;

		public readonly string Condition;
		int conditionToken = Actor.InvalidConditionToken;

		public AttachPoint[] AttachPoints { get; private set; }

		public bool HasParent { get => parent != null; }

		public override bool Rejecting => HasParent;

		public AttachManager(Actor self, AttachManagerInfo info)
			: base(info)
		{
			this.self = self;
			this.info = info;
			Condition = info.Condition;
		}

		protected override void Created(Actor self)
		{
			base.Created(self);
			if (!string.IsNullOrEmpty(info.MainSkeleton))
			{
				MainSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.MainSkeleton);
			}

			AttachPoints = self.TraitsImplementing<AttachPoint>().ToArray();
		}

		void ApplyParent(Actor p)
		{
			if (Condition != null && self != null)
			{
				if (p != null && conditionToken == Actor.InvalidConditionToken)
					conditionToken = self.GrantCondition(Condition);
				else if (p == null && conditionToken != Actor.InvalidConditionToken)
					conditionToken = self.RevokeCondition(conditionToken);
			}

			parent = p;
		}

		public bool AddAttachment(Actor attachment)
		{
			if (IsMyParent(attachment))
				return false;

			attachments.Add(attachment);

			attachment.CancelActivity();
			var am = attachment.TraitOrDefault<AttachManager>();
			if (am != null)
				am.ApplyParent(self);
			return true;
		}

		public bool ReleaseAttachment(Actor attachment)
		{
			if (attachment == null)
				return true;

			attachments.Remove(attachment);

			var am = attachment.TraitOrDefault<AttachManager>();
			if (am != null)
				am.ApplyParent(null);
			return true;
		}

		public bool IsMyParent(Actor actor)
		{
			if (parent == null)
				return false;

			if (parent == actor)
				return true;
			else
			{
				var am = parent.TraitOrDefault<AttachManager>();
				if (am != null)
					return am.IsMyParent(actor);
				else
				{
					Console.WriteLine("Actor " + parent.Info.Name + parent.ActorID + " is the parent of " + self.Info.Name + self.ActorID + " but it has no AttachManager");
					return false;
				}
			}
		}
	}

	public class AttachPointInfo : ConditionalTraitInfo, Requires<AttachManagerInfo>
	{
		public readonly string Name = "point";
		public readonly string Skeleton = null;
		public readonly string BoneAttach = null;

		// or use offset
		public readonly WVec Offset = WVec.Zero;
		public readonly string Turret = null;

		public readonly bool LockFacing = true;

		public readonly bool ConveyAttackAction = true;

		public override object Create(ActorInitializer init) { return new AttachPoint(init.Self, this); }
	}

	public class AttachPoint : ConditionalTrait<AttachPointInfo>, ILaterTick, IIssueOrder, IResolveOrder, INotifyToAttack,
		INotifyKilled
	{
		readonly int attachBoneId = -1;
		public readonly WithSkeleton MainSkeleton;
		public readonly string Name;

		// this actor attached to other's with matrix
		public bool Attached = false;

		readonly Actor self;
		readonly BodyOrientation body;
		IMove move;
		ITurreted turret;

		Actor attachmentActor;
		IPositionable attachmentPositionable;
		IMove attachmentMove;
		IFacing attachmentFacing;
		WithSkeleton attachmentSkeleton;
		AttachManager attachmentAM;
		AttackBase[] attachmentAttackBases;

		readonly AttachManager manager;
		World3DRenderer w3dr;
		public AttachPoint(Actor self, AttachPointInfo info)
			: base(info)
		{
			body = self.Trait<BodyOrientation>();
			Name = info.Name;
			this.self = self;
			if (!string.IsNullOrEmpty(info.Skeleton))
			{
				MainSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.Skeleton);
				attachBoneId = MainSkeleton.GetBoneId(Info.BoneAttach);
				if (attachBoneId == -1)
					throw new Exception("can't find bone " + info.BoneAttach + " in skeleton.");
			}

			manager = self.Trait<AttachManager>();
			w3dr = Game.Renderer.World3DRenderer;
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get
			{
				if (IsTraitDisabled)
					yield break;

				yield return new AttachOrderTargeter();
			}
		}

		public Order IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			if (order.OrderID == "AttachActor")
				return new Order(order.OrderID, self, target, queued);

			return null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "AttachActor" && order.Target.Type == TargetType.Actor)
			{
				AttachActor(order.Target.Actor);
			}
		}

		void ILaterTick.LaterTick(Actor self)
		{
			UpdateAttachment(false);
		}

		public void UpdateAttachment(bool callByParent)
		{
			if (!callByParent && manager.HasParent)
				return;

			if (attachmentActor != null && !attachmentActor.IsDead && attachmentActor.IsInWorld)
			{
				TickAttach();

				foreach (var atpoint in attachmentAM.AttachPoints)
				{
					atpoint.UpdateAttachment(true);
				}
			}
			else
			{
				FlushAttachmentTrits();
			}
		}

		protected override void Created(Actor self)
		{
			base.Created(self);
			turret = self.TraitsImplementing<ITurreted>().FirstOrDefault(t => t.Name == Info.Turret);
			move = self.TraitOrDefault<IMove>();
		}

		public bool AttachActor(Actor target)
		{
			if (target == null || target.IsDead || !target.IsInWorld)
				return false;

			ReleaseAttach();

			if (!manager.AddAttachment(target))
				return false;

			attachmentActor = target;
			attachmentPositionable = attachmentActor.TraitOrDefault<IPositionable>();
			attachmentMove = attachmentActor.TraitOrDefault<IMove>();
			attachmentFacing = attachmentActor.TraitOrDefault<IFacing>();
			attachmentAM = attachmentActor.TraitOrDefault<AttachManager>();
			attachmentAttackBases = attachmentActor.TraitsImplementing<AttackBase>().ToArray();
			if (attachmentAM != null && MainSkeleton != null)
			{
				attachmentSkeleton = attachmentAM.MainSkeleton;
				attachmentSkeleton?.SetParent(MainSkeleton, attachBoneId, attachmentSkeleton.Scale);
			}

			if (attachmentPositionable != null)
			{
				if (attachmentMove is Mobile)
				{
					(attachmentMove as Mobile).RemoveInfluence();
					(attachmentMove as Mobile).TerrainOrientationIgnore = true;
					(attachmentMove as Mobile).ForceDisabled = true;
				}
				else if ((attachmentMove is Aircraft))
				{
					(attachmentMove as Aircraft).RemoveInfluence();
					(attachmentMove as Aircraft).ForceDisabled = true;
				}

				attachmentMove.SteadyBinding = true;
				attachmentPositionable.OccupySpace = false;
			}

			return true;
		}

		void TickAttach()
		{
			if (MainSkeleton != null)
			{
				var mat = MainSkeleton.GetMatrixFromBoneId(attachBoneId);
				var pos = World3DCoordinate.TSVec3ToWPos(Transformation.MatPosition(mat));
				var rot = World3DCoordinate.GetWRotFromBoneMatrix(mat);
				attachmentPositionable?.SetPosition(attachmentActor, pos, true);
				if (attachmentFacing != null)
				{
					if (Info.LockFacing)
					{
						attachmentFacing.Orientation = rot;
					}
				}
			}
			else
			{
				var localOffset = Info.Offset;
				var bodyOrientation = body.QuantizeOrientation(self.Orientation);
				var pos = self.CenterPosition;
				if (turret != null)
				{
					localOffset = localOffset.Rotate(turret.WorldOrientation) + turret.Offset.Rotate(bodyOrientation);
					pos += body.LocalToWorld(localOffset);
					attachmentPositionable?.SetPosition(attachmentActor, pos, true);
					if (attachmentFacing != null)
					{
						if (Info.LockFacing)
						{
							attachmentFacing.Orientation = turret.WorldOrientation;
						}
					}
				}
				else
				{
					localOffset = localOffset.Rotate(bodyOrientation);

					pos += body.LocalToWorld(localOffset);
					attachmentPositionable?.SetPosition(attachmentActor, pos, true);
					if (attachmentFacing != null)
					{
						if (Info.LockFacing)
						{
							attachmentFacing.Orientation = self.Orientation;
						}
					}
				}
			}

		}

		void ReleaseAttach()
		{
			if (attachmentActor == null || attachmentActor.IsDead || !attachmentActor.IsInWorld)
			{
				FlushAttachmentTrits();
				return;
			}

			if (manager.ReleaseAttachment(attachmentActor))
			{
				TickAttach();

				if (attachmentPositionable != null)
				{
					if (attachmentMove is Mobile)
					{
						attachmentFacing.Orientation = WRot.None;
						attachmentFacing.Facing = attachmentFacing.Facing;
						(attachmentMove as Mobile).ForceDisabled = false;
						(attachmentMove as Mobile).TerrainOrientationIgnore = false;
					}
					else if ((attachmentMove is Aircraft))
					{
						(attachmentMove as Aircraft).ForceDisabled = false;
					}

					attachmentMove.SteadyBinding = false;
					attachmentPositionable.OccupySpace = true;
				}

				if (attachmentSkeleton != null)
				{
					attachmentSkeleton.ReleaseFromParent();
				}

				attachmentActor.CancelActivity();

				if (attachmentActor.World.Map.DistanceAboveTerrain(attachmentActor.CenterPosition) > WDist.Zero)
				{
					var fall = new FallDown(attachmentActor, attachmentActor.CenterPosition, 0, true);
					fall.BaseVelocity = move != null ? move.CurrentVelocity : WVec.Zero;
					fall.BrutalLand = self.IsDead;
					attachmentActor.QueueActivity(fall);

					// attachmentActor.QueueActivity(new Parachute(attachmentActor));
				}

				FlushAttachmentTrits();
			}
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			ReleaseAttach();
		}

		void FlushAttachmentTrits()
		{
			attachmentActor = null;
			attachmentMove = null;
			attachmentPositionable = null;
			attachmentFacing = null;
			attachmentAM = null;
			attachmentSkeleton = null;
			attachmentAttackBases = null;
		}

		void INotifyToAttack.ToAttack(in Target target, AttackSource source, bool queued, bool allowMove, bool forceAttack, Color? targetLineColor)
		{
			if (!Info.ConveyAttackAction)
				return;

			if (attachmentActor == null || attachmentActor.IsDead || !attachmentActor.IsInWorld)
			{
				FlushAttachmentTrits();
				return;
			}

			if (attachmentAttackBases != null && attachmentAttackBases.Length > 0)
			{
				foreach (var attack in attachmentAttackBases)
				{
					attack.AttackTarget(target, source, queued, false, forceAttack, null);
				}
			}
		}

		void INotifyToAttack.OnStopOrder(Actor self)
		{
			if (!Info.ConveyAttackAction)
				return;

			if (attachmentActor == null || attachmentActor.IsDead || !attachmentActor.IsInWorld)
			{
				FlushAttachmentTrits();
				return;
			}

			if (attachmentAttackBases != null && attachmentAttackBases.Length > 0)
			{
				foreach (var attack in attachmentAttackBases)
				{
					attack.OnStopOrder(self);
				}
			}

			attachmentActor.CancelActivity();
		}

	}

	class AttachOrderTargeter : IOrderTargeter
	{
		public string OrderID => "AttachActor";
		public int OrderPriority => 7;
		public bool IsQueued { get; protected set; }
		public bool TargetOverridesSelection(Actor self, in Target target, List<Actor> actorsAt, CPos xy, TargetModifiers modifiers) { return true; }

		public bool CanTarget(Actor self, in Target target, ref TargetModifiers modifiers, ref string cursor)
		{
			if (!modifiers.HasModifier(TargetModifiers.ForceMove) ||
				target.Type != TargetType.Actor ||
				target.Actor == null || target.Actor.IsDead || !target.Actor.IsInWorld || target.Actor == self ||
				target.Actor.TraitOrDefault<AttachManager>() == null)
				return false;

			var xy = self.World.Map.CellContaining(target.CenterPosition);
			IsQueued = modifiers.HasModifier(TargetModifiers.ForceQueue);
			if (self.IsInWorld && self.Owner.Shroud.IsExplored(xy))
			{
				return true;
			}

			return false;
		}
	}
}
