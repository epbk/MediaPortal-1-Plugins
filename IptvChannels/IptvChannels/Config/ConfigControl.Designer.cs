namespace SetupTv.Sections
{
    partial class Setup
  {
    /// <summary> 
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary> 
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Component Designer generated code

    /// <summary> 
    /// Required method for Designer support - do not modify 
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Setup));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.bsMergedChannel = new System.Windows.Forms.BindingSource(this.components);
            this.StatusTimer = new System.Windows.Forms.Timer(this.components);
            this.folderBrowserDialogTVGuide = new System.Windows.Forms.FolderBrowserDialog();
            this.tabGeneral = new System.Windows.Forms.TabPage();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.propertyGridSettings = new System.Windows.Forms.PropertyGrid();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.propertyGridPlugins = new System.Windows.Forms.PropertyGrid();
            this.comboBox_Sites = new System.Windows.Forms.ComboBox();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.buttonCreateChannel = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.buttonCopyToClipboard = new System.Windows.Forms.Button();
            this.textBoxResult = new System.Windows.Forms.TextBox();
            this.bSave = new MediaPortal.UserInterface.Controls.MPButton();
            this.tabMain = new System.Windows.Forms.TabControl();
            this.imageList = new System.Windows.Forms.ImageList(this.components);
            this.propertyGridLink = new System.Windows.Forms.PropertyGrid();
            this.buttonReset = new System.Windows.Forms.Button();
            this.dataGridView_ConList = new MediaPortal.IptvChannels.Controls.DataGridViewCustom();
            this.Column6 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewCDN = new MediaPortal.IptvChannels.Controls.DataGridViewCustom();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnDRM = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.bsMergedChannel)).BeginInit();
            this.tabGeneral.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.tabPage5.SuspendLayout();
            this.tabMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_ConList)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewCDN)).BeginInit();
            this.SuspendLayout();
            // 
            // tabGeneral
            // 
            this.tabGeneral.Controls.Add(this.tabControl1);
            this.tabGeneral.Controls.Add(this.bSave);
            this.tabGeneral.Location = new System.Drawing.Point(4, 22);
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.Padding = new System.Windows.Forms.Padding(3);
            this.tabGeneral.Size = new System.Drawing.Size(449, 429);
            this.tabGeneral.TabIndex = 1;
            this.tabGeneral.Text = "General";
            this.tabGeneral.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Location = new System.Drawing.Point(9, 6);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(437, 387);
            this.tabControl1.TabIndex = 27;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.propertyGridSettings);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(429, 361);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Settings";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // propertyGridSettings
            // 
            this.propertyGridSettings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGridSettings.LineColor = System.Drawing.SystemColors.ControlDark;
            this.propertyGridSettings.Location = new System.Drawing.Point(3, 3);
            this.propertyGridSettings.Name = "propertyGridSettings";
            this.propertyGridSettings.Size = new System.Drawing.Size(423, 355);
            this.propertyGridSettings.TabIndex = 25;
            this.propertyGridSettings.ToolbarVisible = false;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.propertyGridPlugins);
            this.tabPage2.Controls.Add(this.comboBox_Sites);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(429, 361);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Plugins";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // propertyGridPlugins
            // 
            this.propertyGridPlugins.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.propertyGridPlugins.LineColor = System.Drawing.SystemColors.ControlDark;
            this.propertyGridPlugins.Location = new System.Drawing.Point(3, 33);
            this.propertyGridPlugins.Name = "propertyGridPlugins";
            this.propertyGridPlugins.Size = new System.Drawing.Size(420, 322);
            this.propertyGridPlugins.TabIndex = 24;
            this.propertyGridPlugins.ToolbarVisible = false;
            // 
            // comboBox_Sites
            // 
            this.comboBox_Sites.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBox_Sites.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox_Sites.FormattingEnabled = true;
            this.comboBox_Sites.Location = new System.Drawing.Point(0, 6);
            this.comboBox_Sites.Name = "comboBox_Sites";
            this.comboBox_Sites.Size = new System.Drawing.Size(423, 21);
            this.comboBox_Sites.TabIndex = 25;
            this.comboBox_Sites.SelectionChangeCommitted += new System.EventHandler(this.comboBox_Sites_SelectionChangeCommitted);
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.dataGridView_ConList);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(429, 361);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Streaming";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.dataGridViewCDN);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage4.Size = new System.Drawing.Size(429, 361);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Media Server";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // tabPage5
            // 
            this.tabPage5.Controls.Add(this.buttonReset);
            this.tabPage5.Controls.Add(this.propertyGridLink);
            this.tabPage5.Controls.Add(this.buttonCreateChannel);
            this.tabPage5.Controls.Add(this.label2);
            this.tabPage5.Controls.Add(this.buttonCopyToClipboard);
            this.tabPage5.Controls.Add(this.textBoxResult);
            this.tabPage5.Location = new System.Drawing.Point(4, 22);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage5.Size = new System.Drawing.Size(429, 361);
            this.tabPage5.TabIndex = 4;
            this.tabPage5.Text = "Link Generator";
            this.tabPage5.UseVisualStyleBackColor = true;
            // 
            // buttonCreateChannel
            // 
            this.buttonCreateChannel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCreateChannel.Enabled = false;
            this.buttonCreateChannel.Location = new System.Drawing.Point(240, 327);
            this.buttonCreateChannel.Name = "buttonCreateChannel";
            this.buttonCreateChannel.Size = new System.Drawing.Size(102, 23);
            this.buttonCreateChannel.TabIndex = 20;
            this.buttonCreateChannel.Text = "Create Channel";
            this.buttonCreateChannel.UseVisualStyleBackColor = true;
            this.buttonCreateChannel.Click += new System.EventHandler(this.buttonCreateChannel_Click);
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(9, 285);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(40, 13);
            this.label2.TabIndex = 14;
            this.label2.Text = "Result:";
            // 
            // buttonCopyToClipboard
            // 
            this.buttonCopyToClipboard.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCopyToClipboard.Enabled = false;
            this.buttonCopyToClipboard.Location = new System.Drawing.Point(348, 327);
            this.buttonCopyToClipboard.Name = "buttonCopyToClipboard";
            this.buttonCopyToClipboard.Size = new System.Drawing.Size(75, 23);
            this.buttonCopyToClipboard.TabIndex = 10;
            this.buttonCopyToClipboard.Text = "Copy Link";
            this.buttonCopyToClipboard.UseVisualStyleBackColor = true;
            this.buttonCopyToClipboard.Click += new System.EventHandler(this.buttonCopyToClipboard_Click);
            // 
            // textBoxResult
            // 
            this.textBoxResult.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxResult.Location = new System.Drawing.Point(9, 301);
            this.textBoxResult.Name = "textBoxResult";
            this.textBoxResult.ReadOnly = true;
            this.textBoxResult.Size = new System.Drawing.Size(414, 20);
            this.textBoxResult.TabIndex = 9;
            // 
            // bSave
            // 
            this.bSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bSave.Location = new System.Drawing.Point(371, 399);
            this.bSave.Name = "bSave";
            this.bSave.Size = new System.Drawing.Size(72, 24);
            this.bSave.TabIndex = 23;
            this.bSave.Text = "Save";
            this.bSave.UseVisualStyleBackColor = true;
            this.bSave.Click += new System.EventHandler(this.bSave_Click);
            // 
            // tabMain
            // 
            this.tabMain.Controls.Add(this.tabGeneral);
            this.tabMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabMain.Location = new System.Drawing.Point(0, 0);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.Size = new System.Drawing.Size(457, 455);
            this.tabMain.TabIndex = 26;
            // 
            // imageList
            // 
            this.imageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList.ImageStream")));
            this.imageList.TransparentColor = System.Drawing.Color.Magenta;
            this.imageList.Images.SetKeyName(0, "Error");
            this.imageList.Images.SetKeyName(1, "Done");
            this.imageList.Images.SetKeyName(2, "Iddle");
            this.imageList.Images.SetKeyName(3, "Run");
            this.imageList.Images.SetKeyName(4, "Dn");
            this.imageList.Images.SetKeyName(5, "Disabled");
            this.imageList.Images.SetKeyName(6, "Schedule");
            this.imageList.Images.SetKeyName(7, "Folder");
            this.imageList.Images.SetKeyName(8, "OpenFolder");
            this.imageList.Images.SetKeyName(9, "Warn");
            this.imageList.Images.SetKeyName(10, "ScheduleDisabled.png");
            this.imageList.Images.SetKeyName(11, "Stopping");
            // 
            // propertyGridLink
            // 
            this.propertyGridLink.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.propertyGridLink.LineColor = System.Drawing.SystemColors.ControlDark;
            this.propertyGridLink.Location = new System.Drawing.Point(3, 3);
            this.propertyGridLink.Name = "propertyGridLink";
            this.propertyGridLink.Size = new System.Drawing.Size(423, 267);
            this.propertyGridLink.TabIndex = 26;
            this.propertyGridLink.ToolbarVisible = false;
            this.propertyGridLink.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.propertyGridLink_PropertyValueChanged);
            // 
            // buttonReset
            // 
            this.buttonReset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonReset.Location = new System.Drawing.Point(13, 327);
            this.buttonReset.Name = "buttonReset";
            this.buttonReset.Size = new System.Drawing.Size(75, 23);
            this.buttonReset.TabIndex = 27;
            this.buttonReset.Text = "Reset";
            this.buttonReset.UseVisualStyleBackColor = true;
            this.buttonReset.Click += new System.EventHandler(this.buttonReset_Click);
            // 
            // dataGridView_ConList
            // 
            this.dataGridView_ConList.AllowUserToAddRows = false;
            this.dataGridView_ConList.AllowUserToDeleteRows = false;
            this.dataGridView_ConList.AllowUserToResizeRows = false;
            this.dataGridView_ConList.BoxOffset = 5;
            this.dataGridView_ConList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView_ConList.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column6,
            this.Column3,
            this.Column4,
            this.Column5});
            this.dataGridView_ConList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView_ConList.GroupRowBackColor = System.Drawing.SystemColors.ButtonShadow;
            this.dataGridView_ConList.Location = new System.Drawing.Point(3, 3);
            this.dataGridView_ConList.MainColumnIndex = 0;
            this.dataGridView_ConList.Name = "dataGridView_ConList";
            this.dataGridView_ConList.RowHeadersVisible = false;
            this.dataGridView_ConList.RowHeadersWidth = 25;
            this.dataGridView_ConList.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dataGridView_ConList.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView_ConList.Size = new System.Drawing.Size(423, 355);
            this.dataGridView_ConList.TabIndex = 83;
            this.dataGridView_ConList.BeforeUncolapse += new System.Windows.Forms.DataGridViewRowEventHandler(this.dataGridView_ConList_BeforeUncolapse);
            this.dataGridView_ConList.CellPostPaint += new System.Windows.Forms.DataGridViewCellPaintingEventHandler(this.dataGridView_ConList_CellPostPaint);
            this.dataGridView_ConList.RowPostPaint += new System.Windows.Forms.DataGridViewRowPostPaintEventHandler(this.dataGridView_ConList_RowPostPaint);
            // 
            // Column6
            // 
            this.Column6.HeaderText = "Id";
            this.Column6.Name = "Column6";
            this.Column6.ReadOnly = true;
            this.Column6.Width = 40;
            // 
            // Column3
            // 
            this.Column3.HeaderText = "Url";
            this.Column3.Name = "Column3";
            this.Column3.ReadOnly = true;
            this.Column3.Width = 230;
            // 
            // Column4
            // 
            this.Column4.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column4.HeaderText = "Info";
            this.Column4.Name = "Column4";
            this.Column4.ReadOnly = true;
            // 
            // Column5
            // 
            this.Column5.HeaderText = "Clients";
            this.Column5.Name = "Column5";
            this.Column5.ReadOnly = true;
            this.Column5.Visible = false;
            this.Column5.Width = 50;
            // 
            // dataGridViewCDN
            // 
            this.dataGridViewCDN.AllowUserToAddRows = false;
            this.dataGridViewCDN.AllowUserToDeleteRows = false;
            this.dataGridViewCDN.AllowUserToResizeRows = false;
            this.dataGridViewCDN.BoxOffset = 5;
            this.dataGridViewCDN.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewCDN.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dataGridViewTextBoxColumn1,
            this.dataGridViewTextBoxColumn2,
            this.dataGridViewTextBoxColumn3,
            this.dataGridViewTextBoxColumn5,
            this.ColumnDRM});
            this.dataGridViewCDN.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewCDN.GroupRowBackColor = System.Drawing.SystemColors.ButtonShadow;
            this.dataGridViewCDN.Location = new System.Drawing.Point(3, 3);
            this.dataGridViewCDN.MainColumnIndex = 0;
            this.dataGridViewCDN.Name = "dataGridViewCDN";
            this.dataGridViewCDN.ReadOnly = true;
            this.dataGridViewCDN.RowHeadersVisible = false;
            this.dataGridViewCDN.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewCDN.Size = new System.Drawing.Size(423, 355);
            this.dataGridViewCDN.TabIndex = 5;
            this.dataGridViewCDN.BeforeUncolapse += new System.Windows.Forms.DataGridViewRowEventHandler(this.dataGridViewCDN_BeforeUncolapse);
            this.dataGridViewCDN.CellPostPaint += new System.Windows.Forms.DataGridViewCellPaintingEventHandler(this.dataGridViewCDN_CellPostPaint);
            this.dataGridViewCDN.RowPostPaint += new System.Windows.Forms.DataGridViewRowPostPaintEventHandler(this.dataGridViewCDN_RowPostPaint);
            // 
            // dataGridViewTextBoxColumn1
            // 
            this.dataGridViewTextBoxColumn1.HeaderText = "ID";
            this.dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            this.dataGridViewTextBoxColumn1.ReadOnly = true;
            this.dataGridViewTextBoxColumn1.Width = 50;
            // 
            // dataGridViewTextBoxColumn2
            // 
            this.dataGridViewTextBoxColumn2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle1.Padding = new System.Windows.Forms.Padding(25, 0, 0, 0);
            this.dataGridViewTextBoxColumn2.DefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridViewTextBoxColumn2.HeaderText = "Title";
            this.dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            this.dataGridViewTextBoxColumn2.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn3
            // 
            this.dataGridViewTextBoxColumn3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewTextBoxColumn3.HeaderText = "Url";
            this.dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
            this.dataGridViewTextBoxColumn3.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn5
            // 
            this.dataGridViewTextBoxColumn5.HeaderText = "Status";
            this.dataGridViewTextBoxColumn5.Name = "dataGridViewTextBoxColumn5";
            this.dataGridViewTextBoxColumn5.ReadOnly = true;
            this.dataGridViewTextBoxColumn5.Width = 80;
            // 
            // ColumnDRM
            // 
            this.ColumnDRM.HeaderText = "DRM";
            this.ColumnDRM.Name = "ColumnDRM";
            this.ColumnDRM.ReadOnly = true;
            this.ColumnDRM.Width = 70;
            // 
            // Setup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tabMain);
            this.Name = "Setup";
            this.Size = new System.Drawing.Size(457, 455);
            ((System.ComponentModel.ISupportInitialize)(this.bsMergedChannel)).EndInit();
            this.tabGeneral.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.tabPage4.ResumeLayout(false);
            this.tabPage5.ResumeLayout(false);
            this.tabPage5.PerformLayout();
            this.tabMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_ConList)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewCDN)).EndInit();
            this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.Timer StatusTimer;
    private System.Windows.Forms.FolderBrowserDialog folderBrowserDialogTVGuide;
    private System.Windows.Forms.BindingSource bsMergedChannel;
    private System.Windows.Forms.TabPage tabGeneral;
    private MediaPortal.UserInterface.Controls.MPButton bSave;
    private System.Windows.Forms.TabControl tabMain;
    private System.Windows.Forms.ComboBox comboBox_Sites;
    private System.Windows.Forms.PropertyGrid propertyGridPlugins;
    private System.Windows.Forms.TabControl tabControl1;
    private System.Windows.Forms.TabPage tabPage1;
    private System.Windows.Forms.PropertyGrid propertyGridSettings;
    private System.Windows.Forms.TabPage tabPage2;
    private System.Windows.Forms.TabPage tabPage3;
    private System.Windows.Forms.TabPage tabPage4;
    private MediaPortal.IptvChannels.Controls.DataGridViewCustom dataGridViewCDN;
    private MediaPortal.IptvChannels.Controls.DataGridViewCustom dataGridView_ConList;
    private System.Windows.Forms.TabPage tabPage5;
    private System.Windows.Forms.Button buttonCopyToClipboard;
    private System.Windows.Forms.TextBox textBoxResult;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column6;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column3;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column4;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column5;
        private System.Windows.Forms.ImageList imageList;
        private System.Windows.Forms.Button buttonCreateChannel;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn5;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnDRM;
        private System.Windows.Forms.PropertyGrid propertyGridLink;
        private System.Windows.Forms.Button buttonReset;
    }
}
