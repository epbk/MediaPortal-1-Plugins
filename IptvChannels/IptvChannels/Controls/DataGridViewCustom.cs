using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

namespace MediaPortal.IptvChannels.Controls
{
    internal class DataGridViewCustom : DataGridView
    {
        private const int _BOX_OFFSET_DEFAULT = 5;
        private const int _BOX_SIZE = 10;

        public const int GROUP_ROW_CONTENT_OFFSET = _BOX_SIZE * 2;


        #region Private fields
        //Drag&Drop table fields
        private Rectangle _DragBoxFromMouseDown;
        private int _RowIdxOver = -1;
        private int _ColumnIdx = -1;
        private bool _RowIdxOverAfter = false;
        private Pen _PenThick = new Pen(SystemColors.ControlText, 2F);
        private List<DataGridViewRow> _RowsFromMouseDown = null;

        private static System.Collections.Hashtable _Pens = new System.Collections.Hashtable(8);
        private static System.Collections.Hashtable _Brushes = new System.Collections.Hashtable(8);

        private int _AutoscrollFast = 0;
        private DateTime _AutoscrollLast = DateTime.Now;
        #endregion

        #region Public fields
        /// <summary>
        /// Conditional predicate for rows to be moved
        /// </summary>
        public Func<IEnumerable<DataGridViewRow>, bool> RowPredicateDrag;

        /// <summary>
        /// Conditional predicate for rows to be drop on selected row
        /// </summary>
        public Func<IEnumerable<DataGridViewRow>, DataGridViewRow, bool> RowPredicateDrop;

        /// <summary>
        /// Group row tag equality predicate
        /// </summary>
        public Func<object, object, bool> GroupRowPredicateEqual = null;

        public int MainColumnIndex
        {
            get { return this._MainColumnIndex; }
            set
            {
                if (value < 0 || this.Columns.Count <= 1)
                    value = 0;
                else if (value >= this.Columns.Count)
                    value = this.Columns.Count - 1;
                else
                    this._MainColumnIndex = value;
            }
        }private int _MainColumnIndex = 0;

        public int BoxOffset
        {
            get { return this._BoxOffset; }
            set
            {
                if (value < 0)
                    this._BoxOffset = 0;
                else
                    this._BoxOffset = value;
            }
        }private int _BoxOffset = _BOX_OFFSET_DEFAULT;

        public Color GroupRowBackColor
        {
            get { return this._GroupRowBackColor; }
            set
            {
                this._GroupRowBackColor = value;
                this.Refresh();
            }
        }private Color _GroupRowBackColor = SystemColors.ButtonShadow;
        #endregion

        #region Events
        public event DataGridViewCellMouseEventHandler BeforeCellMouseDown;
        public event DataGridViewCellMouseEventHandler AfterCellMouseDown;

        /// <summary>
        /// Occurs after sucessful drop
        /// </summary>
        public event DataGridViewDropEventHandler AfterDrop;

        /// <summary>
        /// Occurs after clicking on colapsed group row
        /// </summary>
        public event DataGridViewRowEventHandler BeforeUncolapse;

        /// <summary>
        /// Occurs after internal cell painting
        /// </summary>
        public event DataGridViewCellPaintingEventHandler CellPostPaint;
        #endregion

        #region ctor
        public DataGridViewCustom()
            : this(null, null)
        {
        }
        public DataGridViewCustom(Func<IEnumerable<DataGridViewRow>, bool> rowPredicateMove, Func<IEnumerable<DataGridViewRow>, DataGridViewRow, bool> rowPredicateDrop)
            : base()
        {
            this.DoubleBuffered = true;

            this.RowPredicateDrag = rowPredicateMove;
            this.RowPredicateDrop = rowPredicateDrop;

            this.DragDrop += this.cbDragDrop;
            this.DragOver += this.cbDragOver;
            this.MouseDown += this.cbMouseDown;
            this.MouseMove += this.cbMouseMove;
            this.RowPostPaint += this.cbRowPostPaint;

            this.CellPainting += this.cbCellPainting;
            this.CellMouseClick += this.cbCellMouseClick;
            this.CellMouseDoubleClick += this.cbCellMouseDoubleClick;

            this.RowPrePaint += this.cbRowPrePaint;

            this.RowsAdded += this.cbRowsAdded;
            this.RowsRemoved += this.cbRowsRemoved;
        }
        #endregion

        #region Overrides
        protected override void OnCellMouseDown(DataGridViewCellMouseEventArgs e)
        {
            if (this.BeforeCellMouseDown != null)
                this.BeforeCellMouseDown(this, e);

            base.OnCellMouseDown(e);

            this.cbAfterCellMouseDown(this, e);

            if (this.AfterCellMouseDown != null)
                this.AfterCellMouseDown(this, e);
        }
        #endregion

        #region Callbacks
        private void cbDragDrop(object sender, DragEventArgs e)
        {
            // The mouse locations are relative to the screen, so they must be 
            // converted to client coordinates.
            Point pointClient = this.PointToClient(new Point(e.X, e.Y));

            // Get the row index of the item the mouse is below. 
            int iRowIdxTo = this.HitTest(pointClient.X, pointClient.Y).RowIndex;

            // If the drag operation was a move then remove and insert the row.
            if (e.Effect == DragDropEffects.Move && iRowIdxTo != -1)
            {
                //Get hit row
                DataGridViewRow rowHit = this.Rows[iRowIdxTo];

                //get rows to move
                List<DataGridViewRow> rowsToMove = e.Data.GetData(typeof(List<DataGridViewRow>)) as List<DataGridViewRow>;

                //Remove the selected rows
                rowsToMove.ForEach(r => this.Rows.Remove(r));

                //Starting insert index
                iRowIdxTo = rowHit.Index;

                if (this._RowIdxOverAfter)
                    iRowIdxTo++;

                //Insert back the selected rows
                rowsToMove.ForEach(r => this.Rows.Insert(iRowIdxTo++, r));

                //Invalidate old row
                int iOld = this._RowIdxOver;
                this._RowIdxOver = -1;

                //Redraw the cell to remove the visualisation line
                this.InvalidateCell(this._ColumnIdx, iOld);

                //Row preselection
                this.ClearSelection();
                this._RowsFromMouseDown.ForEach(r => r.Selected = true);
                this.FirstDisplayedCell = rowsToMove[0].Cells[this._ColumnIdx];

                //Call drop event
                if (this.AfterDrop != null)
                {
                    try { this.AfterDrop(this, new DataGridViewDropEventArgs() { MovedRows = this._RowsFromMouseDown }); }
                    catch { }
                }

                //Clear the selected rows
                this._RowsFromMouseDown = null;
            }
        }

        private void cbDragOver(object sender, DragEventArgs e)
        {
            Point pointClient = this.PointToClient(new Point(e.X, e.Y));

            //Row Idx from mouse point
            int iRowIdxTo = this.HitTest(pointClient.X, pointClient.Y).RowIndex;

            #region Autoscroll
            if (iRowIdxTo >= 0 && iRowIdxTo < this.RowCount)
            {
                int iAutoscroll = 0;

                if ((this.DisplayedRowCount(false) + this.FirstDisplayedScrollingRowIndex) == iRowIdxTo)
                {
                    //mouse points to last displayed row

                    if (this.FirstDisplayedScrollingRowIndex < this.RowCount - 1)
                        iAutoscroll = 1; //scroll down
                }
                else if (this.FirstDisplayedScrollingRowIndex == iRowIdxTo)
                {
                    //mouse points to first displayed row

                    if (this.FirstDisplayedScrollingRowIndex > 0)
                        iAutoscroll = -1; //scroll up
                }

                if (iAutoscroll != 0)
                {
                    double dMs = (DateTime.Now - this._AutoscrollLast).TotalMilliseconds;

                    if ((this._AutoscrollFast <= 0 && dMs >= 250) || dMs >= 1000)
                        this._AutoscrollFast = 3; //reset fast scroll

                    if (this._AutoscrollFast <= 0 || dMs >= 500)
                    {
                        //Do scroll
                        this.FirstDisplayedScrollingRowIndex += iAutoscroll;

                        //Scroll ts
                        this._AutoscrollLast = DateTime.Now;

                        //Decrement to-fast counter
                        if (this._AutoscrollFast > 0)
                            this._AutoscrollFast--;
                    }
                }
            }
            #endregion


            if (iRowIdxTo < 0 || (this.RowPredicateDrop != null && !this.RowPredicateDrop(this._RowsFromMouseDown, this.Rows[iRowIdxTo])))
            {
                //Not allowed

                if (this._RowIdxOver >= 0)
                {
                    int iOld = this._RowIdxOver;

                    this._RowIdxOver = -1;
                    e.Effect = DragDropEffects.None;

                    //Redraw the cell to remove the visualisation line
                    this.InvalidateCell(this._ColumnIdx, iOld);
                }
            }
            else if (this._RowIdxOver < 0)
            {
                if (!this._RowsFromMouseDown.Any(r => r.Index == iRowIdxTo) && this.Rows[iRowIdxTo].Tag != null)
                {
                    e.Effect = DragDropEffects.Move;
                    this._RowIdxOver = iRowIdxTo;
                    this._RowIdxOverAfter = iRowIdxTo > this._RowsFromMouseDown[this._RowsFromMouseDown.Count - 1].Index;

                    //Redraw the cell to display the visualisation line
                    this.InvalidateCell(this._ColumnIdx, this._RowIdxOver);
                }
            }
            else if (iRowIdxTo != this._RowIdxOver)
            {
                int iOld = this._RowIdxOver;

                if (!this._RowsFromMouseDown.Any(r => r.Index == iRowIdxTo) && this.Rows[iRowIdxTo].Tag != null)
                {
                    this._RowIdxOver = iRowIdxTo;
                    this._RowIdxOverAfter = iRowIdxTo > this._RowsFromMouseDown[this._RowsFromMouseDown.Count - 1].Index;
                    e.Effect = DragDropEffects.Move;

                    //Redraw the cell to display the visualisation line
                    this.InvalidateCell(this._ColumnIdx, this._RowIdxOver);
                }
                else
                {
                    this._RowIdxOver = -1;
                    e.Effect = DragDropEffects.None;
                }

                //Redraw the cell to remove the visualisation line
                this.InvalidateCell(this._ColumnIdx, iOld);
            }
        }

        private void cbAfterCellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left && this._RowsFromMouseDown != null)
            {
                //Make sure, all rows are selected when drag&drop
                this._RowsFromMouseDown.ForEach(r =>
                {
                    if (!r.Selected)
                        r.Selected = true;
                });
            }
        }

        private void cbMouseDown(object sender, MouseEventArgs e)
        {
            // Get the index of the item the mouse is below.
            DataGridView.HitTestInfo ht = this.HitTest(e.X - 20, e.Y);

            if (ht.ColumnIndex != -1 && ht.RowIndex != -1)
            {
                DataGridViewRow rowHit = this.Rows[ht.RowIndex];

                //Create list of selected rows
                this._RowsFromMouseDown = new List<DataGridViewRow>();
                foreach (DataGridViewRow r in this.SelectedRows)
                    this._RowsFromMouseDown.Add(r);

                //Sort selected rows
                this._RowsFromMouseDown.Sort((r1, r2) => r1.Index.CompareTo(r2.Index));

                //Row(s) check
                if (this._RowsFromMouseDown.Exists(r => r == rowHit) && (this.RowPredicateDrag == null || this.RowPredicateDrag(this._RowsFromMouseDown)))
                {
                    //OK

                    // Remember the point where the mouse down occurred. 
                    // The DragSize indicates the size that the mouse can move before a drag event should be started.
                    Size sizeDrag = SystemInformation.DragSize;

                    // Create a rectangle using the DragSize, with the mouse position being at the center of the rectangle.
                    this._DragBoxFromMouseDown = new Rectangle(new Point(e.X - (sizeDrag.Width / 2), e.Y - (sizeDrag.Height / 2)), sizeDrag);
                    this._ColumnIdx = ht.ColumnIndex;
                    return;
                }
            }

            // Reset the rectangle if the mouse is not over an item in the ListBox.
            this._DragBoxFromMouseDown = Rectangle.Empty;
            this._RowsFromMouseDown = null;
            this._RowIdxOver = -1;
            this._RowsFromMouseDown = null;
        }

        private void cbMouseMove(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left && this._RowsFromMouseDown != null)
            {
                // If the mouse moves outside the rectangle, start the drag.
                if (this._DragBoxFromMouseDown != Rectangle.Empty && !this._DragBoxFromMouseDown.Contains(e.X, e.Y))
                {
                    // Proceed with the drag and drop, passing in the list item.                    
                    DragDropEffects dropEffect = this.DoDragDrop(this._RowsFromMouseDown, DragDropEffects.Move);
                }
            }
        }

        private void cbRowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            //Draw visualisation dropping line
            if (e.RowIndex >= 0 && e.RowIndex == this._RowIdxOver)
            {
                Rectangle rec = this.GetCellDisplayRectangle(this._ColumnIdx, e.RowIndex, true);

                int iY = this._RowIdxOverAfter ? rec.Bottom - 3 : rec.Top + 3;

                e.Graphics.DrawLine(this._PenThick, rec.Left + 3, iY, rec.Right - 3, iY);
            }
        }


        private void cbRowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            DataGridViewRow row = this.Rows[e.RowIndex];

            if (row is DataGridViewCustomRow &&
                (((DataGridViewCustomRow)row).ItemType == DataGridViewRowTypeEnum.GroupColapsed || ((DataGridViewCustomRow)row).ItemType == DataGridViewRowTypeEnum.GroupUncolapsed))
            {
                e.PaintParts &= ~(DataGridViewPaintParts.Background);
                e.Graphics.FillRectangle(Pbk.Controls.Renderer.Renderer.GetCachedBrush(row.Selected ? SystemColors.Highlight : this.GroupRowBackColor), e.RowBounds);
            }
        }

        private void cbCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            DataGridViewRow row = this.Rows[e.RowIndex];
            DataGridViewRowTypeEnum itemType = row is DataGridViewCustomRow ? ((DataGridViewCustomRow)row).ItemType : DataGridViewRowTypeEnum.Item;

            if (e.ColumnIndex == this._MainColumnIndex)
            {
                if (row is DataGridViewCustomRow)
                {
                    //e.AdvancedBorderStyle.Bottom = DataGridViewAdvancedCellBorderStyle.None;

                    Pen pen = Pbk.Controls.Renderer.Renderer.GetCachedPen(this.Rows[e.RowIndex].Selected ? this.DefaultCellStyle.SelectionForeColor : this.DefaultCellStyle.ForeColor);

                    DataGridViewCell cell = row.Cells[e.ColumnIndex];
                    DataGridViewColumn col = this.Columns[e.ColumnIndex];

                    switch (itemType)
                    {
                        case DataGridViewRowTypeEnum.GroupColapsed:
                        case DataGridViewRowTypeEnum.GroupUncolapsed:
                            e.AdvancedBorderStyle.Right = DataGridViewAdvancedCellBorderStyle.None;

                            //e.Paint(e.CellBounds, row.Selected ? DataGridViewPaintParts.SelectionBackground : DataGridViewPaintParts.Background);
                            //e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground);

                            //Draw Box
                            Rectangle rec = new Rectangle(e.CellBounds.X + this._BoxOffset, e.CellBounds.Y + ((e.CellBounds.Height - _BOX_SIZE) / 2) - 1, _BOX_SIZE, _BOX_SIZE);
                            e.Graphics.DrawRectangle(pen, rec);
                            e.Graphics.DrawLine(pen, rec.X + 2, rec.Y + (rec.Height / 2), rec.X + rec.Width - 2, rec.Y + (rec.Height / 2));

                            //Colapsed Box
                            if (((DataGridViewCustomRow)row).ItemType == DataGridViewRowTypeEnum.GroupColapsed)
                                e.Graphics.DrawLine(pen, rec.X + (rec.Width / 2), rec.Y + 2, rec.X + (rec.Width / 2), rec.Y + rec.Height - 2);

                            //Draw edges
                            Pbk.Controls.Renderer.Renderer.DrawRowEdges(this, e);

                            //
                            e.CellStyle.Padding = new System.Windows.Forms.Padding(
                                col.DefaultCellStyle.Padding.Left + GROUP_ROW_CONTENT_OFFSET,
                                col.DefaultCellStyle.Padding.Top,
                                col.DefaultCellStyle.Padding.Right,
                                col.DefaultCellStyle.Padding.Bottom
                                );
                            break;

                        case DataGridViewRowTypeEnum.GroupItem:
                            //Draw default staf
                            e.CellStyle.Padding = new System.Windows.Forms.Padding(
                                col.DefaultCellStyle.Padding.Left + GROUP_ROW_CONTENT_OFFSET,
                                col.DefaultCellStyle.Padding.Top,
                                col.DefaultCellStyle.Padding.Right,
                                col.DefaultCellStyle.Padding.Bottom
                                );
                            //e.Paint(e.CellBounds, DataGridViewPaintParts.All);
                            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);

                            //Draw tree symbol
                            int iX = e.CellBounds.X + this._BoxOffset + (_BOX_SIZE / 2);
                            Point[] points = new Point[]
                            {
                                new Point(iX, e.CellBounds.Top + 3),
                                new Point(iX, e.CellBounds.Top + (e.CellBounds.Height / 2)) ,
                                new Point(iX + 9, e.CellBounds.Top + (e.CellBounds.Height / 2))
                            };

                            //pen = Renderer.GetCachedPen(this.DefaultCellStyle.ForeColor);

                            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

                            e.Graphics.DrawLines(pen, points);

                            if ((e.RowIndex + 1) < this.Rows.Count && this.Rows[e.RowIndex + 1] is DataGridViewCustomRow &&
                                ((DataGridViewCustomRow)this.Rows[e.RowIndex + 1]).ItemType == DataGridViewRowTypeEnum.GroupItem)
                                e.Graphics.DrawLine(pen, iX, e.CellBounds.Top + (e.CellBounds.Height / 2), iX, e.CellBounds.Bottom - 3);

                            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                            break;

                        default:
                            //Item
                            e.CellStyle.Padding = new System.Windows.Forms.Padding(
                                col.DefaultCellStyle.Padding.Left,
                                col.DefaultCellStyle.Padding.Top,
                                col.DefaultCellStyle.Padding.Right,
                                col.DefaultCellStyle.Padding.Bottom
                                );
                            break;
                    }

                    e.Handled = true;
                }
            }
            else
            {
                //Other group columns
                if (itemType == DataGridViewRowTypeEnum.GroupColapsed || itemType == DataGridViewRowTypeEnum.GroupUncolapsed)
                {
                    e.AdvancedBorderStyle.Right = DataGridViewAdvancedCellBorderStyle.None;

                    //Draw edges
                    Pbk.Controls.Renderer.Renderer.DrawRowEdges(this, e);

                    e.Handled = true;
                }
            }

            //Post paint event
            if (this.CellPostPaint != null)
            {
                bool bHandled = e.Handled;

                try { this.CellPostPaint(this, e); }
                catch { };

                if (bHandled)
                    e.Handled = true;
            }
        }

        private void cbCellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == this._MainColumnIndex
                && e.X >= this._BoxOffset && e.X < this._BoxOffset + _BOX_SIZE)
                this.rowClicked(this.Rows[e.RowIndex]); //Colapse/uncolapse box
        }

        private void cbCellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && IsRowGroup(this.Rows[e.RowIndex]))
                this.rowClicked(this.Rows[e.RowIndex]);
        }

        private void cbRowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            this.invalidateGroupRows();
        }

        private void cbRowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            this.invalidateGroupRows();
        }

        #endregion

        private void invalidateGroupRows()
        {
            foreach (DataGridViewRow row in this.Rows.Cast<DataGridViewRow>().Where(r => IsRowGroup(r)))
            {
                if (!((DataGridViewCustomRow)row).InvalidateNeeded)
                {
                    ((DataGridViewCustomRow)row).InvalidateNeeded = true;
                    this.InvalidateRow(row.Index);
                }
            }

            return;
        }

        private void rowClicked(DataGridViewRow row)
        {
            if (row is DataGridViewCustomRow)
            {
                //Colapse/uncolapse box

                if (IsRowGroupColapsed(row))
                    this.rowUncolapse(row.Index);
                else if (IsRowGroupUnColapsed(row))
                    this.rowColapse(row.Index);

                this.InvalidateCell(row.Cells[this._MainColumnIndex]);

                this.EndEdit();
            }
        }

        /// <summary>
        /// Uncolapse selected row.
        /// </summary>
        /// <param name="iIdx">Index of the row to be uncolapsed.</param>
        private void rowUncolapse(int iIdx)
        {
            this.rowUncolapse(this.Rows[iIdx]);
        }
        /// <summary>
        /// Uncolapse selected row.
        /// </summary>
        /// <param name="row">Row to be uncolapsed.</param>
        private void rowUncolapse(DataGridViewRow row)
        {
            if (!IsRowGroupColapsed(row))
                return;

            //Mark group row as uncolapsed
            ((DataGridViewCustomRow)row).ItemType = DataGridViewRowTypeEnum.GroupUncolapsed;

            if (this.BeforeUncolapse != null)
            {
                try { this.BeforeUncolapse(this, new DataGridViewRowEventArgs(row)); }
                catch { }
            }

        }

        /// <summary>
        /// Colapse selected row.
        /// </summary>
        /// <param name="iIdx">Index of the row to be colapsed.</param>
        private void rowColapse(int iIdx)
        {
            RowColapse(this.Rows[iIdx]);
        }
        /// <summary>
        /// Colapse selected row.
        /// </summary>
        /// <param name="iIdx">Row to be colapsed.</param>
        public void RowColapse(DataGridViewRow row)
        {
            if (!IsRowGroupUnColapsed(row))
                return;

            //Mark group row as colapsed
            ((DataGridViewCustomRow)row).ItemType = DataGridViewRowTypeEnum.GroupColapsed;

            //Point behind the group row
            int iIdx = row.Index + 1;

            //Remove all rows following the group row
            while (iIdx < this.Rows.Count)
            {
                if (IsRowGroupItem(this.Rows[iIdx]))
                    this.Rows.RemoveAt(iIdx);
                else
                    return;
            }
        }

        public DataGridViewCustomRow CreateRow()
        {
            return this.CreateRow(null, DataGridViewRowTypeEnum.Item);
        }
        public DataGridViewCustomRow CreateRow(object tag, DataGridViewRowTypeEnum type)
        {
            DataGridViewCustomRow row = new DataGridViewCustomRow();

            for (int i = 0; i < this.Columns.Count; i++)
            {
                row.Cells.Add((DataGridViewCell)this.Columns[i].CellTemplate.Clone());
            }

            row.Tag = tag;
            row.ItemType = type;
            return row;
        }

        public DataGridViewCustomRow CreateGroupRow(object tag, bool bUncolapsed, string strText)
        {
            DataGridViewCustomRow row = this.CreateRow(tag, bUncolapsed ? DataGridViewRowTypeEnum.GroupUncolapsed : DataGridViewRowTypeEnum.GroupColapsed);
            row.Cells[this._MainColumnIndex].Value = strText;
            row.Cells[this._MainColumnIndex].Style.Font = new Font(this.DefaultCellStyle.Font, FontStyle.Bold);
            //row.Height = (int)(1.1F * row.Height);

            for (int i = 0; i < row.Cells.Count; i++)
                row.Cells[i].Style.BackColor = this.GroupRowBackColor;

            return row;
        }

        public void InsertNewGroupItem(object tag, object parent, string strParent, DataGridViewCustomRow rowNew)
        {
            //New task row
            rowNew.ItemType = DataGridViewRowTypeEnum.GroupItem;

            //Append new row to the table
            if (!this.InsertNewGroupItem(parent, rowNew))
                this.AppendNewGroupWithItem(parent, strParent, rowNew, true); //group does not exist; append new group to the table
        }

        public bool InsertNewGroupItem(object parent, DataGridViewCustomRow rowNew)
        {
            for (int i = 0; i < this.Rows.Count; i++ )
            {
                DataGridViewRow row = this.Rows[i];

                if (DataGridViewCustom.IsRowGroup(row) && this.isParentEqual(parent, row.Tag))
                {
                    //Group row found

                    if (DataGridViewCustom.IsRowGroupUnColapsed(row))
                    {
                        //Goto end of the group
                        int iIdx = row.Index;
                        while (++iIdx < this.Rows.Count && DataGridViewCustom.IsRowGroupItem(this.Rows[iIdx]))
                        { }

                        //Group item row
                        rowNew.ItemType = DataGridViewRowTypeEnum.GroupItem;
                        this.Rows.Insert(iIdx, rowNew);

                        //If the previous row is group item then we need redraw tree symbol
                        if (iIdx - row.Index > 1)
                            this.InvalidateCell(this.Rows[iIdx - 1].Cells[this.MainColumnIndex]);
                    }
                    else
                        this.rowUncolapse(row.Index); //simply uncolapse

                    return true;
                }
            }

            return false;
        }

        public void AppendNewGroupWithItem(object parent, string strParent, DataGridViewCustomRow rowNew, bool bUncolapsed)
        {
            //Create new uncolapsd group with one item

            if (parent == null || this.Rows.Cast<DataGridViewRow>().Any(r => r is DataGridViewCustomRow && this.isParentEqual(parent, r.Tag)))
                return;

            //New group row
            DataGridViewRow rowNewGroup = this.CreateGroupRow(parent, rowNew != null && bUncolapsed, strParent);

            int i = 0;
            while (i < this.Rows.Count)
            {
                DataGridViewRow row = this.Rows[i];
                bool bIsRowGroup = IsRowGroup(row);
                if ((!bIsRowGroup && !IsRowGroupItem(row)) || (bIsRowGroup && strParent.CompareTo((string)row.Cells[this._MainColumnIndex].Value) < 0))
                    break;
                else
                    i++;
            }

            //Insert new group row
            this.Rows.Insert(i++, rowNewGroup);

            //Insert group item
            if (bUncolapsed && rowNew != null)
            {
                rowNew.ItemType = DataGridViewRowTypeEnum.GroupItem;
                this.Rows.Insert(i, rowNew);
            }
        }

        public int GetGroupItemCount(object parent)
        {
            return this.Rows.Cast<DataGridViewRow>().Count(r => IsRowGroupItem(r) && this.isParentEqual(parent, r.Tag));
        }

        public void InvaildateGroupRow(object parent)
        {
            if (this.InvokeRequired)
                this.BeginInvoke(new MethodInvoker(() => this.InvaildateGroupRow(parent)));
            else if (parent != null)
            {
                DataGridViewRow row = this.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => IsRowGroup(r) && this.isParentEqual(parent, r.Tag));

                if (row != null)
                {
                    ((DataGridViewCustomRow)row).InvalidateNeeded = true;
                    this.InvalidateRow(row.Index);
                    return;
                }
            }
        }

        public DataGridViewRow GroupUncolapse(object parent)
        {
            DataGridViewRow row = this.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => IsRowGroupColapsed(r) && this.isParentEqual(parent, r.Tag));
            if (row != null)
                this.rowUncolapse(row);

            return row;
        }

        public DataGridViewRow GroupColapse(object parent)
        {
            DataGridViewRow row = this.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => IsRowGroupUnColapsed(r) && this.isParentEqual(parent, r.Tag));
            if (row != null)
                this.RowColapse(row);

            return row;
        }


        public static bool IsRowGroupItem(DataGridViewRow row)
        {
            return row is DataGridViewCustomRow && ((DataGridViewCustomRow)row).ItemType == DataGridViewRowTypeEnum.GroupItem;
        }

        public static bool IsRowGroup(DataGridViewRow row)
        {
            return row is DataGridViewCustomRow &&
                (((DataGridViewCustomRow)row).ItemType == DataGridViewRowTypeEnum.GroupColapsed || ((DataGridViewCustomRow)row).ItemType == DataGridViewRowTypeEnum.GroupUncolapsed);
        }

        public static bool IsRowGroupColapsed(DataGridViewRow row)
        {
            return row is DataGridViewCustomRow && ((DataGridViewCustomRow)row).ItemType == DataGridViewRowTypeEnum.GroupColapsed;
        }

        public static bool IsRowGroupUnColapsed(DataGridViewRow row)
        {
            return row is DataGridViewCustomRow && ((DataGridViewCustomRow)row).ItemType == DataGridViewRowTypeEnum.GroupUncolapsed;
        }

        public void GroupColapseAll()
        {
            for (int i = this.Rows.Count - 1; i >= 0; i--)
            {
                DataGridViewRow row = this.Rows[i];
                if (IsRowGroupUnColapsed(row))
                    this.RowColapse(row);
            }
        }

        private bool isParentEqual(object parent, object o)
        {
            if (this.GroupRowPredicateEqual != null)
                return this.GroupRowPredicateEqual(parent, o);

            return (o is string && parent is string && ((string)o).Equals((string)parent)) || o == parent;
        }
    }
}
