﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using NLog;

namespace MediaPortal.Plugins.WorldWeatherLite
{
    public partial class ConfigurationForm : Form
    {
        private const int _COLUMN_INDEX_MEDIA_IDX = 0;
        private const int _COLUMN_INDEX_MEDIA_EN = 1;
        private const int _COLUMN_INDEX_MEDIA_DESCRIPTION = 2;
        

        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        private Database.dbSettings _Settings;

        private Providers.IWeatherProvider _Provider;
        private Database.dbWeatherLoaction _Profile = null;

        
        private Providers.ProviderMsn _ProviderMsn = new Providers.ProviderMsn();
        private Providers.ProviderForeca _ProviderForeca = new Providers.ProviderForeca();
        private Providers.ProviderAccuWeather _ProviderAccuWeather = new Providers.ProviderAccuWeather();

        private bool _PropertyControlInitialised = false;

        private bool _LoadingProfile = false;
        private bool _UpdatingProfile = false;

        private List<Database.dbWeatherLoaction> _DeleteList = null;

        private ToolTip _ToolTip = new ToolTip();
    

        public ConfigurationForm(Database.dbSettings set)
        {
            this._Settings = set;

            this.InitializeComponent();

            this._ToolTip.SetToolTip(this.buttonAddProfile, "Add new profile");
            this._ToolTip.SetToolTip(this.buttonRemoveProfile, "Remove profile");
            
            this.labelVersion.Text = "Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString();

            //Init comboboxes
            this.comboBoxFullScreenOption.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(FullscreenVideoBehaviorEnum)));
            this.comboBoxProvider.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(Providers.ProviderTypeEnum)));
            this.comboBoxUnitTemp.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(GUI.GUITemperatureUnitEnum)));
            this.comboBoxUnitPress.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(GUI.GUIPressureUnitEnum)));
            this.comboBoxUnitWind.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(GUI.GUIWindUnitEnum)));
            this.comboBoxUnitDist.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(GUI.GUIDistanceUnitEnum)));
            this.comboBoxUnitPrecip.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(GUI.GUIPrecipitationUnitEnum)));

            //Load settings
            this.comboBoxUnitTemp.SelectedIndex = (int)set.GUITemperatureUnit;
            this.comboBoxUnitPress.SelectedIndex = (int)set.GUIPressureUnit;
            this.comboBoxUnitWind.SelectedIndex = (int)set.GUIWindUnit;
            this.comboBoxUnitDist.SelectedIndex = (int)set.GUIDistanceUnit;
            this.comboBoxUnitPrecip.SelectedIndex = (int)set.GUIPrecipitationUnit;

            this.comboBoxFullScreenOption.SelectedIndex = (int)set.FullscreenVideoBehavior;

            List<Database.dbWeatherImage> list = Database.dbWeatherImage.GetAll();
            for (int i = 0; i < list.Count; i++)
            {
                Database.dbWeatherImage wi = list[i];

                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewCheckBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());

                row.Cells[_COLUMN_INDEX_MEDIA_IDX].Value = i.ToString();
                row.Cells[_COLUMN_INDEX_MEDIA_EN].Value = wi.Enable;
                row.Cells[_COLUMN_INDEX_MEDIA_DESCRIPTION].Value = wi.Description;
                row.Tag = wi;

                this.dataGridViewMedia.Rows.Add(row);
            }
            
            //Holidays
            List<Database.dbHoliday> listHolidays = Database.dbHoliday.GetAll();
            int iIdx = 0;
            this.holidayTextBoxNewYear.Init(listHolidays[iIdx++]);
            this.holidayTextBoxEpiphany.Init(listHolidays[iIdx++]);
            this.holidayTextBoxHolyThurstday.Init(listHolidays[iIdx++]);
            this.holidayTextBoxGoodFriday.Init(listHolidays[iIdx++]);
            this.holidayTextBoxEasterSunday.Init(listHolidays[iIdx++]);
            this.holidayTextBoxAscensionDay.Init(listHolidays[iIdx++]);
            this.holidayTextBoxWhitSunday.Init(listHolidays[iIdx++]);
            this.holidayTextBoxCorpusChristi.Init(listHolidays[iIdx++]);
            this.holidayTextBoxAssumptionDay.Init(listHolidays[iIdx++]);
            this.holidayTextBoxReformationDay.Init(listHolidays[iIdx++]);
            this.holidayTextBoxAllSaintsDay.Init(listHolidays[iIdx++]);
            this.holidayTextBoxChristmasDay.Init(listHolidays[iIdx++]);

            this.holidayTextBox0.Init(listHolidays[iIdx++]);
            this.holidayTextBox1.Init(listHolidays[iIdx++]);
            this.holidayTextBox2.Init(listHolidays[iIdx++]);
            this.holidayTextBox3.Init(listHolidays[iIdx++]);
            this.holidayTextBox4.Init(listHolidays[iIdx++]);
            this.checkBoxCalendarEn.Checked = this._Settings.GUICalendarEnable;
            
            //Profiles
            Database.dbWeatherLoaction loc = Database.dbWeatherLoaction.Get(-1);
            Database.dbWeatherLoaction.GetAll().ForEach(p => this.comboBoxProfiles.Items.Add(p));
            this.comboBoxProfiles.SelectedItem = loc;
        }


        private void doSearch(string strQuery, Providers.IWeatherProvider prov)
        {
            if (string.IsNullOrWhiteSpace(strQuery) || prov == null)
                return;

            this.dataGridViewSearch.Rows.Clear();
            IEnumerable<Database.dbWeatherLoaction> result;
            try
            {
                //Search by selected provider
                result = prov.Search(strQuery);
            }
            catch (Exception ex)
            {
                _Logger.Error("[doSearch] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return;
            }
            finally
            {
            }

            foreach (Database.dbWeatherLoaction loc in result)
            {
                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());

                row.Cells[0].Value = loc.Name;
                row.Cells[1].Value = loc.ObservationLocation;
                row.Cells[2].Value = loc.Longitude;
                row.Cells[3].Value = loc.Latitude;
                row.Tag = loc;

                this.dataGridViewSearch.Rows.Add(row);
            }
        }


        private void buttonOK_Click(object sender, EventArgs e)
        {
            //Location
            

            //Refresh while in fullscreen video
            this._Settings.FullscreenVideoBehavior = (FullscreenVideoBehaviorEnum)Enum.Parse(typeof(FullscreenVideoBehaviorEnum), (string)this.comboBoxFullScreenOption.SelectedItem);

            //Units
            this._Settings.GUITemperatureUnit = (GUI.GUITemperatureUnitEnum)this.comboBoxUnitTemp.SelectedIndex;
            this._Settings.GUIPressureUnit = (GUI.GUIPressureUnitEnum)this.comboBoxUnitPress.SelectedIndex;
            this._Settings.GUIWindUnit = (GUI.GUIWindUnitEnum)this.comboBoxUnitWind.SelectedIndex;
            this._Settings.GUIDistanceUnit = (GUI.GUIDistanceUnitEnum)this.comboBoxUnitDist.SelectedIndex;
            this._Settings.GUIPrecipitationUnit = (GUI.GUIPrecipitationUnitEnum)this.comboBoxUnitPrecip.SelectedIndex;

            this._Settings.GUICalendarEnable = this.checkBoxCalendarEn.Checked;
     
            //Profiles
            this.updateProfileFromControls(this._Profile);
            foreach(object o in this.comboBoxProfiles.Items)
            {
                ((Database.dbWeatherLoaction)o).CommitNeeded = true;
                ((Database.dbWeatherLoaction)o).Commit();
            }

            //Delete removed profiles
            if (this._DeleteList != null)
                this._DeleteList.ForEach(p =>
                    {
                        if (p.ID.HasValue)
                            p.Delete();
                    });

            //Settings
            this._Settings.CommitNeeded = true;
            this._Settings.Commit();

            //Media images
            for (int i = 0; i < this.dataGridViewMedia.Rows.Count; i++)
            {
                Database.dbWeatherImage wi = (Database.dbWeatherImage)this.dataGridViewMedia.Rows[i].Tag;
                wi.CommitNeeded = true;
                wi.Commit();
            }

            //Holidays
            this.holidayTextBoxNewYear.Commit();
            this.holidayTextBoxEpiphany.Commit();
            this.holidayTextBoxHolyThurstday.Commit();
            this.holidayTextBoxGoodFriday.Commit();
            this.holidayTextBoxEasterSunday.Commit();
            this.holidayTextBoxAscensionDay.Commit();
            this.holidayTextBoxWhitSunday.Commit();
            this.holidayTextBoxCorpusChristi.Commit();
            this.holidayTextBoxAssumptionDay.Commit();
            this.holidayTextBoxReformationDay.Commit();
            this.holidayTextBoxAllSaintsDay.Commit();
            this.holidayTextBoxChristmasDay.Commit();
            this.holidayTextBox0.Commit();
            this.holidayTextBox1.Commit();
            this.holidayTextBox2.Commit();
            this.holidayTextBox3.Commit();
            this.holidayTextBox4.Commit();

            this.Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void ConfigurationForm_Load(object sender, EventArgs e)
        {
            
        }

        private void ConfigurationForm_Shown(object sender, EventArgs e)
        {
            
        }

        private void buttonSearch_Click(object sender, EventArgs e)
        {
            this.doSearch(this.textBoxSearchQuery.Text, this._Provider);
        }

        private void dataGridViewSearch_SelectionChanged(object sender, EventArgs e)
        {
            if (this.dataGridViewSearch.SelectedRows.Count == 1)
            {
                Database.dbWeatherLoaction loc = (Database.dbWeatherLoaction)this.dataGridViewSearch.SelectedRows[0].Tag;

                this.textBoxLocation.Text = loc.Name;
                this.textBoxId.Text = loc.LocationID;
                this.numericUpDownLong.Value = (decimal)loc.Longitude;
                this.numericUpDownLat.Value = (decimal)loc.Latitude;
                this.textBoxCountry.Text = loc.Country;
            }
        }

        private void checkBoxFahrenheit_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void textBoxSearchQuery_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
                this.doSearch(this.textBoxSearchQuery.Text, this._Provider);
        }

        private void comboBoxProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.textBoxId.Text = string.Empty;
            //this.textBoxLocation.Text = string.Empty;
            //this.numericUpDownLong.Value = 0;
            //this.numericUpDownLat.Value = 0;
            //this.textBoxCountry.Text = string.Empty;
            this.dataGridViewSearch.Rows.Clear();

            switch (this.comboBoxProvider.SelectedIndex)
            {
                case (int)Providers.ProviderTypeEnum.MSN:
                    this._Provider = this._ProviderMsn;
                    break;

                case (int)Providers.ProviderTypeEnum.ACCU_WEATHER:
                    this._Provider = this._ProviderAccuWeather;
                    break;

                default:
                case (int)Providers.ProviderTypeEnum.FORECA:
                    this._Provider = this._ProviderForeca;
                    break;
            }
        }

        private void toolStripButtonHelp_Click(object sender, EventArgs e)
        {
            MediaHelpForm form = new MediaHelpForm();
            form.ShowDialog();
        }

        private void dataGridViewMedia_SelectionChanged(object sender, EventArgs e)
        {
            if (this.dataGridViewMedia.SelectedCells.Count == 1)
                this.propertyControlMedia.SelectedObject = this.dataGridViewMedia.Rows[this.dataGridViewMedia.SelectedCells[0].RowIndex].Tag;
            else
                this.propertyControlMedia.SelectedObject = null;
        }

        private void tabControlMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.tabControlMain.SelectedIndex == 1 && !this._PropertyControlInitialised)
            {
                this.propertyControlMedia.ColumnWidth = 150;
                this._PropertyControlInitialised = true;
            }
        }

        private void propertyControlMedia_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (e.ChangedItem.Label == "Enable" || e.ChangedItem.Label == "Description")
            {

                for (int i = 0; i < this.dataGridViewMedia.RowCount; i++)
                {
                    if (this.dataGridViewMedia.Rows[i].Tag == this.propertyControlMedia.SelectedObject)
                    {
                        Database.dbWeatherImage wi = (Database.dbWeatherImage)this.propertyControlMedia.SelectedObject;
                        this.dataGridViewMedia.Rows[i].Cells[_COLUMN_INDEX_MEDIA_EN].Value = wi.Enable;
                        this.dataGridViewMedia.Rows[i].Cells[_COLUMN_INDEX_MEDIA_DESCRIPTION].Value = wi.Description;

                        break;

                    }
                }
            }
        }

        private void dataGridViewMedia_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                if (e.ColumnIndex == _COLUMN_INDEX_MEDIA_EN)
                {
                    ((Database.dbWeatherImage)this.propertyControlMedia.SelectedObject).Enable = (bool)this.dataGridViewMedia.Rows[e.RowIndex].Cells[_COLUMN_INDEX_MEDIA_EN].Value;
                    this.propertyControlMedia.Refresh();
                }
                else if (e.ColumnIndex == _COLUMN_INDEX_MEDIA_DESCRIPTION)
                {
                    ((Database.dbWeatherImage)this.propertyControlMedia.SelectedObject).Description = (string)this.dataGridViewMedia.Rows[e.RowIndex].Cells[_COLUMN_INDEX_MEDIA_DESCRIPTION].Value;
                    this.propertyControlMedia.Refresh();
                }
            }
        }

        private void dataGridViewMedia_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (this.dataGridViewMedia.CurrentCell != null && this.dataGridViewMedia.CurrentCell.ColumnIndex == _COLUMN_INDEX_MEDIA_EN)
                this.dataGridViewMedia.EndEdit();
        }

        private void buttonAddProfile_Click(object sender, EventArgs e)
        {
            Database.dbWeatherLoaction loc = new Database.dbWeatherLoaction()
            {
                Name = "New location",
                Provider = Providers.ProviderTypeEnum.FORECA
            };

            this.comboBoxProfiles.Items.Add(loc);
            this.comboBoxProfiles.SelectedItem = loc;
        }

        private void buttonRemoveProfile_Click(object sender, EventArgs e)
        {
            //Remember current index
            int i = this.comboBoxProfiles.SelectedIndex;

            //Add to delete list
            if (this._DeleteList == null)
                this._DeleteList = new List<Database.dbWeatherLoaction>();

            this._DeleteList.Add(this._Profile);

            //Remove profile from the combobox
            this.comboBoxProfiles.Items.Remove(this._Profile);

            //Select another profile
            this.comboBoxProfiles.SelectedIndex = (i >= this.comboBoxProfiles.Items.Count ? i - 1 : i);
        }

        private void comboBoxProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!this._UpdatingProfile)
            {
                if (this._Profile != null)
                    this.updateProfileFromControls(this._Profile);

                //Pick selected profile
                this._Profile = (Database.dbWeatherLoaction)this.comboBoxProfiles.Items[this.comboBoxProfiles.SelectedIndex];

                this.loadProfileIntoControls(this._Profile);

                this.buttonRemoveProfile.Enabled = this.comboBoxProfiles.Items.Count > 1;
            }
        }        

        private void updateProfileFromControls(Database.dbWeatherLoaction loc)
        {
            loc.LocationID = this.textBoxId.Text;
            loc.Name = this.textBoxLocation.Text;
            loc.Country = this.textBoxCountry.Text;
            loc.Longitude = (double)this.numericUpDownLong.Value;
            loc.Latitude = (double)this.numericUpDownLat.Value;
            loc.Provider = (Providers.ProviderTypeEnum)this.comboBoxProvider.SelectedIndex;
        }

        private void loadProfileIntoControls(Database.dbWeatherLoaction loc)
        {
            this._LoadingProfile = true;
            try
            {
                this.comboBoxProvider.SelectedIndex = (int)loc.Provider;
                this.textBoxLocation.Text = loc.Name;
                this.textBoxCountry.Text = loc.Country;
                this.textBoxId.Text = loc.LocationID;
                this.numericUpDownLong.Value = (decimal)loc.Longitude;
                this.numericUpDownLat.Value = (decimal)loc.Latitude;
            }
            catch { }

            this._LoadingProfile = false;
        }

        private void textBoxLocation_TextChanged(object sender, EventArgs e)
        {
            if (!this._LoadingProfile)
            {
                this._Profile.Name = this.textBoxLocation.Text;

                this._UpdatingProfile = true;

                //Dirty hack to refresh combobox
                typeof(ComboBox).InvokeMember("RefreshItems", 
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod,
                    null,
                    this.comboBoxProfiles, 
                    null);

                this._UpdatingProfile = false;
            }
        }
    }
}
