using System;
using System.Collections.Generic;
using System.IO;
using OpenRA.FileSystem;
using OpenRA.Graphics;

namespace OpenRA.Mods.Common
{
	public class CampaignProgress
	{
		static string progressFile = Platform.ResolvePath("^", "cnc-progress.yaml");
		public static List<Player> players = new List<Player>();
		static bool saveProgressFlag = false;
		static string playedMission = "";

		public static void Init(List<Player> players)
		{
			CampaignProgress.players = players;
			ModMetadata initialMod = null;
			ModMetadata.AllMods.TryGetValue(Game.Settings.Game.Mod, out initialMod);
			var mod = initialMod.Id;
			progressFile = Platform.ResolvePath("^", mod + "-progress.yaml");
		}

		public static void SetPlayedMission(string mission)
		{
			playedMission = mission;
		}

		public static void SetSaveProgressFlag()
		{
			saveProgressFlag = true;
		}

		public static void ResetSaveProgressFlag()
		{
			saveProgressFlag = false;
		}

		public static bool GetSaveProgressFlag()
		{
			return saveProgressFlag;
		}

		public static void SaveProgress(string faction)
		{
			SaveProgress(faction, playedMission);
		}

		public static void SaveProgress(string faction, string mission)
		{
			if (saveProgressFlag)
			{
				if (!GlobalFileSystem.Exists(progressFile))
					CreateProgressFile();
				var yaml = MiniYaml.FromFile(progressFile);
				foreach (var kv in yaml)
				{
					if (kv.Key.Equals(faction))
					{
						foreach (var node in kv.Value.Nodes)
						{
							if (node.Key.Equals("Mission"))
								node.Value.Value = mission.ToString();
						}
					}
				}

				yaml.WriteToFile(progressFile);
			}
		}

		public static string GetMission(string faction)
		{
			if (!GlobalFileSystem.Exists(progressFile))
				CreateProgressFile();
			var yaml = MiniYaml.FromFile(progressFile);
			var mission = "";
			foreach (var kv in yaml)
			{
				if (kv.Key.Equals(faction))
				{
					foreach (var node in kv.Value.Nodes)
					{
						if (node.Key.Equals("Mission"))
							mission = node.Value.Value;
					}
				}
			}

			return (mission == null) ? "" : mission;
		}

		static void CreateProgressFile()
		{
			var yaml = new List<MiniYamlNode>();
			var mission = new MiniYamlNode("Mission", "");
			var factionList = new List<String>();

			foreach (var p in players)
			{
				if (!factionList.Contains(p.Faction.Name))
				{
					factionList.Add(p.Faction.Name);

					var nodes = new List<MiniYamlNode>();
					nodes.Add(mission);

					var faction = new MiniYaml(null, nodes);
					var node = new MiniYamlNode(p.Faction.Name, faction);

					yaml.Add(node);
				}
			}

			yaml.WriteToFile(progressFile);
		}
	}
}