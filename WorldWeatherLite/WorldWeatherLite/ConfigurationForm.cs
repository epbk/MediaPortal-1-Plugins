using System;
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

        private Database.dbProfile _Profile = null;

        private bool _PropertyControlInitialised = false;

        private bool _Suspending = false;

        private List<Database.dbProfile> _DeleteList = null;

        public ConfigurationForm()
        {
            this._Settings = Database.dbSettings.Instance;
            this._Settings.Upgrade();

            this.InitializeComponent();
            
            this.labelVersion.Text = "Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString();

            //Init comboboxes
            this.comboBoxFullScreenOption.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(FullscreenVideoBehaviorEnum)));

            this.comboBoxProvider.Items.Add(new Providers.ProviderMsn());
            this.comboBoxProvider.Items.Add(new Providers.ProviderForeca());
            this.comboBoxProvider.Items.Add(new Providers.ProviderAccuWeather());

            this.comboBoxUnitTemp.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(GUI.GUITemperatureUnitEnum)));
            this.comboBoxUnitPress.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(GUI.GUIPressureUnitEnum)));
            this.comboBoxUnitWind.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(GUI.GUIWindUnitEnum)));
            this.comboBoxUnitDist.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(GUI.GUIDistanceUnitEnum)));
            this.comboBoxUnitPrecip.Items.AddRange(Pbk.Utils.Enums.GetEnumNames(typeof(GUI.GUIPrecipitationUnitEnum)));

            this.comboBoxTimeZone.Items.AddRange(TimeZoneInfo.GetSystemTimeZones().ToArray());
            this.comboBoxTimeZone.SelectedIndex = 0;

            //Load settings
            this.comboBoxFullScreenOption.SelectedIndex = (int)this._Settings.FullscreenVideoBehavior;
                        
            //Profiles
            Database.dbProfile loc = Database.dbProfile.Get(-1); //pick first or create new
            Database.dbProfile.GetAll().ForEach(p => this.toolStripComboBoxProfiles.Items.Add(p));
            this.toolStripComboBoxProfiles.SelectedItem = loc;
        }


        private void doSearch(string strQuery, Providers.IWeatherProvider prov)
        {
            if (string.IsNullOrWhiteSpace(strQuery) || prov == null)
                return;

            this.dataGridViewSearch.Rows.Clear();
            IEnumerable<Database.dbProfile> result;
            try
            {
                //Search by selected provider
                result = prov.Search(strQuery, this._Profile.ProviderApiKey);
            }
            catch (Exception ex)
            {
                _Logger.Error("[doSearch] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return;
            }
            finally
            {
            }

            foreach (Database.dbProfile loc in result)
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

            if (this.tabControlProfile.SelectedIndex != 0)
                this.tabControlProfile.SelectedIndex = 0;
        }


        private void buttonOK_Click(object sender, EventArgs e)
        {
            //Refresh while in fullscreen video
            this._Settings.FullscreenVideoBehavior = (FullscreenVideoBehaviorEnum)Enum.Parse(typeof(FullscreenVideoBehaviorEnum), (string)this.comboBoxFullScreenOption.SelectedItem);

            //Commit Profiles
            this.updateProfileFromControls(this._Profile);
            foreach (object o in this.toolStripComboBoxProfiles.Items)
            {
                ((Database.dbProfile)o).CommitNeeded = true;
                ((Database.dbProfile)o).Commit();
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

        private void dataGridViewSearch_SelectionChanged(object sender, EventArgs e)
        {
            if (this.dataGridViewSearch.SelectedRows.Count == 1)
            {
                Database.dbProfile loc = (Database.dbProfile)this.dataGridViewSearch.SelectedRows[0].Tag;

                Providers.IWeatherProvider prov = null;
                for (int i = 0; i < this.comboBoxProvider.Items.Count; i++)
                {
                    prov = (Providers.IWeatherProvider)this.comboBoxProvider.Items[i];
                    if (prov.Type == loc.Provider)
                        break;
                }

                prov.FinalizeLocationData(loc);

                this._Profile.Name = loc.Name;
                this._Profile.LocationID = loc.LocationID;
                this._Profile.Provider = loc.Provider;
                this._Profile.ObservationLocation = loc.ObservationLocation;
                this._Profile.Country = loc.Country;
                this._Profile.Longitude = loc.Longitude;
                this._Profile.Latitude = loc.Latitude;
                this._Profile.ProviderApiKey = loc.ProviderApiKey;
                this.loadProfileIntoControls(this._Profile, false);
            }
        }

        private void checkBoxFahrenheit_CheckedChanged(object sender, EventArgs e)
        {
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

            this.updateMediaMoveButtons();
        }

        private void tabControlProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.tabControlProfile.SelectedIndex == 1 && !this._PropertyControlInitialised)
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
            if (!this._Suspending)
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
        }

        private void dataGridViewMedia_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (!this._Suspending)
            {
                if (this.dataGridViewMedia.CurrentCell != null && this.dataGridViewMedia.CurrentCell.ColumnIndex == _COLUMN_INDEX_MEDIA_EN)
                    this.dataGridViewMedia.EndEdit();
            }
        }

        private void toolStripButtonAddProfile_Click(object sender, EventArgs e)
        {
            Database.dbProfile loc = new Database.dbProfile()
            {
                Name = "New location",
                Provider = Providers.ProviderTypeEnum.FORECA
            };

            this.toolStripComboBoxProfiles.Items.Add(loc);
            this.toolStripComboBoxProfiles.SelectedItem = loc;
        }

        private void toolStripButtonRemoveProfile_Click(object sender, EventArgs e)
        {
            //Remember current index
            int i = this.toolStripComboBoxProfiles.SelectedIndex;

            //Add to delete list
            if (this._DeleteList == null)
                this._DeleteList = new List<Database.dbProfile>();

            this._DeleteList.Add(this._Profile);

            //Remove profile from the combobox
            this.toolStripComboBoxProfiles.Items.Remove(this._Profile);

            //Select another profile
            this.toolStripComboBoxProfiles.SelectedIndex = (i >= this.toolStripComboBoxProfiles.Items.Count ? i - 1 : i);
        }

        private void toolStripComboBoxProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!this._Suspending)
            {
                if (this._Profile != null)
                    this.updateProfileFromControls(this._Profile);

                //Pick selected profile
                this._Profile = (Database.dbProfile)this.toolStripComboBoxProfiles.Items[this.toolStripComboBoxProfiles.SelectedIndex];

                this.loadProfileIntoControls(this._Profile, true);

                this.toolStripButtonRemoveProfile.Enabled = this.toolStripComboBoxProfiles.Items.Count > 1;
            }
        }        

        private void updateProfileFromControls(Database.dbProfile loc)
        {
            loc.LocationID = this.textBoxId.Text;
            loc.Name = this.textBoxLocation.Text;
            loc.Country = this.textBoxCountry.Text;
            loc.Longitude = (double)this.numericUpDownLong.Value;
            loc.Latitude = (double)this.numericUpDownLat.Value;
            loc.Provider = ((Providers.IWeatherProvider)this.comboBoxProvider.SelectedItem).Type;
            loc.TimeZone = (TimeZoneInfo)this.comboBoxTimeZone.SelectedItem;
            loc.TimeZoneID = loc.TimeZone.Id;
            loc.ProviderApiKey = this.textBoxAPIkey.Text;
            loc.WeatherRefreshInterval = (int)this.numericUpDownWeatherRefresh.Value * 60;

            //Units
            Database.dbGUIUnits units = loc.GUIUnits;
            units.GUITemperatureUnit = (GUI.GUITemperatureUnitEnum)this.comboBoxUnitTemp.SelectedIndex;
            units.GUIPressureUnit = (GUI.GUIPressureUnitEnum)this.comboBoxUnitPress.SelectedIndex;
            units.GUIWindUnit = (GUI.GUIWindUnitEnum)this.comboBoxUnitWind.SelectedIndex;
            units.GUIDistanceUnit = (GUI.GUIDistanceUnitEnum)this.comboBoxUnitDist.SelectedIndex;
            units.GUIPrecipitationUnit = (GUI.GUIPrecipitationUnitEnum)this.comboBoxUnitPrecip.SelectedIndex;

            //Holidays
            this.holidayTextBoxNewYear.Update(false);
            this.holidayTextBoxEpiphany.Update(false);
            this.holidayTextBoxHolyThurstday.Update(false);
            this.holidayTextBoxGoodFriday.Update(false);
            this.holidayTextBoxEasterSunday.Update(false);
            this.holidayTextBoxAscensionDay.Update(false);
            this.holidayTextBoxWhitSunday.Update(false);
            this.holidayTextBoxCorpusChristi.Update(false);
            this.holidayTextBoxAssumptionDay.Update(false);
            this.holidayTextBoxReformationDay.Update(false);
            this.holidayTextBoxAllSaintsDay.Update(false);
            this.holidayTextBoxChristmasDay.Update(false);
            this.holidayTextBox0.Update(false);
            this.holidayTextBox1.Update(false);
            this.holidayTextBox2.Update(false);
            this.holidayTextBox3.Update(false);
            this.holidayTextBox4.Update(false);
            loc.GUICalendarEnable = this.checkBoxCalendarEn.Checked;
        }

        private void updateProfileName()
        {
            bool bSuspending;
            this._Profile.Name = this.textBoxLocation.Text;

            if (!(bSuspending = this._Suspending))
                this._Suspending = true;

            //Dirty hack to refresh combobox
            typeof(ComboBox).InvokeMember("RefreshItems",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod,
                null,
                this.toolStripComboBoxProfiles.ComboBox,
                null);

            if (!bSuspending)
                this._Suspending = false;
        }

        private void loadProfileIntoControls(Database.dbProfile loc, bool bFullRefresh)
        {
            this._Suspending = true;

            //Basics
            this.textBoxLocation.Text = loc.Name;
            this.textBoxCountry.Text = loc.Country;
            this.textBoxId.Text = loc.LocationID;
            this.numericUpDownLong.Value = (decimal)loc.Longitude;
            this.numericUpDownLat.Value = (decimal)loc.Latitude;

            this.updateProfileName();

            //Timezone
            if (loc.TimeZone != null)
            {
                for (int i = 0; i < this.comboBoxTimeZone.Items.Count; i++)
                {
                    if (((TimeZoneInfo)this.comboBoxTimeZone.Items[i]).Id == loc.TimeZone.Id)
                    {
                        this.comboBoxTimeZone.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (bFullRefresh)
            {
                this.textBoxAPIkey.Text = loc.ProviderApiKey;
                this.numericUpDownWeatherRefresh.Value = loc.WeatherRefreshInterval / 60;

                int iSel = 0;
                for (int i = 0; i < this.comboBoxProvider.Items.Count; i++)
                {
                    if (((Providers.IWeatherProvider)this.comboBoxProvider.Items[i]).Type == loc.Provider)
                    {
                        iSel = i;
                        break;
                    }
                }

                this.comboBoxProvider.SelectedIndex = iSel;


                //Units
                Database.dbGUIUnits units = loc.GUIUnits;
                this.comboBoxUnitTemp.SelectedIndex = (int)units.GUITemperatureUnit;
                this.comboBoxUnitPress.SelectedIndex = (int)units.GUIPressureUnit;
                this.comboBoxUnitWind.SelectedIndex = (int)units.GUIWindUnit;
                this.comboBoxUnitDist.SelectedIndex = (int)units.GUIDistanceUnit;
                this.comboBoxUnitPrecip.SelectedIndex = (int)units.GUIPrecipitationUnit;

                //Holidays
                List<Database.dbHoliday> listHolidays = Database.dbHoliday.Get(loc.ID.HasValue ? (int)loc.ID : -1);
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
                this.checkBoxCalendarEn.Checked = loc.GUICalendarEnable;

                //WeatherImages
                this.propertyControlMedia.SelectedObject = null;
                List<Database.dbWeatherImage> list = loc.WeatherImages;
                for (int i = 0; i < list.Count; i++)
                {
                    Database.dbWeatherImage wi = list[i];
                    DataGridViewRow row;
                    if (this.dataGridViewMedia.Rows.Count <= i)
                    {
                        row = new DataGridViewRow();
                        row.Cells.Add(new DataGridViewTextBoxCell());
                        row.Cells.Add(new DataGridViewCheckBoxCell());
                        row.Cells.Add(new DataGridViewTextBoxCell());
                        row.Cells[_COLUMN_INDEX_MEDIA_IDX].Value = i.ToString();
                    }
                    else
                        row = this.dataGridViewMedia.Rows[i];

                    updateMediaRow(row, wi);

                    if (row.Index < 0)
                        this.dataGridViewMedia.Rows.Add(row);
                }

                if (this.dataGridViewMedia.SelectedCells.Count > 0)
                    this.propertyControlMedia.SelectedObject = this.dataGridViewMedia.Rows[this.dataGridViewMedia.SelectedCells[0].RowIndex].Tag;

            }

            this._Suspending = false;
        }

        private void textBoxLocation_TextChanged(object sender, EventArgs e)
        {
            if (!this._Suspending)
                this.updateProfileName();
        }

        private void toolStripButton_MediaMoveDown_Click(object sender, EventArgs e)
        {
            this._Suspending = true;
            int iIdx = this.dataGridViewMedia.SelectedCells[0].RowIndex;
            Database.dbWeatherImage dbImageToMove = (Database.dbWeatherImage)this.dataGridViewMedia.Rows[iIdx].Tag;
            Database.dbWeatherImage dbImageMoveTo = (Database.dbWeatherImage)this.dataGridViewMedia.Rows[iIdx + 1].Tag;
            this.mediaSwap(dbImageToMove, dbImageMoveTo);
            updateMediaRow(this.dataGridViewMedia.Rows[iIdx], dbImageToMove);
            updateMediaRow(this.dataGridViewMedia.Rows[iIdx + 1], dbImageMoveTo);
            this._Suspending = false;

            this.dataGridViewMedia.Rows[iIdx + 1].Cells[this.dataGridViewMedia.SelectedCells[0].ColumnIndex].Selected = true;
        }

        private void toolStripButton_MediaMoveUp_Click(object sender, EventArgs e)
        {
            this._Suspending = true;
            int iIdx = this.dataGridViewMedia.SelectedCells[0].RowIndex;
            Database.dbWeatherImage dbImageMoveTo = (Database.dbWeatherImage)this.dataGridViewMedia.Rows[iIdx - 1].Tag;
            Database.dbWeatherImage dbImageToMove = (Database.dbWeatherImage)this.dataGridViewMedia.Rows[iIdx].Tag;
            this.mediaSwap(dbImageToMove, dbImageMoveTo);
            updateMediaRow(this.dataGridViewMedia.Rows[iIdx - 1], dbImageMoveTo);
            updateMediaRow(this.dataGridViewMedia.Rows[iIdx], dbImageToMove);
            this._Suspending = false;

            this.dataGridViewMedia.Rows[iIdx - 1].Cells[this.dataGridViewMedia.SelectedCells[0].ColumnIndex].Selected = true;
        }

        private void updateMediaMoveButtons()
        {
            if (this.dataGridViewMedia.SelectedCells.Count == 1)
            {
                int iIdx = this.dataGridViewMedia.SelectedCells[0].RowIndex;
                this.toolStripButton_MediaMoveUp.Enabled = iIdx > 0;
                this.toolStripButton_MediaMoveDown.Enabled = iIdx < this.dataGridViewMedia.Rows.Count - 1;
            }
            else
            {
                this.toolStripButton_MediaMoveUp.Enabled = false;
                this.toolStripButton_MediaMoveDown.Enabled = false;
            }
        }

        private void mediaSwap(Database.dbWeatherImage dbImageA, Database.dbWeatherImage dbImageB)
        {
            Database.dbWeatherImage dbImageTmp = new Database.dbWeatherImage();

            dbImageA.CopyTo(dbImageTmp);
            dbImageB.CopyTo(dbImageA);
            dbImageTmp.CopyTo(dbImageB);
        }

        private static void updateMediaRow(DataGridViewRow row, Database.dbWeatherImage wi)
        {
            row.Cells[_COLUMN_INDEX_MEDIA_EN].Value = wi.Enable;
            row.Cells[_COLUMN_INDEX_MEDIA_DESCRIPTION].Value = wi.Description;
            row.Tag = wi;
        }

        private void textBoxAPIkey_TextChanged(object sender, EventArgs e)
        {
            if (!this._Suspending)
                this._Profile.ProviderApiKey = this.textBoxAPIkey.Text;
        }

        private void comboBoxProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.textBoxAPIkey.Enabled = this.comboBoxProvider.SelectedItem is Providers.ProviderAccuWeather;
        }

        private void toolStripButtonSearch_Click(object sender, EventArgs e)
        {
            this.doSearch(this.toolStripTextBoxSearch.Text, (Providers.IWeatherProvider)this.comboBoxProvider.SelectedItem);
        }

        private void toolStripTextBoxSearch_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
                this.doSearch(this.toolStripTextBoxSearch.Text, (Providers.IWeatherProvider)this.comboBoxProvider.SelectedItem);
        }
    }
}
