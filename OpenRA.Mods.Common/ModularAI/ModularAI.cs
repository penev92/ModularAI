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
using OpenRA.Traits;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Activities;

namespace OpenRA.Mods.Common.AI
{
	public class ModularAIInfo : IBotInfo, ITraitInfo
	{
		[Desc("Display name for this AI.")]
		public readonly string Name = "ModularAI";

		string IBotInfo.Name { get { return Name; } }

		[Desc("Number of ticks to wait between updating activities. Default evaluates to 2 seconds.")]
		public readonly int UpdateDelay = 25 * 2;

		// TODO: The AI should be a state-machine for repairing, power, cash, base management, etc.

		[Desc("Actor type names. Not deployed factories. Typically MCVs. Must have the `Transforms` trait.")]
		public readonly string[] BaseBuilderActorTypes = { "mcv" };

		[Desc("Minimum number of cells to put between each base builder before attempting to deploy.")]
		public readonly int BaseExpansionRadius = 5;

		public object Create(ActorInitializer init) { return new ModularAI(init, this); }
	}

	public class ModularAI : ITick, IBot
	{
		public void Activate(Player p)
		{
			Player = p;
			botEnabled = p.IsBot;
			updateWaitCountdown = info.UpdateDelay;
			Debug("*** Bot {0} Debug ***", info.Name);
		}

		public IBotInfo Info { get { return info; } }
		public Player Player { get; private set; }

		protected IEnumerable<Actor> Idlers;

		readonly World world;
		readonly ModularAIInfo info;
		readonly int expansionRadius;

		bool botEnabled;
		int updateWaitCountdown;

		CPos? tryGetLatestConyardAtCell;
		uint latestDeployedBaseBuilder;
		Actor mainBaseBuilding;

		public ModularAI(ActorInitializer init, ModularAIInfo info)
		{
			world = init.World;
			this.info = info;
			expansionRadius = info.BaseExpansionRadius;
		}

		public void Tick(Actor self)
		{
			if (!botEnabled || Player.WinState == WinState.Lost)
				return;

			// Needs to be called once before handing over to TickInner
			FindIdleUnits(self);

			if (--updateWaitCountdown > 0)
				return;

			updateWaitCountdown = info.UpdateDelay;
			TickInner(self);
		}

		protected virtual void TickInner(Actor self)
		{
			FindIdleUnits(self);

			OrderIdleAttackers();

			var baseBuilders = FindIdleBaseBuilders(self);
			OrderIdleBaseBuilders(baseBuilders);
		}

		protected virtual void FindIdleUnits(Actor self)
		{
			Idlers = world.Actors.Where(a =>
				!a.IsDead &&
				a.IsInWorld &&
				a.Owner == self.Owner &&
				a.IsIdle &&
				a.HasTrait<AIQueryable>());
		}

		protected virtual IEnumerable<Actor> FindIdleBaseBuilders(Actor self)
		{
			return Idlers.Where(a => info.BaseBuilderActorTypes.Contains(a.Info.Name));
		}

		protected virtual void OrderIdleBaseBuilders(IEnumerable<Actor> builders)
		{
			foreach (var builder in builders)
			{
				var tryDeploy = new Order("DeployTransform", builder, true);

				if (mainBaseBuilding != null)
				{
					if (builder.IsMoving())
						continue;

					var transforms = builder.Trait<Transforms>();
					var deployInto = transforms.Info.IntoActor;

					var srcCell = world.Map.CellContaining(builder.CenterPosition);
					var targetCell = world.Map.FindTilesInAnnulus(
						srcCell,
						expansionRadius,
						expansionRadius + expansionRadius / 2 /* TODO: non-random value */)
						.Where(world.Map.Contains)
						.MinBy(c => (c - srcCell).LengthSquared);

					Debug("Try to deploy into {0} at {1}.", deployInto, targetCell);

					var moveToDest = new Order("Move", builder, true)
					{
						TargetLocation = targetCell
					};

					world.AddFrameEndTask(w =>
					{
						w.IssueOrder(moveToDest);
						w.IssueOrder(tryDeploy);
					});

					continue;
				}
				else if (tryGetLatestConyardAtCell.HasValue)
				{
					var atCell = world.ActorMap.GetUnitsAt(tryGetLatestConyardAtCell.Value);
					if (atCell.Count() > 1 || atCell.First().ActorID != latestDeployedBaseBuilder)
					{
						tryGetLatestConyardAtCell = null;
						continue;
					}

					mainBaseBuilding = atCell.First();
					continue;
				}
					
				world.AddFrameEndTask(w =>
				{
					tryDeploy.TargetLocation = world.Map.CellContaining(builder.CenterPosition);
					w.IssueOrder(tryDeploy);
					tryGetLatestConyardAtCell = tryDeploy.TargetLocation;
					latestDeployedBaseBuilder = builder.ActorID;
				});
			}
		}

		protected virtual void OrderIdleAttackers()
		{
			var attackers = Idlers.Where(a => a.HasTrait<AttackBase>());

			foreach (var attacker in attackers)
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

					Debug("{0} to attack {1} ({2})", attacker.Info.Name, target.Info.Name, OwnerString(target));
				});
			}
		}

		protected virtual Actor ClosestTargetableActor(Actor attacker, AIQueryableInfo attackerAIQ)
		{
			var attack = attacker.Trait<AttackBase>();

			var targets = world.Actors.Where(a =>
			{
				if (a.IsDead || !a.IsInWorld)
					return false;

				if (a.AppearsFriendlyTo(attacker))
					return false;

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

		protected void Debug(string message, params object[] fmt)
		{
			if (!botEnabled)
				return;

			message = "{0}: {1}".F(OwnerString(Player), message.F(fmt));

			if (Game.Settings.Debug.BotDebug)
				Game.Debug(message);

			Console.WriteLine(message);
		}

		static string OwnerString(Actor a)
		{
			return OwnerString(a.Owner);
		}

		static string OwnerString(Player p)
		{
			var n = p.PlayerName;
			return p.IsBot ? n + "_" + p.ClientIndex.ToString()
					: n;
		}
	}
}