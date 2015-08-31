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
using System.Linq;

using OpenRA.Graphics;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	class CampaignMissionPreviewLogic
	{
		readonly CampaignWorldLogic campaignWorld;
		readonly MapPreviewWidget campaignPreviewWidget;
		readonly ButtonWidget campaignPreviewContinueButton, campaignPreviewGraficButton, campaignPreviewBackButton;

		public CampaignMissionPreviewLogic(CampaignWorldLogic campaignWorld, Widget widget, Action onExit)
		{
			this.campaignWorld = campaignWorld;


			// Campaign preview grafic
			campaignPreviewWidget = widget.Get<MapPreviewWidget>("CAMPAIGN_PREVIEW_GRAFIC");
			campaignPreviewWidget.Preview = campaignWorld.GetFirstMapPreview;

			campaignPreviewGraficButton = widget.Get<ButtonWidget>("CAMPAIGN_PREVIEW_GRAFIC_BUTTON");
			campaignPreviewGraficButton.OnClick = campaignWorld.CallbackShowCampaignBrowserOnClick;

			// Campaign preview button
			campaignPreviewContinueButton = widget.Get<ButtonWidget>("CAMPAIGN_PREVIEW_CONTINUE_BUTTON");
			campaignPreviewContinueButton.OnClick = campaignWorld.CallbackShowCampaignBrowserOnClick;

			// Campaign preview back button
			campaignPreviewBackButton = widget.Get<ButtonWidget>("CAMPAIGN_PREVIEW_BACK_BUTTON");
			campaignPreviewBackButton.OnClick = () =>
			{
				campaignWorld.SwitchFirstMapPreview();
				if (campaignWorld.GetCongratsFlag())
					campaignWorld.ShowCongratulations();
				else
				{
					Game.Disconnect();
					Ui.CloseWindow();
					onExit();
				}
			};

		}
	}
}
