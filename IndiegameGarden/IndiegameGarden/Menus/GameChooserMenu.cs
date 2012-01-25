﻿// (c) 2010-2012 TranceTrance.com. Distributed under the FreeBSD license in LICENSE.txt

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using TTengine.Core;
using TTengine.Modifiers;

using IndiegameGarden.Base;
using IndiegameGarden.Install;

namespace IndiegameGarden.Menus
{
    /// <summary>
    /// main menu to choose a game; uses a GamePanel to delegate thumbnail rendering and navigation to
    /// </summary>
    public class GameChooserMenu: Drawlet
    {
        const double MIN_MENU_CHANGE_DELAY = 0.2f; 
        
        GameCollection gamesList;

        float lastKeypressTime = 0;
        bool wasEscPressed = false;
        bool wasEnterPressed = false;
        // the game thumbnails or items selection panel
        GamesPanel panel;        

        /// <summary>
        /// construct new menu
        /// </summary>
        public GameChooserMenu()
        {
            ActiveInState = new StateBrowsingMenu();
            panel = new GardenGamesPanel(this);
            panel.Motion.Position = new Vector2(0.0f, 0.0f);

            // get the items to display
            gamesList = GardenGame.Instance.GameLib.GetList();

            // background
            Spritelet bg = new Spritelet("flower");
            bg.Motion.Position = new Vector2(0.66667f, 0.5f);
            bg.DrawInfo.DrawColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            bg.Motion.Add(new MyFuncyModifier( delegate(float v) { return v/25.0f; }, "Rotate")); // FIXME properties no work
            Add(bg);

            // set my panel and games list
            Add(panel);
            panel.OnUpdateList(gamesList);

        }

        protected override void OnDraw(ref DrawParams p)
        {
            base.OnDraw(ref p);
        }

        /// <summary>
        /// handles all keyboard input into the menu, transforming that into events sent to GUI components
        /// </summary>
        /// <param name="p">UpdateParams from TTEngine OnUpdate()</param>
        protected void KeyboardControls(ref UpdateParams p)
        {
            KeyboardState st = Keyboard.GetState();

            // time bookkeeping
            float timeSinceLastKeypress = p.SimTime - lastKeypressTime;

            // -- check all relevant key releases
            if (!st.IsKeyDown(Keys.Escape) && wasEscPressed)
            {
                wasEscPressed = false;
                panel.OnUserInput(GamesPanel.UserInput.STOP_EXIT);
            }

            if (!st.IsKeyDown(Keys.Enter) && wasEnterPressed)
            {
                wasEnterPressed = false;
                panel.OnUserInput(GamesPanel.UserInput.STOP_SELECT);
            }

            // for new keypresses - only proceed if a key pressed and some minimal delay has passed...            
            if (timeSinceLastKeypress < MIN_MENU_CHANGE_DELAY)
                return;
            // if no keys pressed, skip further checks
            if (st.GetPressedKeys().Length == 0)
                return;

            // -- esc key
            if (st.IsKeyDown(Keys.Escape))
            {
                if (!wasEscPressed)
                {
                    panel.OnUserInput(GamesPanel.UserInput.START_EXIT);
                }
                wasEscPressed = true;
            }

            // -- a navigation key is pressed - check keys and generate action(s)
            if (st.IsKeyDown(Keys.Left)) {
                panel.OnUserInput(GamesPanel.UserInput.LEFT);                
            }
            else if (st.IsKeyDown(Keys.Right)) {
                panel.OnUserInput(GamesPanel.UserInput.RIGHT);
            }

            else if (st.IsKeyDown(Keys.Up)) {
                panel.OnUserInput(GamesPanel.UserInput.UP);
            }

            else if (st.IsKeyDown(Keys.Down)){
                panel.OnUserInput(GamesPanel.UserInput.DOWN);
            }

            if (st.IsKeyDown(Keys.Enter))
            {
                if (!wasEnterPressed)
                    panel.OnUserInput(GamesPanel.UserInput.START_SELECT);
                wasEnterPressed = true;
            }

            // (time) bookkeeping for next keypress
            lastKeypressTime = p.SimTime;
        }
       
        protected override void OnUpdate(ref UpdateParams p)
        {
            base.OnUpdate(ref p);

            // check keyboard inputs from user
            KeyboardControls(ref p);
        }

    }
}
