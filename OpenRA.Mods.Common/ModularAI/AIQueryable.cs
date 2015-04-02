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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public class AIQueryableInfo : ITraitInfo
	{
		[Desc("The attacking category for this actor. You may want to attack a heavily fortified position with certain units." +
		"Examples: anti-infantry, hit-and-run, mechs, ..")]
		public readonly string[] AttackCategories = { };

		[Desc("Can attack actors whose TargetableTypes contains any member of this list.")]
		public readonly string[] AttackableTypes = { };

		[Desc("Can *not* target actors in this list, regardless of AttackableTypes.")]
		public readonly string[] UnattackbleTypes = { };

		[Desc("Can be targeted by actors whose AttackableTypes contains any member of this list.")]
		public readonly string[] TargetableTypes = { "any" };

		public object Create(ActorInitializer init) { return new AIQueryable(init.Self, this); }
	}

	public class AIQueryable
	{
		public readonly AIQueryableInfo Info;

		public AIQueryable(Actor self, AIQueryableInfo info)
		{
			Info = info;
		}
	}
}