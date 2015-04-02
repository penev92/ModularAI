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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.AI
{
	public class IdleHarvesterAIModuleInfo : ITraitInfo, Requires<ModularAIInfo>
	{
		[Desc("Actor type names. All of the `Harvester`s to manage.")]
		public readonly string[] HarvesterTypes = { "harv" };

		public object Create(ActorInitializer init) { return new IdleHarvesterAIModule(init.Self, this); }
	}

	public class IdleHarvesterAIModule : IModularAI
	{
		public string Name { get { return "idle-harvester-manager"; } }

		readonly ModularAI ai;
		readonly World world;
		readonly IdleHarvesterAIModuleInfo info;

		IEnumerable<Actor> idleHarvs;

		public IdleHarvesterAIModule(Actor self, IdleHarvesterAIModuleInfo info)
		{
			ai = self.Trait<ModularAI>();
			world = self.World;
			this.info = info;
			ai.RegisterModule(this);
		}

		public void Tick(Actor self)
		{
			idleHarvs = ai.Idlers.Where(a => info.HarvesterTypes.Contains(a.Info.Name));

			foreach (var harv in idleHarvs)
			{
				world.AddFrameEndTask(w =>
				{
					w.IssueOrder(new Order("Harvest", harv, true));
				});
			}
		}
	}
}