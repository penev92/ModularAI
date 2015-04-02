// #region Copyright & License Information
// /*
//  * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
//  * This file is part of OpenRA, which is free software. It is made
//  * available to you under the terms of the GNU General Public License
//  * as published by the Free Software Foundation. For more information,
//  * see COPYING.
//  */
// #endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.AI
{
	public class IdleBaseBuilderAIModuleInfo : ITraitInfo, Requires<ModularAIInfo>
	{
		[Desc("Actor type names. Not deployed factories. Typically MCVs. Must have the `Transforms` trait.")]
		public readonly string[] BaseBuilderTypes = { "mcv" };

		[Desc("Minimum number of cells to put between each base builder before attempting to deploy.")]
		public readonly int BaseExpansionRadius = 5;

		public object Create(ActorInitializer init) { return new IdleBaseBuilderAIModule(init.Self, this); }
	}

	public class IdleBaseBuilderAIModule : IModularAI
	{
		public string Name { get { return "idle-base-builder-manager"; } }

		readonly ModularAI ai;
		readonly World world;
		readonly IdleBaseBuilderAIModuleInfo info;

		IEnumerable<Actor> idleBaseBuilders;
		Actor mainBaseBuilding { get { return ai.MainBaseBuilding; } }
		readonly int expansionRadius;
		CPos? tryGetLatestConyardAtCell;
		uint latestDeployedBaseBuilder;

		public IdleBaseBuilderAIModule(Actor self, IdleBaseBuilderAIModuleInfo info)
		{
			ai = self.Trait<ModularAI>();
			world = self.World;
			this.info = info;
			expansionRadius = info.BaseExpansionRadius;
			ai.RegisterModule(this);
		}

		public bool IsEnabled(Actor self)
		{
			return true;
		}

		public void Tick(Actor self)
		{
			idleBaseBuilders = ai.Idlers.Where(a => info.BaseBuilderTypes.Contains(a.Info.Name));

			foreach (var builder in idleBaseBuilders)
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

					ai.Debug("Try to deploy into {0} at {1}.", deployInto, targetCell);

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

					ai.SetMainBase(atCell.First());
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
	}
}