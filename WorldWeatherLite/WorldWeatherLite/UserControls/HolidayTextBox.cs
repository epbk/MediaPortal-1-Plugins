﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MediaPortal.Plugins.WorldWeatherLite.UserControls
{
    public partial class HolidayTextBox : UserControl
    {
        public int Day
        {
            get { return this.dayMonthTextBox.Day; }
        }

        public int Month
        {
            get { return this.dayMonthTextBox.Month; }
        }

        public string Description
        {
            get { return this.textBoxDescription.Text; }
        }

        public HolidayTextBox()
        {
            InitializeComponent();
        }

        public void Init(Database.dbHoliday holiday)
        {
            this.textBoxDescription.Text = holiday.Description;
            this.dayMonthTextBox.Init(holiday.Day, holiday.Month);

            this.Tag = holiday;
        }

        public void Commit()
        {
            Database.dbHoliday tag = (Database.dbHoliday)this.Tag;

            tag.Description = this.Description;
            tag.Day = this.Day;
            tag.Month = this.Month;

            tag.CommitNeeded = true;
            tag.Commit();
        }

    }
}