﻿// (c) 2010-2013 TranceTrance.com. Distributed under the FreeBSD license in LICENSE.txt

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TTengine.Core;

using IndiegameGarden.Base;
using IndiegameGarden.Download;
using MyDownloader.Core;

namespace IndiegameGarden.Menus
{
    /// <summary>
    /// A thumbnail showing a single game, with scaling, auto-loading and downloading of image data.
    /// </summary>
    public class GameThumbnail: EffectSpritelet
    {
        /// <summary>
        /// my color change behavior used eg for fading in/out
        /// </summary>
        public ColorChangeBehavior ColorB;

        /// <summary>
        /// ref to game for which this thumbnail is
        /// </summary>
        public GardenItem Game;

        public bool IsFadingOut = false;

        /// <summary>
        /// actual/intended full path to local thumbnail file (the file may or may not exist)
        /// </summary>
        protected string ThumbnailFilepath
        {
            get
            {
                return GardenConfig.Instance.GetThumbnailFilepath(Game);
            }
        }

        public static int MaxConcurrentDownloads = 2;

        public static bool IsNewDownloadAllowed
        {
            get
            {
                return (numLoadingTasksActive < MaxConcurrentDownloads);
            }
        }

        /// <summary>
        /// number of thumbnail loading tasks ongoing. Used to limit downloads to MaxConcurrentDownloads
        /// </summary>
        static volatile int numLoadingTasksActive = 0;

        /// <summary>
        /// thumbnail loading task
        /// </summary>
        ITask loaderTask;

        /// <summary>
        /// when a new texture is available, it's passed via this var and using corresponding lock object
        /// </summary>
        Texture2D updatedTexture;
        Object updateTextureLock = new Object();

        /// <summary>
        /// indicate whether texture already loaded
        /// </summary>
        bool isLoaded = false;
        
        /// <summary>
        /// a time variable for rendering shader 'halo' around a thumbnail
        /// </summary>
        float haloTime = 0f;

        // a default texture to use if no thumbnail has been loaded yet
        static Texture2D DefaultTexture;

        /**
         * internal Task to load a thumbnail from disk, or download it first if not available.
         * To be called in a separate thread e.g. by using a ThreadedTask.
         */
        class GameThumbnailLoadTask : ThumbnailDownloader
        {

            // my parent - where to load for/to
            GameThumbnail parent;

            public GameThumbnailLoadTask(GameThumbnail th): 
                base(th.Game)
            {                
                parent = th;
            }

            /// <summary>
            /// loads image from file if it exists, or else by downloading and then loading from file
            /// </summary>
            protected override void StartInternal()
            {
                numLoadingTasksActive++;
                try{
                    if (File.Exists(parent.ThumbnailFilepath))
                    {
                        bool ok = parent.LoadTextureFromFile();
                        if (ok)
                            status = ITaskStatus.SUCCESS;
                        else
                            status = ITaskStatus.FAIL;
                    }
                    else
                    {
                        // first run the base downloading task now. If that is ok, then load from file downloaded.                    
                        base.StartInternal();

                        if (File.Exists(parent.ThumbnailFilepath) && IsSuccess())
                        {
                            bool ok = parent.LoadTextureFromFile();
                            if (ok)
                                status = ITaskStatus.SUCCESS;
                            else
                                status = ITaskStatus.FAIL;
                        }
                        else
                        {
                            status = ITaskStatus.FAIL;
                        }

                    }

                    // after a successful load, enable the thumbnail.
                    if (IsSuccess())
                    {
                        parent.Enable();
                    }
                }
                finally
                {
                    numLoadingTasksActive--;
                }
            }
        } // class

        public GameThumbnail(GardenItem game)
            : base( (Texture2D) null, "GameThumbnail")
        {
            ColorB = new ColorChangeBehavior();         
            Add(ColorB);
            Motion.Scale = GardenGamesPanel.THUMBNAIL_SCALE_UNSELECTED;
            Game = game;
            // effect is still off if no bitmap loaded yet
            EffectEnabled = false;
            // first-time texture init
            
            if (DefaultTexture == null)
            {
                DefaultTexture = GardenGame.Instance.Content.Load<Texture2D>("ball-supernova2");
            }
            
            // use default texture as long as thumbnail not loaded yet
            Texture = DefaultTexture;
        }

        public override void Dispose()
        {
            if (loaderTask != null)
                loaderTask.Abort();
            loaderTask = null;

            base.Dispose();
        }

        /// <summary>
        /// test whether the image for this thumbnail has already been (down)loaded or not.
        /// </summary>
        /// <returns>true if image has been loaded, false if not yet or not successfully</returns>
        public bool IsLoaded()
        {
            return ((loaderTask != null) && (loaderTask.IsSuccess() ));
        }

        /// <summary>
        /// trigger the background loading in a thread of the game's thumbnail image.
        /// Does nothing if loading already in progress or finished.
        /// Does nothing if too many downloads already ongoing.
        /// </summary>
        /// <returns>true if loading was started</returns>
        public bool LoadInBackground()
        {
            if (loaderTask == null)
            {
                if (IsNewDownloadAllowed)
                {                    
                    loaderTask = new ThreadedTask(new GameThumbnailLoadTask(this));
                    loaderTask.Start();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// makes thumbnail visible, setting EffectEnabled properly, and loading thumbnail image if not yet loaded/loading.
        /// </summary>
        public void Enable()
        {
            Visible = true;
            EffectEnabled = (Texture != DefaultTexture);
            
            if (loaderTask == null)
            {
                loaderTask = new ThreadedTask(new GameThumbnailLoadTask(this));
                loaderTask.Start();
            }
        }

        /// <summary>
        /// get the manual scaling if specified, or else the auto-scaling value for game thumbnail size 
        /// </summary>
        protected float ThumbnailScale
        {
            get
            {
                float sc = Game.ScaleIcon;
                if (Texture != null && sc == 1.0f)
                {
                    // scale back in width and height when needed
                    float scw = GardenGamesPanel.THUMBNAIL_MAX_WIDTH_PIXELS / ((float)Texture.Width);
                    float sch = GardenGamesPanel.THUMBNAIL_MAX_HEIGHT_PIXELS / ((float)Texture.Height);
                    sc *= (scw < sch ? scw : sch ); // scale using the smallest value found
                }
                return sc;
            }
        }

        /// <summary>
        /// (re) loads texture from a file and puts in internal updatedTexture var,
        /// which replaces the present texture in next Update() round.
        /// </summary>
        /// <returns>true if loaded ok, false if failed (e.g. no file or invalid file)</returns>
        protected bool LoadTextureFromFile()
        {
            Texture2D tex = null;
            try
            {
                tex = LoadBitmap(ThumbnailFilepath, "", true);
            }
            catch (InvalidOperationException)
            {
                return false; // TODO be able to log the error somehow
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            lock (updateTextureLock)
            {
                updatedTexture = tex;
            }
            return true;
        }

        /*
        protected Texture2D ScaleTexture(Texture2D tex, int x, int y)
        {
            GraphicsDevice cGraphicsDevice = Screen.graphicsDevice;
            int iTextureWidth = x;
            int iTextureHeight = y;

            // Backup the Graphics Device's Depth Stencil Buffer 
            DepthStencilBuffer cOldDepthStencilBuffer = cGraphicsDevice.DepthStencilBuffer;

            // Create the Render Target to draw the scaled Texture to 
            RenderTarget2D cNewRenderTarget = new RenderTarget2D(cGraphicsDevice, iTextureWidth, iTextureHeight);
            RenderTarget2D cOldRenderTarget = cGraphicsDevice.GetRenderTargets
            // Set the given Graphics Device to draw to the new Render Target 
            cGraphicsDevice.SetRenderTarget(cNewRenderTarget);

            // Make sure the Graphic Device's Depth Stencil Buffer is large enough 
            //cGraphicsDevice.DepthStencilBuffer = new DepthStencilBuffer(cGraphicsDevice, iTextureWidth, iTextureHeight, cGraphicsDevice.DepthStencilBuffer.Format);

            // Clear the scene 
            cGraphicsDevice.Clear(Color.Black);

            // Create the new SpriteBatch that will be used to scale the Texture 
            SpriteBatch cSpriteBatch = new SpriteBatch(cGraphicsDevice);

            // Draw the scaled Texture 
            cSpriteBatch.Begin(); // (SpriteBlendMode.None);
            cSpriteBatch.Draw(tex, new Rectangle(0, 0, iTextureWidth, iTextureHeight), Color.White);
            cSpriteBatch.End();

            // Restore the given Graphics Device's Render Target 
            cGraphicsDevice.SetRenderTarget(cOldRenderTarget);

            // Restore the given Graphics Device's Depth Stencil 
            //cGraphicsDevice.DepthStencilBuffer = cOldDepthStencilBuffer;

            // Set the Texture To Return to the scaled Texture 
            //Texture2D cTextureToReturn = cNewRenderTarget.GetTexture();
            return cNewRenderTarget;
        }
         */

        protected override void OnUpdate(ref UpdateParams p)
        {
            base.OnUpdate(ref p);

            // animation of loading
            if (!isLoaded)
            {
                Motion.RotateModifier += SimTime / 6.0f;
            }

            // adapt scale according to GameItem preset
            Motion.ScaleModifier *= ThumbnailScale;

            // check if a new texture has been loaded in background
            if (updatedTexture != null)
            {
                // yes: replace texture by new one
                lock (updateTextureLock)
                {
                    Texture = updatedTexture;
                    updatedTexture = null;
                    isLoaded = true;
                }
            }

            // effect on when FX mode says so, and only if thumbnail is loaded
            if (isLoaded)
                EffectEnabled = (Game.IsGrowable); // TODO && Game.IsInstalled ?;

            if (EffectEnabled)
            {
                Motion.ScaleModifier *= (1f / 0.7f); // this extends image for shader fx region, see .fx file
                if (Game.IsInstalled)
                    haloTime += p.Dt; // move the 'halo' of the icon onwards as long as it's visible.
                else
                    haloTime = 0f;
            }
        }

        protected override void OnDraw(ref DrawParams p)
        {
            if (timeParam != null)
                timeParam.SetValue(SimTime);
            if (positionParam != null)
                positionParam.SetValue(Motion.Position);

            if (Texture != null)
            {
                Color col = DrawInfo.DrawColor;
                if (EffectEnabled)
                {
                    // this is a conversion from 'halotime' to the time format that can be given to the pixel shader
                    // via the 'draw color' parameter
                    double warpedTime = 20 * (1 + Math.Sin(1.5f * MathHelper.Pi + MathHelper.TwoPi * 0.05 * (double)haloTime));
                    int t = (int)(warpedTime * 16);
                    int c3 = t % 256;
                    int c2 = ((t - c3) / 256) % 256;
                    //int c1 = ((t - c2 - c3)/65536) % 256;
                    col = new Color(col.R, c2, c3, col.A); // (intensity, timeMSB, timeLSB, alpha) passed to shader
                }
                MySpriteBatch.Draw(Texture, DrawInfo.DrawPosition, null, col,
                       Motion.RotateAbs, DrawInfo.DrawCenter, DrawInfo.DrawScale, SpriteEffects.None, DrawInfo.LayerDepth);
            }
        }
    }
}
