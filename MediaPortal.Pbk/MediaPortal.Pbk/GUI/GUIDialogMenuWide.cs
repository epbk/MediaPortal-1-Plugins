using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.GUI.Library;
using MediaPortal.Dialogs;

namespace MediaPortal.Pbk.GUI
{
    public class GUIDialogMenuWide : GUIDialogMenu
    {
        public const int WINDOW_DIALOG_MENU_WIDE = 2112;

        public static IDialogbox Dialog
        {
            get
            {
                IDialogbox dialog = (IDialogbox)GUIWindowManager.GetWindow(WINDOW_DIALOG_MENU_WIDE);
                if (dialog == null)
                {
                    Pbk.GUI.GUIDialogMenuWide menu = new Pbk.GUI.GUIDialogMenuWide();
                    menu.Init();

                    GUIWindow win = menu;
                    GUIWindowManager.Add(ref win);
                    dialog = menu;
                }

                return dialog;
            }
        }

        public GUIDialogMenuWide()
        {
            this.GetID = WINDOW_DIALOG_MENU_WIDE;
        }

        public override bool Init()
        {
            return Load(GUIGraphicsContext.GetThemedSkinFile(@"\DialogMenuWide.xml"));
        }

        public override string GetModuleName()
        {
            return "Dialog menu wide";
        }
    }
}
