#region Copyright & License Information
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
using OpenRA.Graphics;
using OpenRA.Mods.Cnc;
using OpenRA.Network;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA.Widgets.Logic
{
    public class CampaignWorldLogic
    {
        public static string Campaign = "";

        static string map = "";
        static bool lastMissionSuccessfullyPlayedGDI = false;
        static bool lastMissionSuccessfullyPlayedNod = false;

        readonly MapPreviewWidget campaignPreviewWidget, missionPreviewWidget;
        readonly ButtonWidget campaignBrowserBackButton, playButton, nextButton,
                                    prevButton, campaignPreviewContinueButton, campaignPreviewGraficButton, missionPreviewGraficButton, campaignCongratulationContinueButton;
        readonly LabelWidget missionTitle;
        readonly LabelWidget worldMenuTitle, previewMenuTitle;
        readonly ScrollPanelWidget missionDescriptionPanel;
        readonly LabelWidget missionDescription;
        readonly SpriteFont missionDescriptionFont;
        readonly ScrollPanelWidget countryDescriptionPanel;
        readonly LabelWidget countryDescriptionHeader, countryDescriptionValues;
        readonly SpriteFont countryDescriptionFont;
        readonly ContainerWidget campaignBrowser, campaignPreview, campaignCongratulation;
        readonly Action onStart;
        readonly LabelWidget congratulationText;
        readonly ScrollPanelWidget congratulationTextPanel;
        readonly SpriteFont congratulationTextFont;
        readonly ContainerWidget congratulationNodLogo, congratulationGdiLogo;

        VqaPlayerWidget videoPlayer;
        BackgroundWidget videoBackground;

        bool congratsFlag = false;
        float cachedMusicVolume;
        int nextMission = 1;
        int mapIndex = 0;
        string faction = "";
        int progress;
        List<Map> factionMaps = new List<Map>();  // all maps of a faction
        List<Map> nextMaps = new List<Map>();     // all actual playable maps

        MapPreview selectedMapPreview;

        bool videoStopped = false;
        bool campaignPreviewRequired = false;

        Map nextMap;

        [ObjectCreator.UseCtor]
        public CampaignWorldLogic(Widget widget, Action onStart, Action onExit)
        {
            this.onStart = onStart;

            campaignBrowser = widget.Get<ContainerWidget>("CAMPAIGN_BROWSER");
            videoBackground = widget.Get<BackgroundWidget>("VIDEO_BG");
            videoBackground.IsVisible = () => false;
            worldMenuTitle = widget.Get<LabelWidget>("CAMPAIGN_MENU_TITLE");

            cachedMusicVolume = Sound.MusicVolume;
            Sound.MusicVolume = 0;

            // preview label
            previewMenuTitle = widget.Get<LabelWidget>("PREVIEW_MENU_TITLE");

            // map grafic
            widget.Get("CAMPAIGN_BROWSER_GRAFIC_CONTAINER").IsVisible = () => selectedMapPreview != null;

            missionPreviewWidget = widget.Get<MapPreviewWidget>("MISSION_PREVIEW");
            missionPreviewWidget.Preview = () => selectedMapPreview;

            missionPreviewGraficButton = widget.Get<ButtonWidget>("MISSION_PREVIEW_BUTTON");
            missionPreviewGraficButton.OnClick = () =>
            {
                campaignBrowser.IsVisible = () => false;
                videoBackground.IsVisible = () => true;
                PlayAndStart();
            };

            if (Game.ModData.Manifest.Missions.Any())
            {
                var yaml = Game.ModData.Manifest.Missions.Select(MiniYaml.FromFile).Aggregate(MiniYaml.MergeLiberal);

                foreach (var kv in yaml)
                {
                    var missionMapPaths = kv.Value.Nodes.Select(n => Path.GetFullPath(n.Key));

                    var maps = Game.ModData.MapCache    // maps of faction
                        .Where(p => p.Status == MapStatus.Available && missionMapPaths.Contains(Path.GetFullPath(p.Map.Path)))
                        .Select(p => p.Map);

                    if (kv.Key.Equals(Campaign))
                    {
                        factionMaps.AddRange(maps);     // factionMaps = all GDI maps
                        worldMenuTitle.Text = kv.Key.ToString();
                        previewMenuTitle.Text = kv.Key.ToString();
                        if (Campaign.Equals("GDI Campaign"))
                        {
                            nextMission = CampaignProgress.GetGdiProgress();
                        }
                        else
                        {
                            nextMission = CampaignProgress.GetNodProgress();
                        }

                        progress = nextMission;

                        break;
                    }
                }
            }

            // check if last mission is exceeded
            this.CheckForLastMissionOfCampaign();

            // Congratualtions
            if (nextMission < progress)
            {
                congratsFlag = true;
                if (Campaign.Equals("GDI Campaign"))
                {
                    lastMissionSuccessfullyPlayedGDI = true;
                }
                else
                {
                    lastMissionSuccessfullyPlayedNod = true;
                }
            }
            else {
                congratsFlag = false;
            }

            // increment if next mission is missing
            this.CheckNextMission();

            // GDI05 Preview Germany or Ukraine --> Flag = 0 -> Germany
            if (Campaign.Equals("GDI Campaign") && progress == 4)
            {
                if (CampaignProgress.GetGdiPathFlag() == 0)
                {
                    nextMaps.RemoveRange(2, 2);
                }
                else
                {
                    nextMaps.RemoveRange(0, 2);
                }
            }

            nextMap = nextMaps[mapIndex];

            // Mission Text
            missionTitle = widget.Get<LabelWidget>("MISSION_TITLE");
            missionTitle.GetText = () => nextMap.Title;

            // Mission Description
            missionDescriptionPanel = widget.Get<ScrollPanelWidget>("MISSION_DESCRIPTION_PANEL");
            missionDescription = missionDescriptionPanel.Get<LabelWidget>("MISSION_DESCRIPTION");
            missionDescriptionFont = Game.Renderer.Fonts[missionDescription.Font];

            // Country Description
            countryDescriptionPanel = widget.Get<ScrollPanelWidget>("COUNTRY_DESCRIPTION_PANEL");
            countryDescriptionHeader = countryDescriptionPanel.Get<LabelWidget>("COUNTRY_DESCRIPTION_HEADER");
            countryDescriptionValues = countryDescriptionPanel.Get<LabelWidget>("COUNTRY_DESCRIPTION_VALUES");
            countryDescriptionFont = Game.Renderer.Fonts[missionDescription.Font];

            // Mission Map
            this.SetMapContent();

            // Next and Previous Button
            nextButton = widget.Get<ButtonWidget>("NEXT_BUTTON");
            prevButton = widget.Get<ButtonWidget>("PREVIOUS_BUTTON");
            if (nextMaps.Count > 1)
            {
                nextButton.OnClick = () =>
                {
                    mapIndex = (mapIndex + 1) % nextMaps.Count;
                    nextMap = nextMaps[mapIndex];
                    this.SetMapContent();
                };

                prevButton.OnClick = () =>
                {
                    mapIndex = (mapIndex + nextMaps.Count - 1) % nextMaps.Count;
                    nextMap = nextMaps[mapIndex];
                    this.SetMapContent();
                };
            }
            else
            {
                nextButton.Visible = false;
                prevButton.Visible = false;
            }

            videoPlayer = widget.Get<VqaPlayerWidget>("VIDEO");

            // Play Button
            playButton = widget.Get<ButtonWidget>("PLAY_BUTTON");
            playButton.OnClick = () =>
            {
                campaignBrowser.IsVisible = () => false;
                videoBackground.IsVisible = () => true;
                PlayAndStart();
            };

            // Campaign Browser Back Button
            campaignBrowserBackButton = widget.Get<ButtonWidget>("CAMPAIGN_BROWSER_BACK_BUTTON");
            campaignBrowserBackButton.OnClick = () =>
            {
                if (campaignPreviewRequired)
                {
                    this.ShowCampaignPreview();
                }
                else
                {
                    if (lastMissionSuccessfullyPlayedGDI || lastMissionSuccessfullyPlayedNod)
                    {
                        nextMission = progress;
                        CampaignProgress.SaveProgress(this.faction, nextMission);
                    }

                    Game.Disconnect();
                    Ui.CloseWindow();
                    onExit();
                }
            };

            // campaign preview
            campaignPreview = widget.Get<ContainerWidget>("CAMPAIGN_PREVIEW");

            // campaign preview grafic
            campaignPreviewWidget = widget.Get<MapPreviewWidget>("CAMPAIGN_PREVIEW_GRAFIC");

            campaignPreviewGraficButton = widget.Get<ButtonWidget>("CAMPAIGN_PREVIEW_GRAFIC_BUTTON");
            campaignPreviewGraficButton.OnClick = () =>
            {
                this.ShowCampaignBrowser();
            };

            // campaign preview button
            campaignPreviewContinueButton = widget.Get<ButtonWidget>("CAMPAIGN_PREVIEW_CONTINUE_BUTTON");
            campaignPreviewContinueButton.OnClick = () =>
            {
                this.ShowCampaignBrowser();
            };

            // Campaign Preview Back Button
            campaignBrowserBackButton = widget.Get<ButtonWidget>("CAMPAIGN_PREVIEW_BACK_BUTTON");
            campaignBrowserBackButton.OnClick = () =>
            {
                if (congratsFlag) {
                    this.ShowCongratulations();
                }
                else
                {
                    Game.Disconnect();
                    Ui.CloseWindow();
                    onExit();
                }
            };

            // congratulation
            campaignCongratulation = widget.Get<ContainerWidget>("CAMPAIGN_CONGRATULATION");

            // congratulation description
            congratulationTextPanel = widget.Get<ScrollPanelWidget>("CONGRATULATION_TEXT_PANEL");
            congratulationText = congratulationTextPanel.Get<LabelWidget>("CONGRATULATION_TEXT");
            congratulationTextFont = Game.Renderer.Fonts[congratulationText.Font];

            // congratulation Logos
            congratulationGdiLogo = widget.Get<ContainerWidget>("CONGRATULATION_LOGO_GDI");
            congratulationNodLogo = widget.Get<ContainerWidget>("CONGRATULATION_LOGO_NOD");

            this.SetCongratulationContent();

            // congratulation replay button
            campaignCongratulationContinueButton = widget.Get<ButtonWidget>("CAMPAIGN_CONGRATULATION_CONTINUE_BUTTON");
            campaignCongratulationContinueButton.OnClick = () =>
            {
                if (campaignPreviewRequired)
                {
                    this.ShowCampaignPreview();
                }
                else
                {
                    this.ShowCampaignBrowser();
                }
            };

            // Congratulation Back Button
            campaignBrowserBackButton = widget.Get<ButtonWidget>("CAMPAIGN_CONGRATULATION_BACK_BUTTON");
            campaignBrowserBackButton.OnClick = () =>
            {
                Game.Disconnect();
                Ui.CloseWindow();
                onExit();
            };

            this.ProgressCampaign();
        }

        void ProgressCampaign()
        {
            this.CheckCampaignProgressForPreview();

            if ((Campaign.Equals("GDI Campaign") && lastMissionSuccessfullyPlayedGDI) || (Campaign.Equals("Nod Campaign") && lastMissionSuccessfullyPlayedNod))
            {
                this.ShowCongratulations();
            }
            else if (campaignPreviewRequired)
            {
                this.ShowCampaignPreview();
            }
            else
            {
                this.ShowCampaignBrowser();
            }
        }

        void CheckCampaignProgressForPreview()
        {
            // check campaign preview
            if (nextMaps.Count() > 1)
            {
                // more missions available - campaign preview required
                campaignPreviewRequired = true;
            }
            else
            {
                campaignPreviewRequired = false;
            }
        }

        void ShowCampaignPreview()
        {
            this.SetPreviewContent();
            campaignPreview.IsVisible = () => true;
            campaignBrowser.IsVisible = () => false;
            campaignCongratulation.IsVisible = () => false;
        }

        void ShowCampaignBrowser()
        {
            campaignPreview.IsVisible = () => false;
            campaignBrowser.IsVisible = () => true;
            campaignCongratulation.IsVisible = () => false;
        }

        void ShowCongratulations()
        {
            campaignCongratulation.IsVisible = () => true;
            campaignPreview.IsVisible = () => false;
            campaignBrowser.IsVisible = () => false;
        }

        void SetCongratulationContent()
        {
            // it would be more elegant to read the text from a yaml-file instead of hardcoding it
            var victoryTextGdi = "Good work Commander! Thanks to your efforts the Global Defence Initiative was victorious. Your actions have thrown the brotherhood into disarray and without their leader we should soon be able to completely rid the world of their remnants.";
            var victoryTextNod = "Well done Brother! Your heroic actions have shown the world truth and freedom. Soon we will be free of the GDIs opression. Kane is proud of you!";
            var victoryText = this.faction == "GDI" ? victoryTextGdi : victoryTextNod;
            victoryText = WidgetUtils.WrapText(victoryText, congratulationText.Bounds.Width, congratulationTextFont);
            congratulationText.Text = victoryText;
            congratulationText.Bounds.Height = congratulationTextFont.Measure(victoryText).Y;
            congratulationTextPanel.ScrollToTop();
            congratulationTextPanel.Layout.AdjustChildren();

            congratulationGdiLogo.IsVisible = () => (this.faction == "GDI");
            congratulationNodLogo.IsVisible = () => !(this.faction == "GDI");
        }

        void SetMapContent()
        {
            var missionDescriptionText = nextMap.Description != null ? nextMap.Description.Replace("\\n", "\n") : "Mission description not available";
            missionDescriptionText = WidgetUtils.WrapText(missionDescriptionText, missionDescription.Bounds.Width, missionDescriptionFont);
            missionDescription.Text = missionDescriptionText;
            missionDescription.Bounds.Height = missionDescriptionFont.Measure(missionDescriptionText).Y;
            missionDescriptionPanel.ScrollToTop();
            missionDescriptionPanel.Layout.AdjustChildren();

            var countryDescriptionText = nextMap.CountryDescription;
            var countryDescriptionTextHeader = "No information available";
            var countryDescriptionTextValues = "";
            if (countryDescriptionText != null && countryDescriptionText.Length > 0)
            {
                countryDescriptionText = countryDescriptionText.Replace("\\n", "\n");
                if (countryDescriptionText.Contains('|'))
                {
                    string[] splits = countryDescriptionText.Split('|');

                    if (splits.Length > 0)
                    {
                        countryDescriptionTextHeader = "";
                        for (int i = 0; i < splits.Length; i = i + 2)
                        {
                            countryDescriptionTextHeader += splits[i];
                            countryDescriptionTextValues += splits[i + 1];
                        }
                    }
                }
            }

            countryDescriptionTextHeader = WidgetUtils.WrapText(countryDescriptionTextHeader, countryDescriptionHeader.Bounds.Width, countryDescriptionFont);
            countryDescriptionHeader.Text = countryDescriptionTextHeader;
            countryDescriptionHeader.Bounds.Height = countryDescriptionFont.Measure(countryDescriptionTextHeader).Y;
            countryDescriptionTextValues = WidgetUtils.WrapText(countryDescriptionTextValues, countryDescriptionValues.Bounds.Width, countryDescriptionFont);
            countryDescriptionValues.Text = countryDescriptionTextValues;
            countryDescriptionValues.Bounds.Height = countryDescriptionFont.Measure(countryDescriptionTextValues).Y;
            countryDescriptionPanel.ScrollToTop();
            countryDescriptionPanel.Layout.AdjustChildren();

            selectedMapPreview = Game.ModData.MapCache[nextMap.Uid];
        }

        void SetPreviewContent()
        {
            if (nextMap.Container.Exists("preview.png"))
                using (var dataStream = nextMap.Container.GetContent("preview.png"))
                {
                    nextMap.CustomPreview = new System.Drawing.Bitmap(dataStream);
                    var preview = new MapPreview(nextMap.Uid, Game.ModData.MapCache);
                    preview.SetMinimap(new SheetBuilder(SheetType.BGRA).Add(nextMap.CustomPreview));
                    campaignPreviewWidget.Preview = () => preview;
                }
        }

        void PlayAndStart()
        {
            try
            {
                if (nextMap.PreviewVideo != null)
                {
                    videoPlayer.Load(nextMap.PreviewVideo);
                    videoPlayer.PlayThen(StopVideoAndStart);
                }
                else
                {
                    StartMission();
                }
            }
            catch (FileNotFoundException)
            {
                StartMission();
            }
        }

        void StopVideoAndStart()
        {
            if (videoStopped)
            {
                Sound.MusicVolume = cachedMusicVolume;
                videoPlayer.Stop();
            }
            else
            {
                videoStopped = true;
                StopVideoAndStart();
                StartMission();
            }
        }

        void StartMission()
        {
            OrderManager om = null;

            Action lobbyReady = null;
            lobbyReady = () =>
            {
                Game.LobbyInfoChanged -= lobbyReady;
                onStart();
                om.IssueOrder(Order.Command("state {0}".F(Session.ClientState.Ready)));
            };
            Game.LobbyInfoChanged += lobbyReady;

            // Set Flag if GDI Path to Belarus is selected: gdi04b
            if (Campaign.Equals("GDI Campaign"))
            {
                if (progress == 3 && mapIndex == 1)
                {
                    CampaignProgress.SetGdiPathFlag();
                }
                else
                {
                    CampaignProgress.ResetGdiPathFlag();
                }
            }

            om = Game.JoinServer(IPAddress.Loopback.ToString(), Game.CreateLocalServer(nextMap.Uid), "", false);
        }

        void CheckForLastMissionOfCampaign()
        {
            bool exit = false;
            this.faction = "";

            // search last map in faction
            // if factionMaps contains 9 maps, start searching "gdi09"/"nod09"
            for (int i = factionMaps.Count; i > 0; i--)
            {
                map = "";
                foreach (Map map1 in factionMaps)
                {
                    if (CampaignWorldLogic.map == "")
                    {
                        if (map1.Container.Name.Contains("gdi"))
                        {
                            CampaignWorldLogic.map = "gdi";
                            this.faction = "GDI";
                        }
                        else
                        {
                            CampaignWorldLogic.map = "nod";
                            this.faction = "Nod";
                        }

                        if (i < 10)
                            CampaignWorldLogic.map += "0";
                        CampaignWorldLogic.map += i.ToString();   // i.e. map = "gdi02"
                    }

                    // if last mission is found
                    if (map1.Container.Name.Contains(CampaignWorldLogic.map))
                    {
                        exit = true;

                        if (i <= progress)
                        {   // if last mission < stored mission
                            if (Campaign.Equals("GDI Campaign"))
                            {
                                lastMissionSuccessfullyPlayedGDI = true;
                            }
                            else
                            {
                                lastMissionSuccessfullyPlayedNod = true;
                            }

                            progress = i;
                            nextMission = i - 1;
                            CampaignProgress.SaveProgress(this.faction, progress);
                        }
                        else
                        {
                            if (Campaign.Equals("GDI Campaign"))
                            {
                                lastMissionSuccessfullyPlayedGDI = false;
                            }
                            else
                            {
                                lastMissionSuccessfullyPlayedNod = false;
                            }
                        }

                        break;
                    }
                }

                if (exit)
                    break;
            }
        }

        void CheckNextMission()
        {
            map = "";

            // check next mission
            while (nextMaps.Count() == 0)
            {
                foreach (Map map1 in factionMaps)
                {
                    if (CampaignWorldLogic.map == "")
                    {
                        string mission = (nextMission + 1).ToString();

                        if (map1.Container.Name.Contains("gdi"))
                            CampaignWorldLogic.map = "gdi";
                        else
                            CampaignWorldLogic.map = "nod";

                        if (mission.Length < 2)
                            mission = "0" + mission;

                        CampaignWorldLogic.map += mission;   // i.e. map = "gdi02"
                    }

                    // check if mission exists
                    if (map1.Container.Name.Contains(CampaignWorldLogic.map))
                    {
                        // add mapXX, mapXXa, mapXXb, ...
                        nextMaps.Add(map1);

                        // if mission is missing increment stored progress
                        if (nextMission > progress)
                        {
                            CampaignProgress.SaveProgress(this.faction, nextMission);
                        }
                    }
                }

                nextMission++;
                map = "";
            }
        }
    }
}
