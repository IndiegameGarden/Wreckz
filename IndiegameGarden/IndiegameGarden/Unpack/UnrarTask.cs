﻿// (c) 2010-2013 TranceTrance.com. Distributed under the FreeBSD license in LICENSE.txt

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using IndiegameGarden.Base;
using IndiegameGarden.Download;

namespace IndiegameGarden.Unpack
{
    /**
     * Task that Unrars a .rar archive using the Unrar class
     */
    public class UnrarTask: Task
    {
        string filename;
        string destfolder;
        double progressContributionSingleFile = 0;
        double currentProgressMultiplier = 0;
        double progressTotal = 0;
        Unrar unrar;
        bool isAborted = false;

        public UnrarTask(string filename, string destfolder)
        {
            this.filename = filename;
            this.destfolder = destfolder;
        }

        public override double Progress()
        {
            double p = progressTotal + progressContributionSingleFile;
            // calculation is non-exact, so impose a ceiling to progress [0,1]
            if (p > 1)
                p = 1;
            return p;
        }

        protected override void StartInternal()
        {
            unrar = null;
            try
            {
                FileInfo fi = new FileInfo(filename);
                long rarBytesTotal = fi.Length;
                unrar = new Unrar(filename);
                unrar.Open();
                unrar.ExtractionProgress += new ExtractionProgressHandler(EvHandlerExtractionProgress);
                while (unrar.ReadHeader())
                {
                    currentProgressMultiplier = ((double)unrar.CurrentFile.PackedSize) / ((double)rarBytesTotal);
                    unrar.ExtractToDirectory(destfolder);
                    progressContributionSingleFile = 0;
                    progressTotal += currentProgressMultiplier;
                    if (isAborted)
                        throw new Exception("UnrarTask aborted.");
                }
                status = ITaskStatus.SUCCESS;
            }
            catch (Exception ex)
            {
                status = ITaskStatus.FAIL;
                statusMsg = ex.ToString();
            }
            finally{
                try
                {
                    if (unrar != null)
                        unrar.Close();
                }
                catch (Exception ex)
                {
                    status = ITaskStatus.FAIL;
                    statusMsg = ex.ToString();
                }
            }
        }

        protected override void AbortInternal()
        {
            isAborted = true;
        }
        
        // called from the Unrar.cs event handler
        void EvHandlerExtractionProgress(object sender, ExtractionProgressEventArgs e)
        {
            if (isAborted)
                throw new Exception("UnrarTask aborted.");

            progressContributionSingleFile = currentProgressMultiplier * e.PercentComplete / 100;
        }
        

    }
}
