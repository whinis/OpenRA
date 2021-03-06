#region Copyright & License Information
/*
 * Copyright 2007-2016 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.FileSystem;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Support;

namespace OpenRA
{
	public sealed class RulesetCache
	{
		static readonly List<MiniYamlNode> NoMapRules = new List<MiniYamlNode>();

		readonly ModData modData;

		readonly Dictionary<string, ActorInfo> actorCache = new Dictionary<string, ActorInfo>();
		readonly Dictionary<string, WeaponInfo> weaponCache = new Dictionary<string, WeaponInfo>();
		readonly Dictionary<string, SoundInfo> voiceCache = new Dictionary<string, SoundInfo>();
		readonly Dictionary<string, SoundInfo> notificationCache = new Dictionary<string, SoundInfo>();
		readonly Dictionary<string, MusicInfo> musicCache = new Dictionary<string, MusicInfo>();
		readonly Dictionary<string, TileSet> tileSetCache = new Dictionary<string, TileSet>();

		public event EventHandler LoadingProgress;
		void RaiseProgress()
		{
			if (LoadingProgress != null)
				LoadingProgress(this, new EventArgs());
		}

		public RulesetCache(ModData modData)
		{
			this.modData = modData;
		}

		/// <summary>
		/// Cache and return the Ruleset for a given map.
		/// If a map isn't specified then return the default mod Ruleset.
		/// </summary>
		public Ruleset Load(IReadOnlyFileSystem fileSystem, Map map = null)
		{
			var m = modData.Manifest;

			Dictionary<string, ActorInfo> actors;
			Dictionary<string, WeaponInfo> weapons;
			Dictionary<string, SoundInfo> voices;
			Dictionary<string, SoundInfo> notifications;
			Dictionary<string, MusicInfo> music;
			Dictionary<string, TileSet> tileSets;

			using (new PerfTimer("Actors"))
				actors = LoadYamlRules(fileSystem, actorCache, m.Rules,
					map != null ? map.RuleDefinitions : NoMapRules,
					k => new ActorInfo(Game.ModData.ObjectCreator, k.Key.ToLowerInvariant(), k.Value));

			using (new PerfTimer("Weapons"))
				weapons = LoadYamlRules(fileSystem, weaponCache, m.Weapons,
					map != null ? map.WeaponDefinitions : NoMapRules,
					k => new WeaponInfo(k.Key.ToLowerInvariant(), k.Value));

			using (new PerfTimer("Voices"))
				voices = LoadYamlRules(fileSystem, voiceCache, m.Voices,
					map != null ? map.VoiceDefinitions : NoMapRules,
					k => new SoundInfo(k.Value));

			using (new PerfTimer("Notifications"))
				notifications = LoadYamlRules(fileSystem, notificationCache, m.Notifications,
					map != null ? map.NotificationDefinitions : NoMapRules,
					k => new SoundInfo(k.Value));

			using (new PerfTimer("Music"))
				music = LoadYamlRules(fileSystem, musicCache, m.Music,
					map != null ? map.MusicDefinitions : NoMapRules,
					k => new MusicInfo(k.Key, k.Value));

			using (new PerfTimer("TileSets"))
				tileSets = LoadTileSets(fileSystem, tileSetCache, m.TileSets);

			// TODO: only initialize, and then cache, the provider for the given map
			var sequences = tileSets.ToDictionary(t => t.Key, t => new SequenceProvider(fileSystem, modData, t.Value, map));
			return new Ruleset(actors, weapons, voices, notifications, music, tileSets, sequences);
		}

		Dictionary<string, T> LoadYamlRules<T>(IReadOnlyFileSystem fileSystem,
			Dictionary<string, T> itemCache,
			string[] files, List<MiniYamlNode> nodes,
			Func<MiniYamlNode, T> f)
		{
			RaiseProgress();

			var inputKey = string.Concat(string.Join("|", files), "|", nodes.WriteToString());
			Func<MiniYamlNode, T> wrap = wkv =>
			{
				var key = inputKey + wkv.Value.ToLines(wkv.Key).JoinWith("|");
				T t;
				if (itemCache.TryGetValue(key, out t))
					return t;

				t = f(wkv);
				itemCache.Add(key, t);

				RaiseProgress();
				return t;
			};

			var tree = MiniYaml.Merge(files.Select(s => MiniYaml.FromStream(fileSystem.Open(s))).Append(nodes))
				.ToDictionaryWithConflictLog(n => n.Key, n => n.Value, "LoadYamlRules", null, null);
			RaiseProgress();

			var itemSet = tree.ToDictionary(kv => kv.Key.ToLowerInvariant(), kv => wrap(new MiniYamlNode(kv.Key, kv.Value)));
			RaiseProgress();
			return itemSet;
		}

		Dictionary<string, TileSet> LoadTileSets(IReadOnlyFileSystem fileSystem, Dictionary<string, TileSet> itemCache, string[] files)
		{
			var items = new Dictionary<string, TileSet>();

			foreach (var file in files)
			{
				TileSet t;
				if (itemCache.TryGetValue(file, out t))
					items.Add(t.Id, t);
				else
				{
					t = new TileSet(fileSystem, file);
					itemCache.Add(file, t);
					items.Add(t.Id, t);
				}
			}

			return items;
		}
	}
}
