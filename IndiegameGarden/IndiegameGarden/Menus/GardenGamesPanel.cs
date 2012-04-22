﻿// (c) 2010-2012 TranceTrance.com. Distributed under the FreeBSD license in LICENSE.txt

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

using TTengine.Core;
using TTengine.Modifiers;

using IndiegameGarden.Base;
using IndiegameGarden.Util;

namespace IndiegameGarden.Menus
{
    /**
     * a GamesPanel that arranges games in a large rectangular matrix, the "garden". User can travel
     * the garden with a cursor. Only a part of the garden is shown at a time.
     */
    public class GardenGamesPanel: GamesPanel
    {
        // below: UI constants
        const float LAYER_BACK = 1.0f;
        const float LAYER_FRONT = 0.0f;
        const float LAYER_ZOOMING_ITEM = 0.1f;
        const float LAYER_DODGING_ITEM = 0.3f;
        const float LAYER_GRID_ITEMS = 0.9f;

        const float PANEL_ZOOM_REGULAR = 0.45f; //0.16f;
        const float PANEL_DELTA_GRID_X = 0.16f;
        const float PANEL_DELTA_GRID_Y = 0.12f;
        const float PANEL_SPEED_SHIFT = 2.1f;
        const float PANEL_SIZE_X = 1.333f;
        const float PANEL_SIZE_Y = 1.0f;
        const float PANEL_ZOOM_TARGET_QUITTING = 0.01f;
        const float PANEL_ZOOM_SPEED_QUITTING = 0.005f;
        const float PANEL_ZOOM_SPEED_REGULAR = 0.005f;
        const float PANEL_ZOOM_SPEED_ABORTQUITTING = 0.005f;
        const float PANEL_ZOOM_DETAILED_VIEW = 1.0f; //2.857f;

        const float CURSOR_SCALE_REGULAR = 0.8f; //5.9375f;
        public const float CURSOR_DISCOVERY_RANGE = 0.35f;
        const float CURSOR_MARGIN_X = 0.15f;
        const float CURSOR_MARGIN_Y = 0.15f;

        public const float THUMBNAIL_SCALE_UNSELECTED_UNINSTALLED = 0.44f;
        const float THUMBNAIL_SCALE_UNSELECTED = 0.44f; //0.6f; //0.54f; //1.5625f;
        const float THUMBNAIL_SCALE_SELECTED = 0.51f; //0.7f; //0.65f; //2f;
        
        static Vector2 INFOBOX_SHOWN_POSITION = new Vector2(0.05f, 0.895f);
        static Vector2 INFOBOX_HIDDEN_POSITION = new Vector2(0.05f, 0.96f);
        const float    INFOBOX_SPEED_MOVE = 3.8f;
        
        const float TIME_BEFORE_GAME_LAUNCH = 0.7f;
        const float TIME_BEFORE_EXIT = 1.2f;
        const float TIME_BEFORE_EXIT_CONTINUES = 0.6f;

        // maximum sizes of grid
        public double GridMaxX=32, GridMaxY=32;

        Dictionary<string, GameThumbnail> thumbnailsCache = new Dictionary<string, GameThumbnail>();
        
        // cursor is the graphics selection thingy         
        GameThumbnailCursor cursor;

        // box showing info of a game such as title and download progressContributionSingleFile
        GameInfoBox infoBox;

        // textbox showing controls help message
        FloatingTextMessage controlsHelpText;

        // UI related vars - related to whether user indicates to quit program or user cancelled this
        bool isExiting = false;
        bool isExitingUnstoppable = false;
        bool isGameLaunchOngoing = false;
        bool isLaunchWebsite = false;
        float timeExiting = 0f;
        float timeLaunching = 0f;
        Vector2 PanelShiftPos = Vector2.Zero;
        int selectionLevel = 1;
        GameChooserMenu parentMenu;

        public GardenGamesPanel(GameChooserMenu parent)
        {
            parentMenu = parent;

            // cursor
            cursor = new GameThumbnailCursor();
            Add(cursor);
            cursor.Motion.Scale = CURSOR_SCALE_REGULAR;

            // info box - will be added to parent upon OnNewParent() event
            infoBox = new GameInfoBox();
            infoBox.Motion.Position = INFOBOX_HIDDEN_POSITION;

            // controls help text
            controlsHelpText = new FloatingTextMessage();
            controlsHelpText.Motion.Position = new Vector2(0.04f, 0.04f);
            controlsHelpText.Text = "CONTROLS:\n\n" + 
                                    "ARROWs = Move cursor          ENTER = Select game\n" +
                                    "ESCAPE = Back                       Hold ESCAPE = Quit the garden\n" +
                                    "Hold ENTER = Grow game in your garden / Play game\n" +
                                    "W = Launch game's website";

            // default zoom
            Motion.ZoomTarget = PANEL_ZOOM_DETAILED_VIEW;
            Motion.ZoomSpeed = PANEL_ZOOM_SPEED_REGULAR;
            Motion.ZoomCenterTarget = cursor.Motion;
        }

        public override void OnUpdateList(GameCollection gl)
        {
            // first process old list - start fading away of items
            for (int i = 0; i < gl.Count; i++)
            {
                GardenItem g = gl[i];
                if (thumbnailsCache.ContainsKey(g.GameID))
                {
                    GameThumbnail th = thumbnailsCache[g.GameID];
                    th.ColorB.FadeToTarget(0f,4f);
                }
            }
            this.gl = gl;

            // update selection
            if (gl.Count > 0)
            {
                if (SelectedGame == null)
                {
                    SelectedGame = gl[0];
                    cursor.SetToGame(SelectedGame);
                    
                }
                else
                {
                    if (!gl.Contains(SelectedGame))
                    {
                        SelectedGame = gl[0];
                        cursor.SetToGame(SelectedGame);
                    }
                    else
                    {
                        // gl contains the previously selected game. Relocate it in new list.
                        cursor.SetToGame(SelectedGame);
                    }
                }
            }
        }

        // shorthand method to select the game currently indicated by cursor
        protected void SelectGameBelowCursor()
        {
            GardenItem g = gl.FindGameAt(cursor.GridPosition);
            SelectedGame = g;
            infoBox.ClearProgressBar();
            if (g!= null)
                g.Refresh();
        }

        protected override void OnNewParent()
        {
            base.OnNewParent();

            // some items are part of the parent, to avoid scaling issues in GardenGamesPanel
            // (which get rescaled/zoomed based on user input).
            Parent.Add(infoBox);
            Parent.Add(controlsHelpText);
        }

        protected override void OnUpdate(ref UpdateParams p)
        {
            GameThumbnail th = null;

            base.OnUpdate(ref p);

            // update text box with currently selected game info
            infoBox.SetGameInfo(SelectedGame);

            // handle download/install/launching of a game
            if (isGameLaunchOngoing)
            {
                timeLaunching += p.Dt;
                th = thumbnailsCache[SelectedGame.GameID];
                float sc = (1f + timeLaunching/3f);
                th.Motion.ScaleTarget = sc; // blow up size of thumbnail while user requests launch
                //th.Motion.ScaleSpeed = 0.00005f;
                cursor.Motion.ScaleTarget = sc;
                cursor.Motion.ScaleSpeed = th.Motion.ScaleSpeed / SelectedGame.ScaleIcon; // TODO correct ScaleIcon?

                if (timeLaunching > TIME_BEFORE_GAME_LAUNCH)
                {
                    // check for mystery game
                    if (SelectedGame.GameID.Equals("igg_mysterygame"))
                    {
                        GameCollection lib = GardenGame.Instance.GameLib.GetList();
                        GardenItem grnd = lib.GetRandomInstalledGame();
                        GardenGame.Instance.music.FadeOut();
                        GameThumbnail thumb = thumbnailsCache[grnd.GameID];
                        GardenGame.Instance.ActionLaunchGame(grnd, thumb);
                        isGameLaunchOngoing = false;
                        return;

                    }
                    else
                    {
                        if (SelectedGame.IsInstalled)
                        {
                            GardenGame.Instance.music.FadeOut();
                            GameThumbnail thumb = thumbnailsCache[SelectedGame.GameID];
                            GardenGame.Instance.ActionLaunchGame(SelectedGame, thumb);
                            isGameLaunchOngoing = false;
                            return;
                        }
                        else
                        {
                            GardenGame.Instance.ActionDownloadAndInstallGame(SelectedGame);
                        }
                    }
                    isGameLaunchOngoing = false;
                }
            }

            // handle exit key
            if (isExiting)
            {
                GardenGame.Instance.music.FadeOut();
                timeExiting += p.Dt;
                if (timeExiting > TIME_BEFORE_EXIT)
                {
                    parentMenu.background.Motion.ScaleModifier = 1f / (1f + (timeExiting-TIME_BEFORE_EXIT) / 11f);
                    if (!isExitingUnstoppable)
                    {
                        GardenGame.Instance.ExitGame();
                        isExitingUnstoppable = true;
                    }
                    return;
                }
            }
            else
            {
                if (timeExiting > 0f)
                {
                    GardenGame.Instance.music.FadeIn();
                    timeExiting = 0f;
                }
            }

            //-- website launch
            if (isLaunchWebsite)
            {
                if (SelectedGame != null)
                {
                    GardenGame.Instance.ActionLaunchWebsite(SelectedGame);
                }
                isLaunchWebsite = false;
            }

            //-- helpful controls text
            if (SelectedGame != null && SelectedGame.GameID.Equals("igg_controls"))
            {
                controlsHelpText.FadeIn();
                SelectedGame.Name = GardenGame.Instance.Config.ServerMsg;
            }
            else
            {
                controlsHelpText.FadeOut();
            }

            //-- loop all games adapt their display properties where needed
            if (gl == null)
                return;
            GardenItem g;
            for (int i = 0; i < gl.Count; i++)
            {
                // fetch that game from list
                g = gl[i];

                // if GameThumbnail for current game does not exist yet, create it                
                if (!thumbnailsCache.ContainsKey(g.GameID))
                {
                    // create now
                    th = new GameThumbnail(g);
                    Add(0,th);
                    thumbnailsCache.Add(g.GameID, th);
                    //th.Position = new Vector2(RandomMath.RandomBetween(-0.4f,2.0f), RandomMath.RandomBetween(-0.4f,1.4f) );
                    //th.Scale = RandomMath.RandomBetween(0.01f, 0.09f); 
                    // create with new position and scale
                    th.Motion.Position = Screen.Center;
                    th.Motion.Scale = 0.05f;

                    th.DrawInfo.LayerDepth = LAYER_GRID_ITEMS;
                    th.Visible = false;
                    th.ColorB.Intensity = 0.0f;
                }else{
                    // retrieve GameThumbnail from cache
                    th = thumbnailsCache[g.GameID];
                }
                th.Motion.ScaleSpeed = 0.01f; // TODO const
                
                // check if thnail visible and in range. If so, start displaying it (fade in)
                if (!th.Visible && cursor.GameletInRange(th))
                {
                    th.LoadInBackground();
                    th.ColorB.Intensity = 0f;
                }

                if (th.IsLoaded())
                    th.ColorB.FadeTarget = (0.65f + 0.35f * g.InstallProgress);
                else
                    th.ColorB.FadeTarget = 0f;

                if (!(isGameLaunchOngoing && g == SelectedGame))
                {
                    if (g.IsInstalling)
                    {
                        // wobble the size of icon when installing.
                        th.Motion.ScaleTarget = 1.0f + 0.1f * (float)Math.Sin(MathHelper.TwoPi * 0.16f * SimTime);
                    }
                    else
                    {
                        th.Motion.ScaleTarget = 1f; 
                        //th.Motion.ScaleSpeed = 0.03f;

                        // displaying selected thumbnails larger
                        if (g == SelectedGame && g.IsGrowable )
                        {
                            th.Motion.ScaleTarget *= THUMBNAIL_SCALE_SELECTED ; 
                        }
                        else
                        {
                            if (g.IsInstalled || !g.IsGrowable)
                                th.Motion.ScaleTarget *= THUMBNAIL_SCALE_UNSELECTED;
                            else
                                th.Motion.ScaleTarget *= THUMBNAIL_SCALE_UNSELECTED_UNINSTALLED ;
                            
                        }
                        
                    }
                }
                th.ColorB.FadeSpeed = 0.15f;// 0.15f;

                // coordinate position where to move a game thumbnail to 
                Vector2 targetPos = (g.Position - PanelShiftPos) * new Vector2(PANEL_DELTA_GRID_X,PANEL_DELTA_GRID_Y);
                th.Motion.TargetPos = targetPos;
                th.Motion.TargetPosSpeed = PANEL_SPEED_SHIFT;

                // cursor where to move to
                cursor.Motion.TargetPos = (cursor.GridPosition - PanelShiftPos) * new Vector2(PANEL_DELTA_GRID_X, PANEL_DELTA_GRID_Y);
                cursor.Motion.TargetPosSpeed = PANEL_SPEED_SHIFT; 

                // panel shift effect when cursor hits edges of panel
                if (true /*Motion.Zoom == Motion.ZoomTarget*/  )
                {
                    Vector2 cp = cursor.Motion.PositionDraw;
                    float chw = cursor.DrawInfo.WidthAbs / 2.0f; // cursor-half-width
                    float chh = cursor.DrawInfo.HeightAbs / 2.0f; // cursor-half-height
                    float dx = PANEL_SPEED_SHIFT * p.Dt;
                    const float xMargin = CURSOR_MARGIN_X; // TODO into gui props
                    const float yMargin = CURSOR_MARGIN_Y;
                    if (cp.X <= chw + xMargin)
                    {
                        PanelShiftPos.X -= dx;
                    }
                    else if (cp.X >= PANEL_SIZE_X - chw - xMargin)
                    {
                        PanelShiftPos.X += dx;
                    }
                    if (cp.Y <= chh + yMargin)
                    {
                        PanelShiftPos.Y -= dx;
                    }
                    else if (cp.Y >= PANEL_SIZE_Y - chh - yMargin)
                    {
                        PanelShiftPos.Y += dx;
                    }
                }
            }
        }

        protected override void OnDraw(ref DrawParams p)
        {
            base.OnDraw(ref p);

            // DEBUG
            //if (SelectedGame != null)
            //    Screen.DebugText(0f, 0f, "Selected: " + gl.IndexOf(SelectedGame) + " " + SelectedGame.GameID );
            //Screen.DebugText(0f, 0.1f, "Zoom: " + Motion.Zoom);
        }

        public override void OnChangedSelectedGame(GardenItem newSel, GardenItem oldSel)
        {
            //
        }

        public override void OnUserInput(GamesPanel.UserInput inp)
        {
            switch (inp)
            {
                case UserInput.DOWN:
                    if (cursor.GridPosition.Y < GridMaxY -1 )
                    {
                        cursor.GridPosition.Y += 1f;
                        SelectGameBelowCursor();
                    }
                    break;
               
                case UserInput.UP:
                    if (cursor.GridPosition.Y > 0)
                    {
                        cursor.GridPosition.Y -= 1f;
                        SelectGameBelowCursor();
                    }
                    break;
                
                case UserInput.LEFT:
                    if (cursor.GridPosition.X > 0)
                    {
                        cursor.GridPosition.X -= 1f;
                        SelectGameBelowCursor();
                    }
                    break;
                
                case UserInput.RIGHT:
                    if (cursor.GridPosition.X < GridMaxX - 1)
                    {
                        cursor.GridPosition.X += 1f;
                        SelectGameBelowCursor();
                    }
                    break;
                
                case UserInput.START_EXIT:
                    isExiting = true;
                    //selectionLevel = 0;
                    Motion.ZoomTarget = PANEL_ZOOM_TARGET_QUITTING ;
                    Motion.ZoomSpeed = PANEL_ZOOM_SPEED_QUITTING ;
                    //Motion.ZoomCenter = cursor.Motion.PositionAbs;
                    //Motion.ZoomCenterTarget = cursor.Motion;
                    break;
                
                case UserInput.STOP_EXIT:
                    if (timeExiting < TIME_BEFORE_EXIT_CONTINUES)
                    {
                        isExiting = false;
                        selectionLevel = 0;
                        Motion.ZoomTarget = PANEL_ZOOM_REGULAR;
                        Motion.ZoomSpeed = PANEL_ZOOM_SPEED_ABORTQUITTING;
                        //Motion.ZoomCenter = cursor.Motion.PositionAbs;
                        //Motion.ZoomCenterTarget = cursor.Motion;
                    }
                    break;

                case UserInput.START_SELECT:
                    if (SelectedGame != null)
                    {                        
                        GameThumbnail th = thumbnailsCache[SelectedGame.GameID];
                        if (th != null)
                        {
                            switch (selectionLevel)
                            {
                                case 0:
                                    // select once - zoom in on selected game
                                    Motion.ZoomTarget = PANEL_ZOOM_DETAILED_VIEW;
                                    Motion.ZoomSpeed = PANEL_ZOOM_SPEED_REGULAR; // 0.01f; 
                                    //Motion.ZoomCenter = cursor.Motion.PositionAbs;
                                    Motion.ZoomCenterTarget = cursor.Motion;
                                    SelectedGame.Refresh();
                                    //infoBox.Motion.TargetPos = INFOBOX_SHOWN_POSITION - new Vector2(0f,0.05f * (SelectedGame.DescriptionLineCount-1));
                                    //infoBox.Motion.TargetPosSpeed = INFOBOX_SPEED_MOVE;
                                    selectionLevel++;
                                    break;
                                case 1:
                                    // select again - install or launch game if selection key pressed long enough.
                                    isGameLaunchOngoing = true;
                                    break;
                            }


                        }
                    }
                    break;

                case UserInput.STOP_SELECT:
                    isGameLaunchOngoing = false;
                    timeLaunching = 0f;
                    cursor.Motion.ScaleTarget = CURSOR_SCALE_REGULAR;
                    break;

                case UserInput.LAUNCH_WEBSITE:
                    isLaunchWebsite = true;
                    break;

                case UserInput.TOGGLE_MUSIC:
                    GardenGame.Instance.music.ToggleMusic();
                    break;

            } // switch(inp)

            if (selectionLevel == 0)
            {
                infoBox.Motion.TargetPos = INFOBOX_HIDDEN_POSITION;
                infoBox.Motion.TargetPosSpeed = INFOBOX_SPEED_MOVE;
            }

            if (selectionLevel == 1)
            {
                int lnCount = 1;
                if (SelectedGame != null)
                {
                    lnCount = SelectedGame.DescriptionLineCount;
                }
                infoBox.Motion.TargetPos = INFOBOX_SHOWN_POSITION - new Vector2(0f, 0.029f * (lnCount - 1));
                infoBox.Motion.TargetPosSpeed = INFOBOX_SPEED_MOVE;
            }

        }
    }
}
