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

		public object Create(ActorInitializer init) { return new ModularAI(init, this); }
	}

	public interface IModularAI
	{
		string Name { get; }
		void Tick(Actor self);
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
		public Actor MainBaseBuilding { get; protected set; }
		public Player Player { get; private set; }

		public IEnumerable<Actor> Idlers { get; private set; }

		readonly World world;
		readonly ModularAIInfo info;

		bool botEnabled;
		int updateWaitCountdown;

		List<IModularAI> modules;

		public ModularAI(ActorInitializer init, ModularAIInfo info)
		{
			world = init.World;
			this.info = info;
			modules = new List<IModularAI>();
		}

		public void SetMainBase(Actor mainBase)
		{
			MainBaseBuilding = mainBase;
		}

		public bool RegisterModule(IModularAI module)
		{
			if (modules.Contains(module))
				return false;

			modules.Add(module);
			return true;
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

			foreach (var mod in modules)
				mod.Tick(self);
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

		public void Debug(string message, params object[] fmt)
		{
			if (!botEnabled)
				return;

			message = "{0}: {1}".F(OwnerString(Player), message.F(fmt));

			if (Game.Settings.Debug.BotDebug)
				Game.Debug(message);

			Console.WriteLine(message);
		}

		public static string OwnerString(Actor a)
		{
			return OwnerString(a.Owner);
		}

		public static string OwnerString(Player p)
		{
			var n = p.PlayerName;
			return p.IsBot ? n + "_" + p.ClientIndex.ToString()
					: n;
		}
	}
}