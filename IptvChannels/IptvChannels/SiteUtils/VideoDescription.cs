using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace MediaPortal.IptvChannels.SiteUtils
{
    public class VideoDescription
    {
        #region Variables
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        public string Title = "";

        public SiteUtilBase.VideoQualityTypes VideoQuality = SiteUtilBase.VideoQualityTypes.SD;
        public bool VideoQualityValid = false;
        public string VideoType = "";
        public string Resolution = "";
        public int ResolutionX = 0;
        public int ResolutionY = 0;
        public int ID = -1;
        public bool Type3D = false;
        public string Url = "";
        public int Bandwith = 0;

        public object Tag;
        #endregion

        #region ctor
        #endregion

        #region Public methods
        public bool ParseVideoQuality()
        {
            this.VideoQualityValid = false;

            try
            {
                Regex regex = new Regex("\\s*(?<resX>\\d+)\\s*x\\s*(?<resY>\\d+)\\s*"); // 1920x1080
                Match match = regex.Match(this.Resolution);
                if (match.Success)
                {
                    this.ResolutionX = int.Parse(match.Groups["resX"].Value);
                    this.ResolutionY = int.Parse(match.Groups["resY"].Value);

                    if (this.ResolutionY > 2160)
                        this.VideoQuality = SiteUtils.SiteUtilBase.VideoQualityTypes.UHD8K;
                    else if (this.ResolutionY > 2000)
                        this.VideoQuality = SiteUtils.SiteUtilBase.VideoQualityTypes.UHD4K;
                    else if (this.ResolutionY > 1080) 
                        this.VideoQuality = SiteUtils.SiteUtilBase.VideoQualityTypes.UHD2K;
                    else if (this.ResolutionY >= 800) 
                        this.VideoQuality = SiteUtils.SiteUtilBase.VideoQualityTypes.HD1080;
                    else if (this.ResolutionY >= 600)
                        this.VideoQuality = SiteUtils.SiteUtilBase.VideoQualityTypes.HD720;
                    else if (this.ResolutionY >= 300) 
                        this.VideoQuality = SiteUtils.SiteUtilBase.VideoQualityTypes.SD;
                    else 
                        this.VideoQuality = SiteUtils.SiteUtilBase.VideoQualityTypes.LQ;

                    this.VideoQualityValid = true;

                    _Logger.Debug(string.Format("[ParseVideoQuality] Success: '{0}' : {1}", this.Title, this.VideoQuality.ToString()));
                }
                else 
                    _Logger.Debug(string.Format("[ParseVideoQuality] Failed: '{0}'", this.Title));
            }
            catch (Exception ex)
            {
                _Logger.Error(string.Format("[ParseVideoQuality] '{0}' : {1}", this.Title, ex.Message));
            }

            return this.VideoQualityValid;
        }

        public static SiteUtils.VideoDescription SelectVideoQuality(List<SiteUtils.VideoDescription> videoList,
           SiteUtils.SiteUtilBase.VideoQualityTypes preferredVideoQuality)
        {
            if (videoList == null || videoList.Count == 0)
            {
                _Logger.Debug("[SelectVideoQuality] No playback options");
                return null;
            }
            else if (videoList.Count == 1)
            {
                _Logger.Debug("[SelectVideoQuality] Only 1 option");
                return videoList[0];
            }
            else
            {
                //Preselect last one
                SiteUtils.VideoDescription selOption = videoList[videoList.Count - 1];

                //Get the resolution for each option
                int iValidCnt = 0;
                foreach (SiteUtils.VideoDescription option in videoList)
                {
                    if (option.ParseVideoQuality())
                        iValidCnt++;
                }

                if (iValidCnt == 0) 
                    _Logger.Debug("[SelectVideoQuality] Unable to parse resolution.");
                else
                {
                    _Logger.Debug(string.Format("[SelectVideoQuality] Select {1} from {0} options.", videoList.Count, preferredVideoQuality.ToString()));

                    if (preferredVideoQuality == SiteUtils.SiteUtilBase.VideoQualityTypes.Highest || preferredVideoQuality == SiteUtils.SiteUtilBase.VideoQualityTypes.Lowest)
                    {
                        List<SiteUtils.VideoDescription> descListAll = new List<SiteUtils.VideoDescription>(videoList);

                        //Sort the description list
                        if (preferredVideoQuality == SiteUtils.SiteUtilBase.VideoQualityTypes.Highest) 
                            descListAll.Sort((p1, p2) => p2.ResolutionX.CompareTo(p1.ResolutionX));
                        else 
                            descListAll.Sort((p1, p2) => p1.ResolutionX.CompareTo(p2.ResolutionX));

                        _Logger.Debug(string.Format("[SelectVideoQuality] Selected {0}:{1}", descListAll[0].Title, descListAll[0].Url));
                        return descListAll[0];
                    }
                    else
                    {
                        SiteUtils.SiteUtilBase.VideoQualityTypes quality = preferredVideoQuality;

                        bool bLower = false;
                        while (true)
                        {
                            //Find all with requested preferred quality
                            List<SiteUtils.VideoDescription> descListFiltered = videoList.FindAll(p => p.VideoQualityValid && p.VideoQuality == quality);

                            //Sort by highest resolution
                            descListFiltered.Sort((p1, p2) => p2.ResolutionX.CompareTo(p1.ResolutionX));

                            if (descListFiltered.Count > 0)
                            {
                                //Take the best one
                                selOption = descListFiltered[0];
                                break;
                            }
                            else
                            {
                                //Preferred quality not found; try determine nearest
                                if (!bLower)
                                {
                                    //try higher quality first
                                    if (quality == SiteUtils.SiteUtilBase.VideoQualityTypes.LQ) 
                                        quality = SiteUtils.SiteUtilBase.VideoQualityTypes.SD;
                                    else if (quality == SiteUtils.SiteUtilBase.VideoQualityTypes.SD) 
                                        quality = SiteUtils.SiteUtilBase.VideoQualityTypes.HD720;
                                    else if (quality == SiteUtils.SiteUtilBase.VideoQualityTypes.HD720)
                                        quality = SiteUtils.SiteUtilBase.VideoQualityTypes.HD1080;
                                    else if (quality == SiteUtils.SiteUtilBase.VideoQualityTypes.HD1080) 
                                        quality = SiteUtils.SiteUtilBase.VideoQualityTypes.UHD2K;
                                    else if (quality == SiteUtils.SiteUtilBase.VideoQualityTypes.UHD2K)
                                        quality = SiteUtils.SiteUtilBase.VideoQualityTypes.UHD4K;
                                    else if (quality == SiteUtils.SiteUtilBase.VideoQualityTypes.UHD4K)
                                        quality = SiteUtils.SiteUtilBase.VideoQualityTypes.UHD8K;
                                    else
                                        quality = preferredVideoQuality; bLower = true;
                                }
                                else
                                {
                                    //try lower quality
                                    if (quality == SiteUtils.SiteUtilBase.VideoQualityTypes.UHD8K) 
                                        quality = SiteUtils.SiteUtilBase.VideoQualityTypes.UHD4K;
                                    else if (quality == SiteUtils.SiteUtilBase.VideoQualityTypes.UHD4K) 
                                        quality = SiteUtils.SiteUtilBase.VideoQualityTypes.UHD2K;
                                    else if (quality == SiteUtils.SiteUtilBase.VideoQualityTypes.UHD2K) 
                                        quality = SiteUtils.SiteUtilBase.VideoQualityTypes.HD1080;
                                    else if (quality == SiteUtils.SiteUtilBase.VideoQualityTypes.HD1080)
                                        quality = SiteUtils.SiteUtilBase.VideoQualityTypes.HD720;
                                    else if (quality == SiteUtils.SiteUtilBase.VideoQualityTypes.HD720)
                                        quality = SiteUtils.SiteUtilBase.VideoQualityTypes.SD;
                                    else
                                        quality = SiteUtils.SiteUtilBase.VideoQualityTypes.LQ;
                                }
                            }
                        }
                    }
                }

                _Logger.Debug(string.Format("[SelectVideoQuality] Selected: {0}:{1} .", selOption.Title, selOption.Url));
                return selOption;

            }
        }
        #endregion
    }
}
