﻿#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

using OpenRA.FileSystem;
using OpenRA.Network;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic.CampaignLogic
{
	public class CampaignFactionLogic
	{		
		readonly Action onStart;
		readonly VqaPlayerWidget videoPlayer;
		readonly BackgroundWidget chooseTextBg;
		readonly float cachedMusicVolume;
		bool videoStopped = false;
		bool campaignStarted = false;
		string startedCampaign;

		enum PlayThen
		{
			Replay,
			GDI,
			NODpre,
			chooseNOD,
			Start
		}

		PlayThen playThen = PlayThen.Replay;

		[ObjectCreator.UseCtor]
		public CampaignFactionLogic(Widget widget, Action onStart, Action onExit)
		{			
			this.onStart = onStart;

			var factionList = CampaignProgress.factions;
			var buttonList = new List<ButtonWidget>();

			int i = 0;
			foreach (var f in factionList)
			{
				buttonList.Add(widget.Get<ButtonWidget>(f));
				buttonList[i].OnClick = () => CallbackFactionButtonOnClick(f);
				i++;
			}
			var videoBGPlayer = widget.Get<VqaPlayerWidget>("VIDEO_BG");

			this.videoPlayer = widget.Get<VqaPlayerWidget>("VIDEO");

			// Mute other distracting sounds
			cachedMusicVolume = Sound.MusicVolume;
			Sound.MusicVolume = 0;

			// Hide choose text, if faction is selected
			chooseTextBg = widget.Get<BackgroundWidget>("CHOOSE_TEXT_BG");
			chooseTextBg.Visible = false;

			videoBGPlayer.Load("choose.vqa");
			videoPlayer.Load("choose.vqa");
			videoPlayer.PlayThen(PlayThenMethod);

			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				if (playThen == PlayThen.Replay)
				{
					StopVideo();
					Game.Disconnect();
					Ui.CloseWindow();
					onExit();
				}
				else
				{
					StopVideo();
					if ((playThen != PlayThen.Start || playThen != PlayThen.GDI) && !campaignStarted)
						StartCampaign(startedCampaign);
				}
			};
		}

		void PlayThenMethod()
		{
			if (!videoStopped)
			{
				var filename = "";
				switch (playThen)
				{
					case PlayThen.Replay:
						break;
					case PlayThen.GDI:
						playThen = PlayThen.Start;
						break;
					case PlayThen.chooseNOD:
						playThen = PlayThen.NODpre;
						filename = "choosenod.vqa";
						if (GlobalFileSystem.Exists(filename))
							videoPlayer.Load(filename);
						break;
					case PlayThen.NODpre:
						filename = "nod1pre.vqa";
						if (GlobalFileSystem.Exists(filename))
							videoPlayer.Load(filename);
						chooseTextBg.Visible = true;
						playThen = PlayThen.Start;
						break;
					case PlayThen.Start:
						chooseTextBg.Visible = true;
						StartCampaign(startedCampaign);
						return;
				}

				videoPlayer.PlayThen(PlayThenMethod);
			}
		}

		void CallbackFactionButtonOnClick(string faction)
		{
			
			CampaignProgress.SaveProgress(faction, "");
			startedCampaign = faction + " Campaign";
			Sound.Play("gdiselected.wav");
			playThen = PlayThen.GDI;
		}

		void StopVideo()
		{
			Sound.MusicVolume = cachedMusicVolume;
			videoPlayer.Stop();
			videoStopped = true;
		}

		Map DetectFirstMapFromFaction(string faction)
		{
			var yaml = Game.ModData.Manifest.Missions.Select(MiniYaml.FromFile).Aggregate(MiniYaml.MergeLiberal);

			var allMaps = new List<Map>();
			foreach (var kv in yaml)
			{
				var missionMapPaths = kv.Value.Nodes.Select(n => Path.GetFullPath(n.Key));

				if (faction.Equals(kv.Key))
				{
					var maps = Game.ModData.MapCache
						.Where(p => p.Status == MapStatus.Available && missionMapPaths.Contains(Path.GetFullPath(p.Map.Path)))
						.Select(p => p.Map);
					allMaps.AddRange(maps);
				}
			}

			return allMaps.First();
		}

		void StartCampaign(string faction)
		{
			campaignStarted = true;
			OrderManager om = null;

			Action lobbyReady = null;
			lobbyReady = () =>
			{
				Game.LobbyInfoChanged -= lobbyReady;
				onStart();
				om.IssueOrder(Order.Command("state {0}".F(Session.ClientState.Ready)));
			};
			Game.LobbyInfoChanged += lobbyReady;

			var firstMapFromFaction = this.DetectFirstMapFromFaction(faction);
			var selectedMapPreview = Game.ModData.MapCache[firstMapFromFaction.Uid];
			var video = selectedMapPreview.Map.Videos.Briefing;
			CampaignWorldLogic.Campaign = startedCampaign;

			CampaignProgress.SetPlayedMission(firstMapFromFaction.Path.Split('\\').Last());

			if (GlobalFileSystem.Exists(video))
			{
				videoPlayer.Load(video);
				videoPlayer.PlayThen(() => { StopVideo(); om = Game.JoinServer(IPAddress.Loopback.ToString(), Game.CreateLocalServer(firstMapFromFaction.Uid), "", false); });
			}
			else
			{
				StopVideo();
				om = Game.JoinServer(IPAddress.Loopback.ToString(), Game.CreateLocalServer(firstMapFromFaction.Uid), "", false);
			}
		}
	}
}