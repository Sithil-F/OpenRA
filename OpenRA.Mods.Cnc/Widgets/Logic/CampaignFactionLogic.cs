#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

namespace OpenRA.Mods.RA.Widgets.Logic
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;

    using OpenRA.Network;
    using OpenRA.Widgets;
    using OpenRA.Mods.Cnc;

    public class CampaignFactionLogic
    {
        readonly Action onStart;
        VqaPlayerWidget videoPlayer;
        bool videoStopped = false;
        bool campaignStarted = false;
        float cachedMusicVolume;
        string gdiCampaign = "GDI Campaign";
        string nodCampaign = "Nod Campaign";
        string startedCampaign;
        BackgroundWidget chooseTextBg;

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

            var gdiButton = widget.Get<ButtonWidget>("GDI_FACTION");
            var nodButton = widget.Get<ButtonWidget>("NOD_FACTION");
            videoPlayer = widget.Get<VqaPlayerWidget>("VIDEO");
            var videoBGPlayer = widget.Get<VqaPlayerWidget>("VIDEO_BG");

            // Mute other distracting sounds
            cachedMusicVolume = Sound.MusicVolume;
            Sound.MusicVolume = 0;
            
            //hide choose text, if faction is selected
            chooseTextBg = widget.Get<BackgroundWidget>("CHOOSE_TEXT_BG");
            chooseTextBg.Visible = false;

            videoBGPlayer.Load("choose.vqa");
            videoPlayer.Load("choose.vqa");
            videoPlayer.PlayThen(PlayThenMethod);

            gdiButton.OnClick = () =>
            {
                CampaignProgress.saveProgress("GDI", 0);
                startedCampaign = gdiCampaign;
                Sound.Play("gdiselected.wav");
                playThen = PlayThen.GDI;
            };

            nodButton.OnClick = () =>
            {
                CampaignProgress.saveProgress("Nod", 0);
                startedCampaign = nodCampaign;
                Sound.Play("brotherhoodofnodselected.wav");
                playThen = PlayThen.chooseNOD;
            };

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
                    {
                        StartCampaign(startedCampaign);
                    }
                }
            };
        }

        void PlayThenMethod()
        {
            if (!videoStopped)
            {
                switch (playThen)
                {
                    case PlayThen.Replay:
                        break;
                    case PlayThen.GDI:
                        playThen = PlayThen.Start;
                        break;
                    case PlayThen.chooseNOD:
                        playThen = PlayThen.NODpre;
                         try
                        {
                            videoPlayer.Load("choosenod.vqa");
                        }
                         catch (FileNotFoundException) { }
                        break;
                    case PlayThen.NODpre:
                        try
                        {
                            videoPlayer.Load("nod1pre.vqa");
                        }
                        catch (FileNotFoundException) { }
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

        void StopVideo()
        {
            Sound.MusicVolume = cachedMusicVolume;
            videoPlayer.Stop();
            videoStopped = true;
        }


        Map DetectFirstMapFromFaction(string faction)
        {
            var yaml = Game.ModData.Manifest.Missions.Select(MiniYaml.FromFile).Aggregate(MiniYaml.MergeLiberal);
            Map map = null;

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

            map = allMaps.First();

            return map;
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

            string firstMapFromFaction = this.DetectFirstMapFromFaction(faction).Uid;
            MapPreview selectedMapPreview = Game.ModData.MapCache[firstMapFromFaction];
            var video = selectedMapPreview.Map.PreviewVideo;
            if (video != null)
            {
                try
                {
                    videoPlayer.Load(video);
                    videoPlayer.PlayThen(() => { StopVideo(); om = Game.JoinServer(IPAddress.Loopback.ToString(), Game.CreateLocalServer(firstMapFromFaction), "", false); });
                }
                catch (FileNotFoundException) 
                {
                    StopVideo();
                    om = Game.JoinServer(IPAddress.Loopback.ToString(), Game.CreateLocalServer(firstMapFromFaction), "", false);
                }
            }
            else
            {
                StopVideo();
                om = Game.JoinServer(IPAddress.Loopback.ToString(), Game.CreateLocalServer(firstMapFromFaction), "", false);
            }
        }
    }
}
