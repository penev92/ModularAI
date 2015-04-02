﻿// #region Copyright & License Information
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
		[Desc("infantry, vehicle, aircraft, .. base-defense, anti-vehicle, hero, ..")]
		public readonly string[] Types = { };

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