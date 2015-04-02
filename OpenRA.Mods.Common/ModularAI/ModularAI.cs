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

		[Desc("Number of ticks to wait between updating activities.")]
		public readonly int UpdateDelay = 25 * 2;

		[Desc("Each item in this list is appended with 'attacking-' and queried against their AIQueryable trait.")]
		public readonly string[] AttackingTypes = { };

		// TODO: Possibly move all of the types (attacking, support, base-builder, anti-*) to AIQueryable
		// and have the ai only deal with filtering, issuing orders, and being a state-machine
		// for repairing, power, cash, base management, etc.
		// Essentially cutting out the string mangling (attacking- prefix, and similar).

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

		readonly ModularAIInfo info;
		readonly World world;
		bool botEnabled;

		public ModularAI(ActorInitializer init, ModularAIInfo info)
		{
			world = init.World;
			this.info = info;
		}

		int updateWaitCountdown;
		public void Tick(Actor self)
		{
			if (!botEnabled)
				return;

			if (--updateWaitCountdown > 0)
				return;

			updateWaitCountdown = info.UpdateDelay;

			OrderIdleUnits(self);
		}

		void OrderIdleUnits(Actor self)
		{
			var idles = world.Actors.Where(a =>
				!a.IsDead &&
				a.IsInWorld &&
				a.Owner == self.Owner &&
				a.IsIdle &&
				a.HasTrait<AIQueryable>());

			var attackers = idles.Where(a =>
			{
				// Example:
				// If AttackingTypes contains 'infantry'
				// and AIQueryable.Types contains 'attacking-infantry'
				// we have found a match.

				if (!a.HasTrait<AttackBase>())
					return false;

				var key = "attacking-";

				var types = a.Info.Traits.Get<AIQueryableInfo>().Types;
				if (!types.Any(t => t.StartsWith(key)))
				{
					Debug("{0} has no types starting with " + key, a.Info.Name);
					return false;
				}

				foreach (var type in types.Where(t => t.StartsWith(key)))
				{
					var nonPrefixedType = type.SubstringAfter('-');

					if (string.IsNullOrWhiteSpace(nonPrefixedType))
						throw new YamlException("Bogus AIQueryable type `{0}` on actor type `{1}`.".F(type, a.Info.Name));

					if (info.AttackingTypes.Contains(nonPrefixedType))
						return true;
				}

				return false;
			});

			OrderIdleAttackers(attackers);
		}

		void OrderIdleAttackers(IEnumerable<Actor> attackers)
		{
			foreach (var attacker in attackers)
			{
				var target = RandomTargetableActor(attacker);
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

		// TODO: TargetableTypes
		static Actor RandomTargetableActor(Actor attacker)
		{
			var attack = attacker.Trait<AttackBase>();

			var targets = attacker.World.Actors.Where(a =>
				!a.Owner.IsAlliedWith(attacker.Owner) &&
				attack.HasAnyValidWeapons(Target.FromActor(a)));

			var rand = Game.CosmeticRandom;
			return targets.RandomOrDefault(rand);
		}

		void Debug(string message, params object[] fmt)
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