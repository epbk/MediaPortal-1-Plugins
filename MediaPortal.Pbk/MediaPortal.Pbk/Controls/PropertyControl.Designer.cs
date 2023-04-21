namespace MediaPortal.Pbk.Controls
{
    partial class PropertyControl
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
            this.propertyGrid = new System.Windows.Forms.PropertyGrid();
            this.contextMenuStripPropertyGrid = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.resetValueToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.colapseAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.expandAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStripPropertyGrid.SuspendLayout();
            this.SuspendLayout();
            // 
            // propertyGrid
            // 
            this.propertyGrid.ContextMenuStrip = this.contextMenuStripPropertyGrid;
            this.propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGrid.LineColor = System.Drawing.SystemColors.ControlDark;
            this.propertyGrid.Location = new System.Drawing.Point(0, 0);
            this.propertyGrid.Name = "propertyGrid";
            this.propertyGrid.Size = new System.Drawing.Size(473, 380);
            this.propertyGrid.TabIndex = 1;
            this.propertyGrid.ToolbarVisible = false;
            this.propertyGrid.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.propertyGrid_PropertyValueChanged);
            // 
            // contextMenuStripPropertyGrid
            // 
            this.contextMenuStripPropertyGrid.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.resetValueToolStripMenuItem,
            this.toolStripSeparator1,
            this.colapseAllToolStripMenuItem,
            this.expandAllToolStripMenuItem});
            this.contextMenuStripPropertyGrid.Name = "contextMenuStripPropertyGrid";
            this.contextMenuStripPropertyGrid.Size = new System.Drawing.Size(158, 104);
            this.contextMenuStripPropertyGrid.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStripPropertyGrid_Opening);
            // 
            // resetValueToolStripMenuItem
            // 
            this.resetValueToolStripMenuItem.Name = "resetValueToolStripMenuItem";
            this.resetValueToolStripMenuItem.Size = new System.Drawing.Size(157, 24);
            this.resetValueToolStripMenuItem.Text = "Reset";
            this.resetValueToolStripMenuItem.Click += new System.EventHandler(this.resetValueToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(154, 6);
            // 
            // colapseAllToolStripMenuItem
            // 
            this.colapseAllToolStripMenuItem.Name = "colapseAllToolStripMenuItem";
            this.colapseAllToolStripMenuItem.Size = new System.Drawing.Size(157, 24);
            this.colapseAllToolStripMenuItem.Text = "Colapse All";
            this.colapseAllToolStripMenuItem.Click += new System.EventHandler(this.colapseAllToolStripMenuItem_Click);
            // 
            // expandAllToolStripMenuItem
            // 
            this.expandAllToolStripMenuItem.Name = "expandAllToolStripMenuItem";
            this.expandAllToolStripMenuItem.Size = new System.Drawing.Size(157, 24);
            this.expandAllToolStripMenuItem.Text = "Expand All";
            this.expandAllToolStripMenuItem.Click += new System.EventHandler(this.expandAllToolStripMenuItem_Click);
            // 
            // PropertyControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.propertyGrid);
            this.Name = "PropertyControl";
            this.Size = new System.Drawing.Size(473, 380);
            this.contextMenuStripPropertyGrid.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PropertyGrid propertyGrid;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripPropertyGrid;
        private System.Windows.Forms.ToolStripMenuItem resetValueToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem colapseAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem expandAllToolStripMenuItem;
    }
}
