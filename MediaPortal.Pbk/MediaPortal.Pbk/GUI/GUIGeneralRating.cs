using System;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using NLog;

namespace MediaPortal.Pbk.GUI
{
    public class GUIGeneralRating : GUIDialogWindow
    {
        private static Logger _Logger = LogManager.GetCurrentClassLogger();
        public const int ID = 28380;

        public GUIGeneralRating()
        {
            this.GetID = ID;
        }

        public enum StarDisplay
        {
            FIVE_STARS = 5,
            TEN_STARS = 10
        }

        [SkinControlAttribute(6)]
        protected GUILabelControl _LblText = null;
        [SkinControlAttribute(7)]
        protected GUILabelControl _LblRating = null;
        [SkinControlAttribute(100)]
        protected GUICheckMarkControl _BtnStar1 = null;
        [SkinControlAttribute(101)]
        protected GUICheckMarkControl _BtnStar2 = null;
        [SkinControlAttribute(102)]
        protected GUICheckMarkControl _BtnStar3 = null;
        [SkinControlAttribute(103)]
        protected GUICheckMarkControl _BtnStar4 = null;
        [SkinControlAttribute(104)]
        protected GUICheckMarkControl _BtnStar5 = null;
        [SkinControlAttribute(105)]
        protected GUICheckMarkControl _BtnStar6 = null;
        [SkinControlAttribute(106)]
        protected GUICheckMarkControl _BtnStar7 = null;
        [SkinControlAttribute(107)]
        protected GUICheckMarkControl _BtnStar8 = null;
        [SkinControlAttribute(108)]
        protected GUICheckMarkControl _BtnStar9 = null;
        [SkinControlAttribute(109)]
        protected GUICheckMarkControl _BtnStar10 = null;

        #region properties
        public string Text
        {
            get
            {
                return this._LblText.Label;
            }

            set
            {
                this._LblText.Label = value;
            }
        }

        public StarDisplay DisplayStars
        {
            get
            {
                return this._DisplayStars;
            }
            set
            {
                this._DisplayStars = value;
            }
        } public StarDisplay _DisplayStars = StarDisplay.FIVE_STARS;

        public int Rating { get; set; }
        public bool IsSubmitted { get; set; }

        #region Rate Description Properties
        public string FiveStarRateOneDesc { get; set; }
        public string FiveStarRateTwoDesc { get; set; }
        public string FiveStarRateThreeDesc { get; set; }
        public string FiveStarRateFourDesc { get; set; }
        public string FiveStarRateFiveDesc { get; set; }

        public string TenStarRateOneDesc { get; set; }
        public string TenStarRateTwoDesc { get; set; }
        public string TenStarRateThreeDesc { get; set; }
        public string TenStarRateFourDesc { get; set; }
        public string TenStarRateFiveDesc { get; set; }
        public string TenStarRateSixDesc { get; set; }
        public string TenStarRateSevenDesc { get; set; }
        public string TenStarRateEightDesc { get; set; }
        public string TenStarRateNineDesc { get; set; }
        public string TenStarRateTenDesc { get; set; }
        #endregion

        #endregion


        public override void Reset()
        {
            base.Reset();

            this.SetHeading("");
            this.SetLine(1, "");
            this.SetLine(2, "");
            this.SetLine(3, "");
            this.SetLine(4, "");
        }

        public override void DoModal(int iParentID)
        {
            this.LoadSkin();
            this.AllocResources();
            this.InitControls();
            this.updateStarVisibility();

            base.DoModal(iParentID);
        }

        public override bool Init()
        {
            return this.Load(GUIGraphicsContext.Skin + @"\dialogGeneralRating.xml");
        }

        public override void OnAction(MediaPortal.GUI.Library.Action action)
        {
            switch (action.wID)
            {
                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_1:
                    this.Rating = 1;
                    this.updateRating();
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_2:
                    this.Rating = 2;
                    this.updateRating();
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_3:
                    this.Rating = 3;
                    this.updateRating();
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_4:
                    this.Rating = 4;
                    this.updateRating();
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_5:
                    this.Rating = 5;
                    this.updateRating();
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_6:
                    if (DisplayStars == StarDisplay.FIVE_STARS)
                        break;

                    this.Rating = 6;
                    this.updateRating();
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_7:
                    if (DisplayStars == StarDisplay.FIVE_STARS)
                        break;

                    this.Rating = 7;
                    this.updateRating();
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_8:
                    if (DisplayStars == StarDisplay.FIVE_STARS)
                        break;

                    this.Rating = 8;
                    this.updateRating();
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_9:
                    if (DisplayStars == StarDisplay.FIVE_STARS)
                        break;

                    this.Rating = 9;
                    this.updateRating();
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_0:
                    if (DisplayStars == StarDisplay.FIVE_STARS)
                        break;

                    this.Rating = 10;
                    this.updateRating();
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.ACTION_SELECT_ITEM:
                    IsSubmitted = true;
                    PageDestroy();
                    return;

                case MediaPortal.GUI.Library.Action.ActionType.ACTION_PREVIOUS_MENU:
                case MediaPortal.GUI.Library.Action.ActionType.ACTION_CLOSE_DIALOG:
                case MediaPortal.GUI.Library.Action.ActionType.ACTION_CONTEXT_MENU:
                    this.IsSubmitted = false;
                    this.PageDestroy();
                    return;
            }

            base.OnAction(action);
        }

        protected override void OnClicked(int controlId, GUIControl control, MediaPortal.GUI.Library.Action.ActionType actionType)
        {
            base.OnClicked(controlId, control, actionType);

            if (control == this._BtnStar1)
            {
                this.Rating = 1;
                this.IsSubmitted = true;
                this.PageDestroy();
                return;
            }
            else if (control == this._BtnStar2)
            {
                this.Rating = 2;
                this.IsSubmitted = true;
                this.PageDestroy();
                return;
            }
            else if (control == this._BtnStar3)
            {
                this.Rating = 3;
                this.IsSubmitted = true;
                this.PageDestroy();
                return;
            }
            else if (control == this._BtnStar4)
            {
                this.Rating = 4;
                this.IsSubmitted = true;
                this.PageDestroy();
                return;
            }
            else if (control == this._BtnStar5)
            {
                this.Rating = 5;
                this.IsSubmitted = true;
                this.PageDestroy();
                return;
            }
            else if (control == this._BtnStar6)
            {
                this.Rating = 6;
                this.IsSubmitted = true;
                this.PageDestroy();
                return;
            }
            else if (control == this._BtnStar7)
            {
                this.Rating = 7;
                this.IsSubmitted = true;
                this.PageDestroy();
                return;
            }
            else if (control == this._BtnStar8)
            {
                this.Rating = 8;
                this.IsSubmitted = true;
                this.PageDestroy();
                return;
            }
            else if (control == this._BtnStar9)
            {
                this.Rating = 9;
                this.IsSubmitted = true;
                this.PageDestroy();
                return;
            }
            else if (control == this._BtnStar10)
            {
                this.Rating = 10;
                this.IsSubmitted = true;
                this.PageDestroy();
                return;
            }
        }

        public override bool OnMessage(GUIMessage message)
        {
            switch (message.Message)
            {
                case GUIMessage.MessageType.GUI_MSG_WINDOW_INIT:
                    base.OnMessage(message);
                    this.IsSubmitted = false;
                    this.updateRating();
                    return true;

                case GUIMessage.MessageType.GUI_MSG_SETFOCUS:
                    if (message.TargetControlId < 100 || message.TargetControlId > (100 + (int)this.DisplayStars))
                        break;

                    this.Rating = message.TargetControlId - 99;
                    this.updateRating();
                    break;
            }
            return base.OnMessage(message);
        }
                

        public void SetHeading(string strHeadingLine)
        {
            GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_LABEL_SET, GetID, 0, 1, 0, 0, null);
            msg.Label = strHeadingLine;
            this.OnMessage(msg);
        }

        public void SetLine(int iLineNr, string strLine)
        {
            if (iLineNr < 1)
                return;

            GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_LABEL_SET, GetID, 0, 1 + iLineNr, 0, 0, null);
            msg.Label = strLine;

            if ((msg.Label == string.Empty) || (msg.Label == ""))
                msg.Label = "  ";

            this.OnMessage(msg);
        }


        private void updateRating()
        {
            GUICheckMarkControl[] btnStars;

            if (this.DisplayStars == StarDisplay.FIVE_STARS)
            {
                btnStars = new GUICheckMarkControl[5] { 
                    this._BtnStar1, this._BtnStar2, this._BtnStar3, this._BtnStar4, this._BtnStar5
                };
            }
            else
            {
                btnStars = new GUICheckMarkControl[10] { 
                    this._BtnStar1, this._BtnStar2, this._BtnStar3, this._BtnStar4, this._BtnStar5,
					this._BtnStar6, this._BtnStar7, this._BtnStar8, this._BtnStar9, this._BtnStar10
                };
            }

            for (int i = 0; i < (int)this.DisplayStars; i++)
            {
                btnStars[i].Label = string.Empty;
                btnStars[i].Selected = (this.Rating >= i + 1);
            }
            btnStars[Rating - 1].Focus = true;

            // Display Rating Description
            if (this._LblRating != null)
            {
                this._LblRating.Label = string.Format("({0}) {1} / {2}", this.getRatingDescription(), this.Rating.ToString(), (int)this.DisplayStars);
            }
        }

        private void updateStarVisibility()
        {

            // Check skin supports 10 stars, if not fallback to 5 stars
            if (this._BtnStar10 == null && DisplayStars == StarDisplay.TEN_STARS)
                this.DisplayStars = StarDisplay.FIVE_STARS;

            // Hide star controls 6-10
            if (this.DisplayStars == StarDisplay.FIVE_STARS)
            {
                if (this._BtnStar6 != null) 
                    this._BtnStar6.Visible = false;

                if (this._BtnStar7 != null)
                    this._BtnStar7.Visible = false;

                if (this._BtnStar8 != null)
                    this._BtnStar8.Visible = false;

                if (this._BtnStar9 != null)
                    this._BtnStar9.Visible = false;

                if (this._BtnStar10 != null)
                    this._BtnStar10.Visible = false;
            }
        }

        private string getRatingDescription()
        {

            string strDescription = string.Empty;

            if (this.DisplayStars == StarDisplay.FIVE_STARS)
            {
                switch (this.Rating)
                {
                    case 1:
                        strDescription = this.FiveStarRateOneDesc;
                        break;
                    case 2:
                        strDescription = this.FiveStarRateTwoDesc;
                        break;
                    case 3:
                        strDescription = this.FiveStarRateThreeDesc;
                        break;
                    case 4:
                        strDescription = this.FiveStarRateFourDesc;
                        break;
                    case 5:
                        strDescription = this.FiveStarRateFiveDesc;
                        break;
                }
            }
            else
            {
                switch (this.Rating)
                {
                    case 1:
                        strDescription = this.TenStarRateOneDesc;
                        break;
                    case 2:
                        strDescription = this.TenStarRateTwoDesc;
                        break;
                    case 3:
                        strDescription = this.TenStarRateThreeDesc;
                        break;
                    case 4:
                        strDescription = this.TenStarRateFourDesc;
                        break;
                    case 5:
                        strDescription = this.TenStarRateFiveDesc;
                        break;
                    case 6:
                        strDescription = this.TenStarRateSixDesc;
                        break;
                    case 7:
                        strDescription = this.TenStarRateSevenDesc;
                        break;
                    case 8:
                        strDescription = this.TenStarRateEightDesc;
                        break;
                    case 9:
                        strDescription = this.TenStarRateNineDesc;
                        break;
                    case 10:
                        strDescription = this.TenStarRateTenDesc;
                        break;
                }
            }
            return strDescription;
        }

    }
}
