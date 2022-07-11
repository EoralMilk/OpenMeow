using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Warheads
{
	public class EffectSystemWarhead : Warhead
	{
		[SequenceReference(nameof(Image), dictionaryReference: LintDictionaryReference.Values)]
		public readonly Dictionary<string, string[]> BlastEffectSequences = new Dictionary<string, string[]>();

		[SequenceReference(nameof(Image), dictionaryReference: LintDictionaryReference.Values)]
		public readonly Dictionary<string, string[]> SmokeEffectSequences = new Dictionary<string, string[]>();

		public readonly Dictionary<string, string[]> ImpactSounds = new Dictionary<string, string[]>();

		[Desc("Image containing explosion effect sequence.")]
		public readonly string Image = "explosion";

		[PaletteReference(nameof(UsePlayerPalette))]
		[Desc("Palette to use for explosion effect.")]
		public readonly string ExplosionPalette = "Blast";

		[Desc("Image containing explosion effect sequence.")]
		public readonly string SmokesImage = "explosion";

		[PaletteReference(nameof(UsePlayerPalette))]
		[Desc("Palette to use for explosion effect.")]
		public readonly string SmokesPalette = "BlastSmoke";

		[Desc("Remap explosion effect to player color, if art supports it.")]
		public readonly bool UsePlayerPalette = false;

		[Desc("Display explosion effect at ground level, regardless of explosion altitude.")]
		public readonly bool ForceDisplayAtGroundLevel = false;

		[Desc("Chance of impact sound to play.")]
		public readonly int ImpactSoundChance = 100;

		[Desc("Whether to consider actors in determining whether the explosion should happen. If false, only terrain will be considered.")]
		public readonly bool ImpactActors = true;

		[Desc("Whether to consider actors in determining whether the explosion should happen. If false, only terrain will be considered.")]
		public readonly bool AvoidInvalidActors = false;

		[Desc("Whether to consider blocker")]
		public readonly bool ImpactBlocker = true;

		[Desc("The maximum inaccuracy of the effect spawn position relative to actual impact position.")]
		public readonly WDist Inaccuracy = WDist.Zero;

		[Desc("If the impact point is less than zero, force the check of the impact point to zero ground level ")]
		public bool ForceUnderGroundHitToSurface = true;

		static readonly BitSet<TargetableType> TargetTypeAir = new BitSet<TargetableType>("Air");

		string[] seqsA;
		string[] seqsB;
		string[] sounds;

		/// <summary>Checks if there are any actors at impact position and if the warhead is valid against any of them.</summary>
		ImpactActorType ActorTypeAtImpact(World world, WPos pos, Actor firedBy, WarheadArgs args)
		{
			var anyInvalidActor = false;

			if (ImpactBlocker && args.Blocker != null && args.Blocker.IsInWorld && !args.Blocker.IsDead)
			{
				if (IsValidAgainst(args.Blocker, firedBy))
					return ImpactActorType.Valid;

				anyInvalidActor = true;

				return anyInvalidActor ? ImpactActorType.Invalid : ImpactActorType.None;
			}

			// Check whether the impact position overlaps with an actor's hitshape
			foreach (var victim in world.FindActorsOnCircle(pos, WDist.Zero))
			{
				if (!AffectsParent && victim == firedBy)
					continue;

				var activeShapes = victim.TraitsImplementing<HitShape>().Where(Exts.IsTraitEnabled);
				if (!activeShapes.Any(s => s.DistanceFromEdge(victim, pos).Length <= 0))
					continue;

				if (IsValidAgainst(victim, firedBy))
					return ImpactActorType.Valid;

				anyInvalidActor = true;
			}

			return anyInvalidActor ? ImpactActorType.Invalid : ImpactActorType.None;
		}

		// ActorTypeAtImpact already checks AffectsParent beforehand, to avoid parent HitShape look-ups
		// (and to prevent returning ImpactActorType.Invalid on AffectsParent=false)
		public override bool IsValidAgainst(Actor victim, Actor firedBy)
		{
			var relationship = firedBy.Owner.RelationshipWith(victim.Owner);
			if (!ValidRelationships.HasRelationship(relationship))
				return false;

			if (!IsValidTarget(victim.GetEnabledTargetTypes()))
				return false;

			foreach (var tt in victim.GetEnabledTargetTypes())
			{
				if (sounds == null)
					ImpactSounds.TryGetValue(tt, out sounds);
				if (seqsA == null)
					BlastEffectSequences.TryGetValue(tt, out seqsA);
				if (seqsB == null)
					SmokeEffectSequences.TryGetValue(tt, out seqsB);

				if (sounds != null && seqsA != null && seqsB != null)
					return true;
			}

			return false;
		}

		public override void DoImpact(in Target target, WarheadArgs args)
		{
			if (target.Type == TargetType.Invalid)
				return;

			// 弹头的某些变量应该在doImpact进行强制初始化
			// Some variables of the warhead should be forcibly initialized in doImpact
			seqsA = null;
			seqsB = null;
			sounds = null;

			var firedBy = args.SourceActor;
			var pos = target.CenterPosition;
			var world = firedBy.World;
			var vpos = new WPos(pos.X, pos.Y, pos.Z - firedBy.World.Map.DistanceAboveTerrain(pos).Length);
			ImpactActorType actorAtImpact;
			if (ForceUnderGroundHitToSurface && firedBy.World.Map.DistanceAboveTerrain(pos) < WDist.Zero)
				actorAtImpact = ImpactActors ? ActorTypeAtImpact(world, vpos, firedBy, args) : ImpactActorType.None;
			else
				actorAtImpact = ImpactActors ? ActorTypeAtImpact(world, pos, firedBy, args) : ImpactActorType.None;

			// 如果范围内没有目标actor的话，应该再判断地形是否适合
			// Some variables of the warhead should be forcibly initialized in doImpact and if there is no target actor in range, the terrain should be determined
			if (actorAtImpact == ImpactActorType.Invalid)
			{
				if (AvoidInvalidActors)
					return;

				if (!IsValidAgainstTerrain(world, pos))
					return;
			}
			else if (actorAtImpact == ImpactActorType.None && !IsValidAgainstTerrain(world, pos))
				return;

			if (sounds != null)
			{
				var impactSound = sounds.RandomOrDefault(world.LocalRandom);
				if (impactSound != null && world.LocalRandom.Next(0, 100) < ImpactSoundChance)
					Game.Sound.Play(SoundType.World, impactSound, pos);
			}

			// if no sequence, skip
			if (seqsA == null || seqsB == null)
				return;

			if (seqsA.Length == 0 && seqsB.Length == 0)
				return;

			if (seqsA.Length != seqsB.Length)
				throw new YamlException("The sequence number of EffectA must be the same as that of EffectB  ");

			var random = args.SourceActor.World.SharedRandom.Next(seqsA.Length - 1);

			var explosion = seqsA[random];
			var smoke = seqsB[random];
			if (Image != null && explosion != null && SmokesImage != null && smoke != null)
			{
				if (Inaccuracy.Length > 0)
					pos += WVec.FromPDF(world.SharedRandom, 2) * Inaccuracy.Length / 1024;

				if (ForceDisplayAtGroundLevel)
				{
					var dat = world.Map.DistanceAboveTerrain(pos);
					pos -= new WVec(0, 0, dat.Length);
				}

				var explopalette = ExplosionPalette;
				if (UsePlayerPalette)
					explopalette += firedBy.Owner.InternalName;

				world.AddFrameEndTask(w => w.Add(new SpriteEffect(pos, w, Image, explosion, explopalette)));

				var smokepalette = SmokesPalette;
				if (UsePlayerPalette)
					smokepalette += firedBy.Owner.InternalName;

				world.AddFrameEndTask(w => w.Add(new SpriteEffect(pos, w, SmokesImage, smoke, smokepalette)));
			}
			else
				throw new YamlException("There is no valid sequence or image to play");
		}

		/// <summary>Checks if the warhead is valid against the terrain at impact position.</summary>
		bool IsValidAgainstTerrain(World world, WPos pos)
		{
			var cell = world.Map.CellContaining(pos);
			if (!world.Map.Contains(cell))
				return false;
			var dat = world.Map.DistanceAboveTerrain(pos);
			var tts = world.Map.GetTerrainInfo(cell).TargetTypes;
			if (dat > AirThreshold)
				tts = TargetTypeAir;

			if (IsValidTarget(tts))
			{

				foreach (var tt in tts)
				{
					if (sounds == null)
						ImpactSounds.TryGetValue(tt, out sounds);
					if (seqsA == null)
						BlastEffectSequences.TryGetValue(tt, out seqsA);
					if (seqsB == null)
						SmokeEffectSequences.TryGetValue(tt, out seqsB);

					if (sounds != null && seqsA != null && seqsB != null)
						return true;
				}

				if (sounds != null || seqsA != null || seqsB != null)
					return true;
				else
					return false;
			}

			return false;
		}
	}
}
