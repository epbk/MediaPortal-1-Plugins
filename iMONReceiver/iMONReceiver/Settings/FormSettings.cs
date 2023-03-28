using System;
using System.Windows.Forms;
using MediaPortal.Profile;
using System.IO;

namespace MediaPortal.Plugins.iMONReceiver.Settings
{
    public partial class FormSettings : Form
    {


        

        public FormSettings()
        {
            this.InitializeComponent();

            string strCfgDir = MediaPortal.InputDevices.InputHandler.CustomizedMappingsDirectory;

            string[] strFiles = Directory.GetFiles(strCfgDir, "iMON*.xml", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < strFiles.Length; i++)
                this.comboBoxFiles.Items.Add(Path.GetFileNameWithoutExtension(strFiles[i]));

            if (this.comboBoxFiles.Items.Count < 1)
            {
                this.button_Mapping.Enabled = false;
                return;
            }

            using (MediaPortal.Profile.Settings set = new MediaPortal.Profile.MPSettings())
            {
                this.comboBoxFiles.SelectedItem = set.GetValueAsString(Plugin.CFG_SECTION, Plugin.CFG_FILE, string.Empty);
            }

            if (this.comboBoxFiles.SelectedIndex < 0)
                this.comboBoxFiles.SelectedIndex = 0;

        }

        private void button_Mapping_Click(object sender, EventArgs e)
        {
            string strSelectedFile = (string)this.comboBoxFiles.SelectedItem;
            if (!string.IsNullOrEmpty(strSelectedFile))
            {
                MediaPortal.InputDevices.InputMappingForm dlg = new MediaPortal.InputDevices.InputMappingForm(strSelectedFile);
                dlg.ShowDialog(this);
            }
        }

        private void cbFormClosing(object sender, FormClosingEventArgs e)
        {
            if (this.comboBoxFiles.SelectedIndex >= 0)
            {
                using (MediaPortal.Profile.Settings set = new MediaPortal.Profile.MPSettings())
                {
                    set.SetValue(Plugin.CFG_SECTION, Plugin.CFG_FILE, (string)this.comboBoxFiles.SelectedItem);
                }
            }
        }
    }
}
