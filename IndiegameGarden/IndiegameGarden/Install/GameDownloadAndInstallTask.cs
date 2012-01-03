﻿// (c) 2010-2012 TranceTrance.com. Distributed under the FreeBSD license in LICENSE.txt

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using IndiegameGarden.Download;
using IndiegameGarden.Base;
using MyDownloader.Core;

namespace IndiegameGarden.Install
{
    /// <summary>
    /// a Task to both download and install a game. If game file already exists locally, download is skipped.
    /// If game is already installed locally, install is skipped.
    /// </summary>
    public class GameDownloadAndInstallTask: GameDownloader
    {
        InstallTask installTask;
        enum CombinedTaskPhase { CHECK, DOWNLOAD, INSTALL, DONE };
        CombinedTaskPhase combinedTaskPhase;

        /// <summary>
        /// create new Download and Install task
        /// </summary>
        /// <param name="game">info of game to download and install</param>
        public GameDownloadAndInstallTask(IndieGame game): base(game)
        {
            combinedTaskPhase = CombinedTaskPhase.CHECK;
        }

        public override void Start()
        {
            // do the checking if already installed
            game.Refresh();
            if (game.IsInstalled)
            {
                combinedTaskPhase = CombinedTaskPhase.DONE;
                return;
            }

            // start the download task (my base class)
            combinedTaskPhase = CombinedTaskPhase.DOWNLOAD;
            base.Start();
        }

        public override void Abort()
        {
            base.Abort();
            installTask.Abort();
        }

        /// <summary>
        /// check whether currently downloading
        /// </summary>
        /// <returns>true if downloading, false otherwise</returns>
        public bool IsDownloading()
        {
            return (combinedTaskPhase == CombinedTaskPhase.DOWNLOAD);
        }

        /// <summary>
        /// check whether currently installing
        /// </summary>
        /// <returns>true if installing, false otherwise</returns>
        public bool IsInstalling()
        {
            return (combinedTaskPhase == CombinedTaskPhase.INSTALL);
        }

        public override double Progress()
        {
            switch (combinedTaskPhase)
            {
                case CombinedTaskPhase.CHECK:
                    return 0;
                case CombinedTaskPhase.DOWNLOAD:
                    return ProgressDownload();
                case CombinedTaskPhase.INSTALL:
                    return ProgressInstall();
                case CombinedTaskPhase.DONE:
                    return 1;
            }
            throw new NotImplementedException("Wrong combinedTaskPhase");
        }

        /// <summary>
        /// check progress in downloading task
        /// </summary>
        /// <returns>progress value 0...1</returns>
        public double ProgressDownload()
        {
            return base.Progress();
        }

        /// <summary>
        /// check progress in installing task
        /// </summary>
        /// <returns>progress value 0...1</returns>
        public double ProgressInstall()
        {
            if (installTask == null)
                return 0;
            return installTask.Progress();
        }

        // once download ends, start the install task
        public override void OnDownloadEnded(Downloader dl)
        {
            if (dl==null || dl.State.Equals(DownloaderState.Ended))
            {
                // if download ready and OK, start install
                combinedTaskPhase = CombinedTaskPhase.INSTALL;
                installTask = new InstallTask(game);
                installTask.Start();
            }
            else
            {
                // TODO
                throw new NotImplementedException("check code OnDownloadEnded");
            }            
        }

    }
}
