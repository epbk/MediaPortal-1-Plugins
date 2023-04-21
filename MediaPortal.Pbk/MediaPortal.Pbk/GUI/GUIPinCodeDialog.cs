using System;
using System.Collections.Generic;
using System.Text;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using System.ComponentModel;
using System.Threading;

namespace MediaPortal.Pbk.GUI
{
    public class GUIPinCodeDialog : GUIDialogWindow
    {
        public const int ID = 9915;

        public GUIPinCodeDialog()
        {
            this.GetID = ID;
        }

        [SkinControlAttribute(6)]
        protected GUILabelControl _LabelFeedback = null;

        [SkinControlAttribute(100)]
        protected GUIImage _ImagePin1 = null;
        [SkinControlAttribute(101)]
        protected GUIImage _ImagePin2 = null;
        [SkinControlAttribute(102)]
        protected GUIImage _ImagePin3 = null;
        [SkinControlAttribute(103)]
        protected GUIImage _ImagePin4 = null;

        public string EnteredPinCode { get; set; }
        public string MasterCode { get; set; }
        public bool IsCorrect { get; set; }

        /// <summary>
        /// Message reported to use when Pin is incorrect
        /// </summary>
        public string InvalidPinMessage { get; set; }


        public override void Reset()
        {
            base.Reset();

            SetHeading("");
            SetLine(1, "");
            SetLine(2, "");
            SetLine(3, "");
            SetLine(4, "");
        }

        public override void DoModal(int ParentID)
        {
            LoadSkin();
            AllocResources();
            InitControls();
            clearPinCode();

            EnteredPinCode = "";

            base.DoModal(ParentID);
        }

        public override bool Init()
        {
            return Load(GUIGraphicsContext.Skin + @"\DialogPinCode.xml");
        }

        public override void OnAction(MediaPortal.GUI.Library.Action action)
        {
            switch (action.wID)
            {
                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_1:
                    if (this.EnteredPinCode.Length >= 4)
                        return;

                    this.EnteredPinCode = this.EnteredPinCode + "1";
                    this.updatePinCode(this.EnteredPinCode.Length);
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_2:
                    if (this.EnteredPinCode.Length >= 4)
                        return;

                    this.EnteredPinCode = this.EnteredPinCode + "2";
                    this.updatePinCode(this.EnteredPinCode.Length);
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_3:
                    if (this.EnteredPinCode.Length >= 4)
                        return;

                    this.EnteredPinCode = this.EnteredPinCode + "3";
                    this.updatePinCode(this.EnteredPinCode.Length);
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_4:
                    if (this.EnteredPinCode.Length >= 4)
                        return;

                    this.EnteredPinCode = this.EnteredPinCode + "4";
                    this.updatePinCode(this.EnteredPinCode.Length);
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_5:
                    if (this.EnteredPinCode.Length >= 4)
                        return;

                    this.EnteredPinCode = this.EnteredPinCode + "5";
                    this.updatePinCode(this.EnteredPinCode.Length);
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_6:
                    if (this.EnteredPinCode.Length >= 4)
                        return;

                    this.EnteredPinCode = this.EnteredPinCode + "6";
                    this.updatePinCode(this.EnteredPinCode.Length);
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_7:
                    if (this.EnteredPinCode.Length >= 4)
                        return;

                    this.EnteredPinCode = this.EnteredPinCode + "7";
                    this.updatePinCode(this.EnteredPinCode.Length);
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_8:
                    if (this.EnteredPinCode.Length >= 4)
                        return;

                    this.EnteredPinCode = this.EnteredPinCode + "8";
                    this.updatePinCode(this.EnteredPinCode.Length);
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_9:
                    if (this.EnteredPinCode.Length >= 4)
                        return;

                    this.EnteredPinCode = this.EnteredPinCode + "9";
                    this.updatePinCode(this.EnteredPinCode.Length);
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.REMOTE_0:
                    if (this.EnteredPinCode.Length >= 4)
                        return;

                    this.EnteredPinCode = this.EnteredPinCode + "0";
                    this.updatePinCode(this.EnteredPinCode.Length);
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.ACTION_KEY_PRESSED:
                    // some types of remotes send ACTION_KEY_PRESSED instead of REMOTE_0 - REMOTE_9 commands
                    if (this.EnteredPinCode.Length >= 4)
                        return;

                    if (action.m_key != null && action.m_key.KeyChar >= '0' && action.m_key.KeyChar <= '9')
                    {
                        char ckey = (char)action.m_key.KeyChar;
                        this.EnteredPinCode = this.EnteredPinCode + ckey;
                        this.updatePinCode(this.EnteredPinCode.Length);
                    }
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.ACTION_DELETE_ITEM:
                case MediaPortal.GUI.Library.Action.ActionType.ACTION_MOVE_DOWN:
                case MediaPortal.GUI.Library.Action.ActionType.ACTION_MOVE_LEFT:
                    if (this.EnteredPinCode.Length > 0)
                    {
                        this.EnteredPinCode = this.EnteredPinCode.Substring(0, this.EnteredPinCode.Length - 1);
                        this.updatePinCode(this.EnteredPinCode.Length);
                    }
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.ACTION_SELECT_ITEM:
                    this.PageDestroy();
                    return;

                case MediaPortal.GUI.Library.Action.ActionType.ACTION_PREVIOUS_MENU:
                case MediaPortal.GUI.Library.Action.ActionType.ACTION_CLOSE_DIALOG:
                case MediaPortal.GUI.Library.Action.ActionType.ACTION_CONTEXT_MENU:
                    this.PageDestroy();
                    return;
            }

            base.OnAction(action);
        }

        protected override void OnClicked(int controlId, GUIControl control, MediaPortal.GUI.Library.Action.ActionType actionType)
        {
            base.OnClicked(controlId, control, actionType);
        }

        public override bool OnMessage(GUIMessage message)
        {
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


        private void updatePinCode(int iPinLength)
        {
            GUIImage[] imagePins = new GUIImage[4] { this._ImagePin1, this._ImagePin2, this._ImagePin3, this._ImagePin4 };

            // Visually indicate to user the number of digits entered
            this.clearPinCode();

            for (int i = 0; i < iPinLength; i++)
            {
                imagePins[i].Visible = true;
            }

            // Check if PinCode entered is correct
            if (iPinLength == 4)
            {
                this.confirmPinCode();
            }

        }

        private void clearPinCode()
        {
            GUIImage[] imagePins = new GUIImage[4] { this._ImagePin1, this._ImagePin2, this._ImagePin3, this._ImagePin4 };

            for (int i = 0; i < 4; i++)
            {
                imagePins[i].Visible = false;
            }

            this.IsCorrect = false;

            if (_LabelFeedback != null)
                this._LabelFeedback.Label = " ";
        }

        private void confirmPinCode()
        {
            // Show Feedback to user that PinCode is incorrect
            // otherwise nothing more to do, exit
            if (this.EnteredPinCode != this.MasterCode)
            {
                this._LabelFeedback.Label = this.InvalidPinMessage;
            }
            else
            {
                this.IsCorrect = true;

                // delay shutting down the dialog so the user gets visual confirmation of the last input
                ThreadStart actions = delegate
                {
                    Thread.Sleep(500);
                    this.PageDestroy();
                };

                Thread thread = new Thread(actions);
                thread.IsBackground = true;
                thread.Start();
                return;
            }
        }
    }
}