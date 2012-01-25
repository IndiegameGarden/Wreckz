﻿// (c) 2010-2012 TranceTrance.com. Distributed under the FreeBSD license in LICENSE.txt
﻿
// defines for global settings (debug etc)
// -> defines set in Visual Studio Profiles: DEBUG, RELEASE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices; 

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

using TTengine.Core;
using TTengine.Util;
using TTengine.Modifiers;

using IndiegameGarden.Download;
using IndiegameGarden.Unpack;
using IndiegameGarden.Menus;
using IndiegameGarden.Base;
using IndiegameGarden.Install;

using MyDownloader.Core.Extensions;
using MyDownloader.Extension;
using MyDownloader.Extension.Protocols;
using MyDownloader.Core;

namespace IndiegameGarden
{
    /**
     * <summary>
     * Main game class and singleton for IndiegameGarden
     * </summary>
     */
    public class GardenGame : Game
    {
        /// <summary>
        /// singleton instance
        /// </summary>
        public static GardenGame Instance = null;

        /// <summary>
        /// Library of games to select from
        /// </summary>
        public GameLibrary GameLib;

        /// <summary>
        /// configuration and parameters store
        /// </summary>
        public GardenConfig Config;
        
        /// <summary>
        /// the top-level Gamelet
        /// </summary>
        public Gamelet TreeRoot;

        // --- internal + TTengine related
        GameLauncherTask launcher;
        ThreadedTask launchGameThread;
        GraphicsDeviceManager graphics;
        Screenlet mainScreen;
        LoadingScreen loadingScreen;
        SpriteBatch spriteBatch;
        HttpFtpProtocolExtension myDownloaderProtocol;
        int myWindowWidth = 1280; //1024; //1280; //1440; //1280;
        int myWindowHeight = 768; //768; //720; //900; //720;

        #region Constructors
        public GardenGame()
        {
            Instance = this;
            Content.RootDirectory = "Content";

            // create the TTengine for this game
            TTengineMaster.Create(this);

            // basic XNA graphics init here (before Initialize() and LoadContent() )
            graphics = new GraphicsDeviceManager(this);
            myWindowWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            myWindowHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            graphics.PreferredBackBufferWidth = myWindowWidth;
            graphics.PreferredBackBufferHeight = myWindowHeight;
            graphics.IsFullScreen = false;
            this.IsFixedTimeStep = false;            
            graphics.SynchronizeWithVerticalRetrace = true;
        }
        #endregion

        protected override void Initialize()
        {
            Exception initError = null;

            // more graphics init
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // loading screen
            loadingScreen = new LoadingScreen(myWindowWidth, myWindowHeight);
            TTengineMaster.ActiveScreen = loadingScreen;
            loadingScreen.ActiveInState = new StatePlayingGame();
            loadingScreen.DrawInfo.DrawColor = Color.Black;

            // from here on, main screen
            mainScreen = new Screenlet(myWindowWidth, myWindowHeight);
            TTengineMaster.ActiveScreen = mainScreen;
            mainScreen.ActiveInState = new StateBrowsingMenu();
            TreeRoot = new FixedTimestepPhysics();
            TreeRoot.SetNextState(new StateBrowsingMenu()); // set the initial state

            TreeRoot.Add(mainScreen);
            TreeRoot.Add(loadingScreen);
            mainScreen.Add(new FrameRateCounter(1.0f, 0f)); // TODO
            mainScreen.Add(new ScreenZoomer()); // TODO remove
            mainScreen.DrawInfo.DrawColor = Color.Black;

            // MyDownloader Config
            myDownloaderProtocol = new HttpFtpProtocolExtension();

            // GardenConfig
            try
            {
                Config = new GardenConfig();
            }
            catch (Exception ex)
            {
                TTengine.Util.MsgBox.Show("Could not load configuration", "Could not load configuration file."); // TODO
                initError = ex;
                Exit();
            }

            // game library
            try
            {
                GameLib = new GameLibrary();
            }
            catch (Exception ex)
            {
                MsgBox.Show("Could not load game library file", "Could not load game library file."); // TODO
                initError = ex;
                Exit();
            }

            if (initError == null)
            {
                // game chooser menu
                GameChooserMenu menu = new GameChooserMenu();
                mainScreen.Add(menu);
            }

            // finally call base to enumnerate all (gfx) Game components to init
            base.Initialize();

        }

        protected override void Update(GameTime gameTime)
        {
            // update params, and call the root gamelet to do all.
            TTengineMaster.Update(gameTime, TreeRoot);

            // update any other XNA components
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            // draw all my gamelet items
            GraphicsDevice.SetRenderTarget(null); // TODO
            TTengineMaster.Draw(gameTime, TreeRoot);

            // then buffer drawing on screen at right positions                        
            GraphicsDevice.SetRenderTarget(null); // TODO
            //GraphicsDevice.Clear(Color.Black);
            Screenlet visibleScreen = mainScreen;
            if (TreeRoot.IsInState(new StatePlayingGame()))
                visibleScreen = loadingScreen;
            Rectangle destRect = new Rectangle(0, 0, visibleScreen.RenderTarget.Width, visibleScreen.RenderTarget.Height);
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            spriteBatch.Draw(visibleScreen.RenderTarget, destRect, Color.White);
            spriteBatch.End();

            // then draw other (if any) game components on the screen
            base.Draw(gameTime);

        }

        /// <summary>
        /// indicate to game that now we should clean up and exit
        /// </summary>
        public void ExitGame()
        {
            if (TreeRoot != null)
            {
                TreeRoot.Dispose();
                TreeRoot = null;
            }
            System.GC.Collect();
            Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && TreeRoot != null)
            {
                TreeRoot.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// called by a child GUI component to launch a game
        /// </summary>
        /// <param name="g">game to launch</param>
        public void ActionLaunchGame(IndieGame g)
        {
            if (g.IsInstalled)
            {
                // if installed, then launch it if possible
                if ((launcher == null || launcher.IsFinished() == true) &&
                     (launchGameThread == null || launchGameThread.IsFinished()))
                {
                    loadingScreen.SetGame(g);
                    // set state of game to 'game playing state'
                    TreeRoot.SetNextState(new StatePlayingGame());

                    launcher = new GameLauncherTask(g);
                    launchGameThread = new ThreadedTask(launcher);
                    launchGameThread.TaskSuccessEvent += new TaskEventHandler(taskThread_TaskFinishedEvent);
                    launchGameThread.TaskFailEvent += new TaskEventHandler(taskThread_TaskFinishedEvent);
                    launchGameThread.Start();
                }
            }
        }

        // when a launched process concludes
        void taskThread_TaskFinishedEvent(object sender)
        {
            // set menu state back to 'menu viewing' state
            GardenGame.Instance.TreeRoot.SetNextState(new StateBrowsingMenu());
        }

        /// <summary>
        /// called by a child GUI component to install a game
        /// </summary>
        /// <param name="g">game to install</param>
        public void ActionDownloadAndInstallGame(IndieGame g)
        {
            // check if download+install task needs to start or not
            if (g.DlAndInstallTask == null && g.ThreadedDlAndInstallTask == null && !g.IsInstalled)
            {
                g.DlAndInstallTask = new GameDownloadAndInstallTask(g);
                g.ThreadedDlAndInstallTask = new ThreadedTask(g.DlAndInstallTask);
                g.ThreadedDlAndInstallTask.Start();
            }
        }



    }
}
