// #region Copyright & License Information
// /*
//  * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
//  * This file is part of OpenRA, which is free software. It is made
//  * available to you under the terms of the GNU General Public License
//  * as published by the Free Software Foundation. For more information,
//  * see COPYING.
//  */
// #endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.AI
{
	public class AttackingAIModuleInfo : ITraitInfo, Requires<ModularAIInfo>
	{
		[Desc("Use `AIQueryable`s with `AttackingCategories` contained in this list. Leave empty for 'any'.")]
		public readonly string[] UseAttackingCategories = { "any" };

		public object Create(ActorInitializer init) { return new AttackingAIModule(init.Self, this); }
	}

	public class AttackingAIModule : IModularAI
	{
		public string Name { get { return "attacking-module"; } }

		readonly ModularAI ai;
		readonly World world;
		readonly AttackingAIModuleInfo info;

		IEnumerable<Actor> idleAttackers;

		public AttackingAIModule(Actor self, AttackingAIModuleInfo info)
		{
			ai = self.Trait<ModularAI>();
			world = self.World;
			this.info = info;
			ai.RegisterModule(this);
		}

		public bool IsEnabled(Actor self)
		{
			return true;
		}

		public void Tick(Actor self)
		{
			idleAttackers = ai.Idlers.Where(a =>
			{
				var aiq = a.Info.Traits.Get<AIQueryableInfo>();
				if (!info.UseAttackingCategories.Intersect(aiq.AttackableTypes).Any())
					return false;

				return a.HasTrait<AttackBase>();
			});

			foreach (var attacker in idleAttackers)
			{
				var target = ClosestTargetableActor(attacker, attacker.Info.Traits.Get<AIQueryableInfo>());
				if (target == null || target.IsDead || !target.IsInWorld)
					continue;

				world.AddFrameEndTask(w =>
				{
					w.IssueOrder(new Order("Attack", attacker, false)
					{
						TargetActor = target
					});

					ai.Debug("{0} to attack {1} ({2})", attacker.Info.Name, target.Info.Name,
						ModularAI.OwnerString(target));
				});
			}
		}

		protected virtual Actor ClosestTargetableActor(Actor attacker, AIQueryableInfo attackerAIQ)
		{
			var attack = attacker.Trait<AttackBase>();

			var targets = world.Actors.Where(a =>
			{
				if (a == null || a.IsDead || !a.IsInWorld)
					return false;

				var position = a.TraitOrDefault<IPositionable>();
				if (position == null)
					return false;

				if (a.AppearsFriendlyTo(attacker))
					return false;

				if (!attacker.Owner.Shroud.IsExplored(a))
					return false;

				/* TODO: Handle frozen actors
				if (world.FogObscures(a))
					test if exist,
					if not get frozen actor data,
					determine if we should try to attack it
				*/

				if (!a.HasTrait<TargetableUnit>())
					return false;

				var aiq = a.Info.Traits.GetOrDefault<AIQueryableInfo>();
				if (aiq == null)
					return false;

				var typesMatch = attackerAIQ.AttackableTypes.Intersect(aiq.TargetableTypes).Any();
				if (!typesMatch)
					return false;

				return attack.HasAnyValidWeapons(Target.FromActor(a));
			});

			return targets.ClosestTo(attacker);
		}
	}
}