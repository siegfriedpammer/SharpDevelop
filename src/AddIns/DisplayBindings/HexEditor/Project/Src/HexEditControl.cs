// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Siegfried Pammer" email="sie_pam@gmx.at"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Diagnostics;

using ICSharpCode.SharpDevelop;

using HexEditor.Util;

namespace HexEditor
{
	// TODO : Make BIG FILES COMPATIBLE (Data structures are bad)
	// TODO : Add options

	/// <summary>
	/// Hexadecimal editor control.
	/// </summary>
	public partial class HexEditControl : UserControl
	{
		/// <summary>
		/// number of the first visible line (first line = 0)
		/// </summary>
		int topline;
		int caretwidth, charwidth, hexinputmodepos;
		int underscorewidth, underscorewidth3, fontheight;
		bool hexViewFocus, textViewFocus, insertmode, hexinputmode, selectionmode, handled, moved;
		bool shiftwaspressed;
		Rectangle[] selregion;
		Point[] selpoints;
		BufferManager buffer;
		Caret caret;
		SelectionManager selection;
		UndoManager undoStack;
		Color offsetForeColor, offsetBackColor, dataForeColor, dataBackColor;
		bool offsetBold, offsetItalic, offsetUnderline, dataBold, dataItalic, dataUnderline;
		Font offsetFont, dataFont;
		
		/// <summary>
		/// Event fired every time something is changed in the editor.
		/// </summary>
		[Browsable(true)]
		public event EventHandler DocumentChanged;
		
		/// <summary>
		/// On-method for DocumentChanged-event.
		/// </summary>
		/// <param name="e">The eventargs for the event</param>
		protected virtual void OnDocumentChanged(EventArgs e)
		{
			if (DocumentChanged != null) {
				DocumentChanged(this, e);
			}
		}
		
		/// <summary>
		/// Creates a new HexEditor Control with basic settings and initialises all components.
		/// </summary>
		public HexEditControl()
		{
			//
			// The InitializeComponent() call is required for Windows Forms designer support.
			//
			InitializeComponent();
			
			buffer = new BufferManager(this);
			selection = new SelectionManager(ref buffer);
			undoStack = new UndoManager();
			caret = new Caret(this, 1, fontheight, 0);
			insertmode = true;
			caretwidth = 1;
			underscorewidth = MeasureStringWidth(this.CreateGraphics(), "_", this.Font);
			underscorewidth3 = underscorewidth * 3;
			fontheight = GetFontHeight(this.Font);
			selregion = new Rectangle[] {};
			selpoints = new Point[] {};
			headertext = GetHeaderText();
			
			this.offsetFont = new Font(this.Font, FontStyle.Regular);
			this.dataFont = new Font(this.Font, FontStyle.Regular);
			
			// TODO : Implement settings
			//LoadSettings();
			
			HexEditSizeChanged(null, EventArgs.Empty);
			AdjustScrollBar();
		}
		
		/// <summary>
		/// Loads the settings out of the config file of hexeditor.
		/// </summary>
		/// <remarks>Currently not working, because there's no options dialog implemented.</remarks>
		public void LoadSettings()
		{
			string configpath = Path.GetDirectoryName(typeof(HexEditControl).Assembly.Location) + Path.DirectorySeparatorChar + "config.xml";
			
			if (!File.Exists(configpath)) return;
			
			XmlDocument file = new XmlDocument();
			file.Load(configpath);
			
			foreach (XmlElement el in file.GetElementsByTagName("Setting"))	{
				switch(el.GetAttribute("Name")) {
					case "OffsetFore" :
						this.offsetForeColor = Color.FromArgb(int.Parse(el.GetAttribute("R")), int.Parse(el.GetAttribute("G")), int.Parse(el.GetAttribute("B")));
						break;
					case "OffsetBack" :
						this.offsetBackColor = Color.FromArgb(int.Parse(el.GetAttribute("R")), int.Parse(el.GetAttribute("G")), int.Parse(el.GetAttribute("B")));
						break;
					case "DataFore" :
						this.dataForeColor = Color.FromArgb(int.Parse(el.GetAttribute("R")), int.Parse(el.GetAttribute("G")), int.Parse(el.GetAttribute("B")));
						break;
					case "DataBack" :
						this.dataBackColor = Color.FromArgb(int.Parse(el.GetAttribute("R")), int.Parse(el.GetAttribute("G")), int.Parse(el.GetAttribute("B")));
						break;
					case "OffsetStyle" :
						this.offsetBold = bool.Parse(el.GetAttribute("Bold"));
						this.offsetItalic = bool.Parse(el.GetAttribute("Italic"));
						this.offsetUnderline = bool.Parse(el.GetAttribute("Underline"));
						break;
					case "DataStyle" :
						this.dataBold = bool.Parse(el.GetAttribute("Bold"));
						this.dataItalic = bool.Parse(el.GetAttribute("Italic"));
						this.dataUnderline = bool.Parse(el.GetAttribute("Underline"));
						break;
					case "Font" :
						this.Font = new Font(el.GetAttribute("FontName"), float.Parse(el.GetAttribute("FontSize")));
						break;
				}
			}
			
			FontStyle offsetStyle = FontStyle.Regular;
			if (this.offsetBold) offsetStyle &= FontStyle.Bold;
			if (this.offsetItalic) offsetStyle &= FontStyle.Italic;
			if (this.offsetUnderline) offsetStyle &= FontStyle.Underline;
			
			this.offsetFont = new Font(this.Font, offsetStyle);
			
			FontStyle dataStyle = FontStyle.Regular;
			if (this.dataBold) dataStyle &= FontStyle.Bold;
			if (this.dataItalic) dataStyle &= FontStyle.Italic;
			if (this.dataUnderline) dataStyle &= FontStyle.Underline;
			
			this.dataFont = new Font(this.Font, dataStyle);
		}

		#region Measure functions
		/*
		 * Code from SharpDevelop TextEditor
		 * */
		
		static int GetFontHeight(Font font)
		{
			int height1 = TextRenderer.MeasureText("_", font).Height;
			int height2 = (int)Math.Ceiling(font.GetHeight());
			return Math.Max(height1, height2) + 1;
		}

		static int MeasureStringWidth(Graphics g, string word, Font font)
		{
			// This code here provides better results than MeasureString!
			// Example line that is measured wrong:
			// txt.GetPositionFromCharIndex(txt.SelectionStart)
			// (Verdana 10, highlighting makes GetP... bold) -> note the space between 'x' and '('
			// this also fixes "jumping" characters when selecting in non-monospace fonts
			// [...]
			// Replaced GDI+ measurement with GDI measurement: faster and even more exact
			return TextRenderer.MeasureText(g, word, font, new Size(short.MaxValue, short.MaxValue),
			                                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
			                                TextFormatFlags.PreserveGraphicsClipping).Width;
		}

		#endregion

		/// <summary>
		/// used to store headertext for calculation.
		/// </summary>
		string headertext = String.Empty;
		
		/// <summary>
		/// Used to get the arrow keys to the keydown event.
		/// </summary>
		/// <param name="keyData">The pressed keys.</param>
		/// <returns>true if keyData is an arrow key, otherwise false.</returns>
		protected override bool IsInputKey(Keys keyData)
		{
			switch (keyData) {
				case Keys.Down:
				case Keys.Up:
				case Keys.Left:
				case Keys.Right:
					return true;
			}
			return false;
		}

		#region Properties
		ViewMode viewMode = ViewMode.Hexadecimal;
		int bytesPerLine = 16;
		bool fitToWindowWidth;
		Font font = new Font("Courier New", 10f);
		string fileName;
		Encoding encoding = Encoding.Default;

		/// <summary>
		/// ProgressBar used to display the progress of loading saving, outside of the control.
		/// </summary>
		/// <remarks>Currently not in use</remarks>
		private ToolStripProgressBar progressBar;
		
		/// <summary>
		/// ProgressBar used to display the progress of loading saving, outside of the control.
		/// </summary>
		/// <remarks>Currently not in use</remarks>
		public ToolStripProgressBar ProgressBar {
			get { return progressBar; }
			set { progressBar = value; }
		}
		
		/// <summary>
		/// Represents the current buffer of the editor.
		/// </summary>
		public BufferManager Buffer {
			get { return buffer; }
		}
		
		/// <summary>
		/// Offers access to the current selection
		/// </summary>
		public SelectionManager Selection {
			get { return selection; }
		}
		
		/// <summary>
		/// Represents the undo stack of the editor.
		/// </summary>
		public UndoManager UndoStack {
			get { return undoStack; }
		}
		
		new public bool Enabled {
			get { return base.Enabled; }
			set {
				if (this.InvokeRequired) {
					base.Enabled = this.VScrollBar.Enabled = this.HexView.Enabled = this.TextView.Enabled = this.Side.Enabled = this.Header.Enabled = value;
				} else {
					base.Enabled = this.VScrollBar.Enabled = this.HexView.Enabled = this.TextView.Enabled = this.Side.Enabled = this.Header.Enabled = value;
				}
			}
		}
		
		/// <summary>
		/// Returns the name of the currently loaded file.
		/// </summary>
		public string FileName
		{
			get { return fileName; }
			set { fileName = value; }
		}
		
		/// <summary>
		/// Property for future use to allow user to select encoding.
		/// </summary>
		/// <remarks>Currently not in use.</remarks>
		public Encoding Encoding {
			get { return encoding; }
			set { encoding = value; }
		}
		
		/// <summary>
		/// The font used in the hex editor.
		/// </summary>
		new public Font Font {
			get { return font; }
			set {
				font = value;
				underscorewidth = MeasureStringWidth(this.CreateGraphics(), "_", this.Font);
				underscorewidth3 = underscorewidth * 3;
				fontheight = GetFontHeight(this.Font);
				this.Invalidate();
			}
		}
		
		/// <summary>
		/// The ViewMode used in the hex editor.
		/// </summary>
		public ViewMode ViewMode
		{
			get { return viewMode; }
			set {
				viewMode = value;
				SetViews();
				
				this.headertext = GetHeaderText();

				this.Invalidate();
			}
		}
		
		/// <summary>
		/// "Auto-fit width" setting.
		/// </summary>
		public bool FitToWindowWidth
		{
			get { return fitToWindowWidth; }
			set { 
				fitToWindowWidth = value;
				if (value) this.BytesPerLine = CalculateMaxBytesPerLine();
			}
		}
		
		/// <summary>
		/// Gets or sets how many bytes (chars) are displayed per line.
		/// </summary>
		public int BytesPerLine
		{
			get { return bytesPerLine; }
			set {
				if (value < 1) value = 1;
				if (value > CalculateMaxBytesPerLine()) value = CalculateMaxBytesPerLine();
				bytesPerLine = value;
				SetViews();
				
				this.headertext = GetHeaderText();
				
				this.Invalidate();
			}
		}
		
		/// <summary>
		/// Generates the current header text
		/// </summary>
		/// <returns>the header text</returns>
		string GetHeaderText()
		{
			StringBuilder text = new StringBuilder();
			for (int i = 0; i < this.BytesPerLine; i++) {
				switch (this.ViewMode) {
					case ViewMode.Decimal:
						text.Append(' ', 3 - GetLength(i));
						text.Append(i.ToString());
						break;
					case ViewMode.Hexadecimal:
						text.Append(' ', 3 - string.Format("{0:X}", i).Length);
						text.AppendFormat("{0:X}", i);
						break;
					case ViewMode.Octal:
						int tmp = i;
						string num = "";
						if (tmp == 0) num = "0";
						while (tmp != 0)
						{
							num = (tmp % 8).ToString() + num;
							tmp = (int)(tmp / 8);
						}
						text.Append(' ', 3 - num.Length);
						text.Append(num);
						break;
				}
			}
			
			return text.ToString();
		}
		#endregion
		
		#region MouseActions/Focus/ScrollBar
		/// <summary>
		/// Used to update the scrollbar.
		/// </summary>
		void AdjustScrollBar()
		{
			int linecount = this.GetMaxLines();
			
			if (linecount > GetMaxVisibleLines()) {
				// Set Vertical scrollbar
				VScrollBar.Enabled = true;
				VScrollBar.Maximum = linecount - 1;
				VScrollBar.Minimum = 0;
			} else {
				VScrollBar.Value = 0;
				VScrollBar.Enabled = false;
			}
		}
		
		/// <summary>
		/// Handles the vertical scrollbar.
		/// </summary>
		void VScrollBarScroll(object sender, ScrollEventArgs e)
		{
			SetViews();
			this.topline = VScrollBar.Value;
			Point pos = GetPositionForOffset(caret.Offset, charwidth);
			caret.SetToPosition(pos);
			
			this.Invalidate();
		}
		
		/// <summary>
		/// Handles the mouse wheel
		/// </summary>
		protected override void OnMouseWheel(MouseEventArgs e)
		{
			base.OnMouseWheel(e);

			if (!this.VScrollBar.Enabled) return;

			int delta = -(e.Delta / 3 / 10);
			int oldvalue = 0;

			if ((VScrollBar.Value + delta) > VScrollBar.Maximum) {
				oldvalue = VScrollBar.Value;
				VScrollBar.Value = VScrollBar.Maximum;
				this.VScrollBarScroll(null, new ScrollEventArgs(ScrollEventType.Last, oldvalue, VScrollBar.Value, ScrollOrientation.VerticalScroll));
			} else if ((VScrollBar.Value + delta) < VScrollBar.Minimum) {
				oldvalue = VScrollBar.Value;
				VScrollBar.Value = 0;
				this.VScrollBarScroll(null, new ScrollEventArgs(ScrollEventType.First, oldvalue, VScrollBar.Value, ScrollOrientation.VerticalScroll));
			} else {
				oldvalue = VScrollBar.Value;
				VScrollBar.Value += delta;
				if (delta > 0) this.VScrollBarScroll(null, new ScrollEventArgs(ScrollEventType.SmallIncrement, oldvalue, VScrollBar.Value, ScrollOrientation.VerticalScroll));
				if (delta < 0) this.VScrollBarScroll(null, new ScrollEventArgs(ScrollEventType.SmallDecrement, oldvalue, VScrollBar.Value, ScrollOrientation.VerticalScroll));
			}
		}
		
		/// <summary>
		/// Handles when the hexeditor was click (hexview)
		/// </summary>
		void HexViewMouseClick(object sender, MouseEventArgs e)
		{
			this.Focus();
			hexViewFocus = true;
			textViewFocus = false;
			this.charwidth = 3;
			this.caretwidth = 1;
			if (!insertmode) caretwidth = underscorewidth3;

			caret.Create(this.HexView, caretwidth, fontheight);
			
			if (e.Button != MouseButtons.Right) {
				if (!moved) {
					selection.HasSomethingSelected = false;
					selectionmode = false;
				} else {
					moved = false;
					return;
				}
			} else {
				this.Invalidate();
			}
			caret.Offset = this.GetOffsetForPosition(e.Location, 3);
			caret.SetToPosition(GetPositionForOffset(caret.Offset, 3));
			
			this.Invalidate();
			caret.Show();
		}
		
		/// <summary>
		/// Handles when the hexeditor was click (textview)
		/// </summary>
		void TextViewMouseClick(object sender, MouseEventArgs e)
		{
			this.Focus();
			hexinputmode = false;
			hexViewFocus = false;
			textViewFocus = true;
			this.charwidth = 1;
			this.caretwidth = 1;
			if (!insertmode) caretwidth = underscorewidth;
			caret.Create(this.TextView, caretwidth, fontheight);
			
			if (e.Button != MouseButtons.Right) {
				if (!moved) {
					selection.HasSomethingSelected = false;
					selectionmode = false;
				} else {
					moved = false;
					return;
				}
			}
			
			this.Focus();
			hexinputmode = false;
			hexViewFocus = false;
			textViewFocus = true;
			this.charwidth = 1;
			this.caretwidth = 1;
			if (!insertmode) caretwidth = underscorewidth;
			caret.Create(this.TextView, caretwidth, fontheight);
			caret.Offset = this.GetOffsetForPosition(e.Location, 1);
			caret.SetToPosition(GetPositionForOffset(caret.Offset, 1));
			this.Invalidate();
			caret.Show();
		}
		#endregion

		#region Painters
		/// <summary>
		/// General painting, using double buffering.
		/// </summary>
		void HexEditPaint(object sender, PaintEventArgs e)
		{
			// Refresh selection.
			CalculateSelectionRegions();
			
			// Paint using double buffering for better painting!
			
			// Bitmaps for painting
			Bitmap Header = new Bitmap(this.Width, this.Height, this.Header.CreateGraphics());
			Bitmap Side = new Bitmap(this.Side.Width, this.Side.Height, this.Side.CreateGraphics());
			Bitmap Hex = new Bitmap(this.HexView.Width, this.HexView.Height, this.HexView.CreateGraphics());
			Bitmap Text = new Bitmap(this.TextView.Width, this.TextView.Height, this.TextView.CreateGraphics());
			
			// Do painting.
			PaintHex(Graphics.FromImage(Hex), VScrollBar.Value);
			PaintOffsetNumbers(Graphics.FromImage(Side), VScrollBar.Value);
			PaintHeader(Graphics.FromImage(Header));
			PaintText(Graphics.FromImage(Text), VScrollBar.Value);
			PaintPointer(Graphics.FromImage(Hex), Graphics.FromImage(Text));
			PaintSelection(Graphics.FromImage(Hex), Graphics.FromImage(Text), true);
			
			// Calculate views and reset scrollbar.
			SetViews();
			AdjustScrollBar();
			
			// Paint on device ...
			this.Header.CreateGraphics().DrawImageUnscaled(Header, 0, 0);
			this.Side.CreateGraphics().DrawImageUnscaled(Side, 0, 0);
			this.HexView.CreateGraphics().DrawImageUnscaled(Hex, 0, 0);
			this.TextView.CreateGraphics().DrawImageUnscaled(Text, 0, 0);
			
			GC.Collect();
		}
		
		/// <summary>
		/// Draws the header text ("Offset 0 1 2 3 ...")
		/// </summary>
		/// <param name="g">The graphics device to draw on.</param>
		void PaintHeader(System.Drawing.Graphics g)
		{
			g.Clear(Color.White);
			TextRenderer.DrawText(g, headertext,
			                      this.Font, new Rectangle(1, 1, this.HexView.Width + 5, fontheight),
			                      Color.Blue, this.BackColor, TextFormatFlags.Left & TextFormatFlags.Top);
		}

		/// <summary>
		/// Draws the offset numbers for each visible line.
		/// </summary>
		/// <param name="g">The graphics device to draw on.</param>
		/// <param name="top">The top line to start.</param>
		void PaintOffsetNumbers(System.Drawing.Graphics g, int top)
		{
			g.Clear(Color.White);
			string text = String.Empty;
			int count = top + this.GetMaxVisibleLines();
			string tmpcls = String.Empty;

			StringBuilder builder = new StringBuilder("Offset\n0\n");

			for (int i = top; i < count; i++) {
				if (i == top) builder = new StringBuilder("Offset\n");
				
				if ((i * this.BytesPerLine) <= this.buffer.BufferSize) {
					switch (this.ViewMode) {
						case ViewMode.Decimal:
							builder.AppendLine((i * this.BytesPerLine).ToString());
							if (string.IsNullOrEmpty(tmpcls)) tmpcls = (i * this.BytesPerLine).ToString();
							break;
						case ViewMode.Hexadecimal:
							builder.AppendFormat("{0:X}", i * this.BytesPerLine);
							builder.AppendLine();
							if (string.IsNullOrEmpty(tmpcls)) tmpcls = string.Format("{0:X}", i * this.BytesPerLine);
							break;
						case ViewMode.Octal:
							StringBuilder num = new StringBuilder();
							int tmp = i * this.BytesPerLine;
							if (tmp == 0) num.Append("0");
							while (tmp != 0) {
								num.Insert(0, (tmp % 8).ToString());
								tmp = (int)(tmp / 8);
							}
							builder.AppendLine(num.ToString());
							if (string.IsNullOrEmpty(tmpcls)) tmpcls = num.ToString();
							break;
					}
				}
			}

			text = builder.ToString();
			builder = null;
			
			TextRenderer.DrawText(g, text, this.Font, new Rectangle(0, 0, this.Side.Width, this.Side.Height), Color.Blue, this.BackColor, TextFormatFlags.Right);
		}
		
		/// <summary>
		/// Draws the hexadecimal view of the data.
		/// </summary>
		/// <param name="g">The graphics device to draw on.</param>
		/// <param name="top">The top line to start.</param>
		void PaintHex(System.Drawing.Graphics g, int top)
		{
			g.Clear(Color.White);
			string text = String.Empty;

			int offset = GetOffsetForLine(top);

			for (int i = 0; i < GetMaxVisibleLines(); i++) {
				string line = GetHex(buffer.GetBytes(offset, this.BytesPerLine));

				if (line == String.Empty) {
					text += new string(' ', this.BytesPerLine * 3) + "\n";
				} else if (line.Length < this.BytesPerLine * 3) {
					text += line + new string(' ', this.BytesPerLine * 3 - line.Length) + "\n";
				} else {
					text += line + "\n";
				}
				offset = GetOffsetForLine(top + i + 1);
			}

			TextRenderer.DrawText(g, text, this.Font, new Rectangle(0, 0, this.HexView.Width, this.HexView.Height), this.ForeColor, this.BackColor, TextFormatFlags.Left & TextFormatFlags.Top);
		}
		
		/// <summary>
		/// Draws the normal text view of the data.
		/// </summary>
		/// <param name="g">The graphics device to draw on.</param>
		/// <param name="top">The top line to start.</param>
		void PaintText(System.Drawing.Graphics g, int top)
		{
			g.Clear(Color.White);

			int offset = GetOffsetForLine(top);
			
			StringBuilder builder = new StringBuilder();

			for (int i = 0; i < GetMaxVisibleLines(); i++) {
				builder.AppendLine(GetText(buffer.GetBytes(offset, this.BytesPerLine)));
				offset = GetOffsetForLine(top + i + 1);
			}
			TextRenderer.DrawText(g, builder.ToString(), this.Font, new Point(0, 0), this.ForeColor, this.BackColor);
		}
		
		/// <summary>
		/// Draws a pointer to show the cursor in the opposite view panel.
		/// </summary>
		/// <param name="hexView">the graphics device for the hex view panel</param>
		/// <param name="textView">the graphics device for the text view panel</param>
		void PaintPointer(System.Drawing.Graphics hexView, System.Drawing.Graphics textView)
		{
			// Paint a rectangle as a pointer in the view without the focus ...
			if (selection.HasSomethingSelected) return;
			if (hexViewFocus) {
				Point pos = this.GetPositionForOffset(caret.Offset, 1);
				if (hexinputmode) pos = this.GetPositionForOffset(caret.Offset - 1, 1);
				Size size = new Size(underscorewidth, fontheight);
				Pen p = new Pen(Color.Black, 1f);
				p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
				textView.DrawRectangle(p, new Rectangle(pos, size));
			} else if (textViewFocus) {
				Point pos = this.GetPositionForOffset(caret.Offset, 3);
				pos.Offset(0, 1);
				Size size = new Size(underscorewidth * 2, fontheight);
				Pen p = new Pen(Color.Black, 1f);
				p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
				hexView.DrawRectangle(p, new Rectangle(pos, size));
			}
		}
		
		/// <summary>
		/// Recalculates the current selection regions for drawing.
		/// </summary>
		void CalculateSelectionRegions()
		{
			ArrayList al = new ArrayList();
			
			int lines = Math.Abs(GetLineForOffset(selection.End) - GetLineForOffset(selection.Start));
			int start, end;
			
			if (selection.End > selection.Start) {
				start = selection.Start;
				end = selection.End;
			} else {
				start = selection.End;
				end = selection.Start;
			}
			
			int start_dummy = start;
			
			if (start < GetOffsetForLine(topline)) {
				start = GetOffsetForLine(topline) - 2;
				start_dummy = GetOffsetForLine(topline - 2);
			}
			
			if (end > GetOffsetForLine(topline + GetMaxVisibleLines())) end = GetOffsetForLine(topline + GetMaxVisibleLines() + 1);
			
			if (hexViewFocus)
			{
				if (GetLineForOffset(end) == GetLineForOffset(start)) {
					Point pt = GetPositionForOffset(start, 3);
					al.Add(new Rectangle(new Point(pt.X - 4, pt.Y), new Size((end - start) * underscorewidth3 + 2, fontheight)));
				} else {
					// First Line
					Point pt = GetPositionForOffset(start, 3);
					al.Add(new Rectangle(new Point(pt.X - 4, pt.Y), new Size((this.BytesPerLine - (start - this.BytesPerLine * GetLineForOffset(start))) * underscorewidth3 + 2, fontheight)));
					
					// Lines between
					Point pt2 = GetPositionForOffset((1 + GetLineForOffset(start)) * this.BytesPerLine, 3);
					al.Add(new Rectangle(new Point(pt2.X - 4, pt2.Y), new Size(this.BytesPerLine * underscorewidth3 + 2, fontheight * (lines - 1) - lines + 1)));
					
					// Last Line
					Point pt3 = GetPositionForOffset(GetLineForOffset(end) * this.BytesPerLine, 3);
					al.Add(new Rectangle(new Point(pt3.X - 4, pt3.Y), new Size((end - GetLineForOffset(end) * this.BytesPerLine) * underscorewidth3 + 2, fontheight)));
				}
				
				this.selregion = (Rectangle[])al.ToArray(typeof (Rectangle));
				
				al.Clear();
				
				start = start_dummy;
				
				if (GetLineForOffset(end) == GetLineForOffset(start)) {
					Point pt = GetPositionForOffset(start, 1);
					al.Add(new Point(pt.X - 1, pt.Y));
					al.Add(new Point(pt.X - 1, pt.Y + fontheight));
					al.Add(new Point(pt.X - 1 + (end - start + 1) * underscorewidth - 8, pt.Y + fontheight));
					al.Add(new Point(pt.X - 1 + (end - start + 1) * underscorewidth - 8, pt.Y));
				} else {
					// First Line
					Point pt = GetPositionForOffset(start, 1);
					pt = new Point(pt.X - 1, pt.Y);
					al.Add(pt);
					pt = new Point(pt.X, pt.Y + fontheight - 1);
					al.Add(pt);

					// Second Line
					pt = GetPositionForOffset(GetOffsetForLine(GetLineForOffset(start) + 1), 1);
					pt = new Point(pt.X - 1, pt.Y);
					al.Add(pt);

					//last
					pt = GetPositionForOffset(GetOffsetForLine(GetLineForOffset(end)), 1);
					if ((end % this.BytesPerLine) != 0) {
						pt = new Point(pt.X - 1, pt.Y + fontheight);
					} else {
						pt = new Point(pt.X - 1, pt.Y + fontheight - 1);
					}
					al.Add(pt);
					
					if ((end % this.BytesPerLine) != 0) {
						//last
						pt = GetPositionForOffset(end, 1);
						pt = new Point(pt.X, pt.Y + fontheight);
						al.Add(pt);

						//last
						pt = GetPositionForOffset(end, 1);
						pt = new Point(pt.X, pt.Y);
						al.Add(pt);
						
					}

					//last
					pt = GetPositionForOffset(end + (this.BytesPerLine - (end % this.BytesPerLine)) - 1, 1);
					pt = new Point(pt.X + underscorewidth, pt.Y);
					al.Add(pt);

					//last
					pt = GetPositionForOffset(end + (this.BytesPerLine - (end % this.BytesPerLine)) - 1, 1);
					pt = new Point(pt.X + underscorewidth, GetPositionForOffset(start, 1).Y);
					al.Add(pt);
				}
				
				selpoints = (Point[])al.ToArray(typeof(Point));
			} else if (textViewFocus) {
				if (GetLineForOffset(end) == GetLineForOffset(start)) {
					Point pt = GetPositionForOffset(start, 1);
					al.Add(new Rectangle(new Point(pt.X - 4, pt.Y), new Size((end - start) * underscorewidth + 3, fontheight)));
				} else {
					// First Line
					Point pt = GetPositionForOffset(start, 1);
					al.Add(new Rectangle(new Point(pt.X - 4, pt.Y), new Size((this.BytesPerLine - (start - this.BytesPerLine * GetLineForOffset(start))) * underscorewidth + 3, fontheight)));
					
					// Lines between
					Point pt2 = GetPositionForOffset((1 + GetLineForOffset(start)) * this.BytesPerLine, 3);
					al.Add(new Rectangle(new Point(pt2.X - 4, pt2.Y), new Size(this.BytesPerLine * underscorewidth + 3, fontheight * (lines - 1) - lines + 1)));
					
					// Last Line
					Point pt3 = GetPositionForOffset(GetLineForOffset(end) * this.BytesPerLine, 1);
					al.Add(new Rectangle(new Point(pt3.X - 4, pt3.Y), new Size((end - GetLineForOffset(end) * this.BytesPerLine) * underscorewidth + 3, fontheight)));
				}
				
				selregion = (Rectangle[])al.ToArray(typeof(Rectangle));
				
				al.Clear();
				
				start = start_dummy;
				
				if (GetLineForOffset(end) == GetLineForOffset(start)) {
					Point pt = GetPositionForOffset(start, 3);
					al.Add(new Point(pt.X - 1, pt.Y));
					al.Add(new Point(pt.X - 1, pt.Y + fontheight));
					al.Add(new Point(pt.X - 1 + (end - start) * underscorewidth3 - 5, pt.Y + fontheight));
					al.Add(new Point(pt.X - 1 + (end - start) * underscorewidth3 - 5, pt.Y));
				} else {
					// First Line
					Point pt = GetPositionForOffset(start, 3);
					pt = new Point(pt.X - 1, pt.Y);
					al.Add(pt);
					pt = new Point(pt.X, pt.Y + fontheight - 1);
					al.Add(pt);

					// Second Line
					pt = GetPositionForOffset(GetOffsetForLine(GetLineForOffset(start) + 1), 3);
					pt = new Point(pt.X - 1, pt.Y);
					al.Add(pt);

					//last
					pt = GetPositionForOffset(GetOffsetForLine(GetLineForOffset(end)), 3);
					if ((end % this.BytesPerLine) != 0) {
						pt = new Point(pt.X - 1, pt.Y + fontheight);
					} else {
						pt = new Point(pt.X - 1, pt.Y + fontheight - 1);
					}
					al.Add(pt);
					
					if ((end % this.BytesPerLine) != 0) {

						//last
						pt = GetPositionForOffset(end, 3);
						pt = new Point(pt.X - 5, pt.Y + fontheight);
						al.Add(pt);

						//last
						pt = GetPositionForOffset(end, 3);
						pt = new Point(pt.X - 5, pt.Y);
						al.Add(pt);
					}

					//last
					pt = GetPositionForOffset(end + (this.BytesPerLine - (end % this.BytesPerLine)) - 1, 3);
					pt = new Point(pt.X - 5 + underscorewidth3, pt.Y);
					al.Add(pt);

					//last
					pt = GetPositionForOffset(end + (this.BytesPerLine - (end % this.BytesPerLine)) - 1, 3);
					pt = new Point(pt.X - 5 + underscorewidth3, GetPositionForOffset(start, 3).Y);
					al.Add(pt);
				}
				
				selpoints = (Point[])al.ToArray(typeof(Point));
			}
		}
		
		/// <summary>
		/// Draws the current selection
		/// </summary>
		/// <param name="hexView">The graphics device for the hex view panel</param>
		/// <param name="textView">The graphics device for the text view panel</param>
		/// <param name="paintMarker">If true the marker is painted, otherwise not.</param>
		void PaintSelection(Graphics hexView, Graphics textView, bool paintMarker)
		{
			if (!selection.HasSomethingSelected) return;
			
			int lines = Math.Abs(GetLineForOffset(selection.End) - GetLineForOffset(selection.Start)) + 1;
			int start, end;
			
			if (selection.End > selection.Start) {
				start = selection.Start;
				end = selection.End;
			} else {
				start = selection.End;
				end = selection.Start;
			}
			
			if (start > GetOffsetForLine(topline + GetMaxVisibleLines())) return;
			
			if (start < GetOffsetForLine(topline)) start = GetOffsetForLine(topline) - 2;
			if (end > GetOffsetForLine(topline + GetMaxVisibleLines())) end = GetOffsetForLine(topline + GetMaxVisibleLines() + 1);
			
			if (hexViewFocus) {
				StringBuilder builder = new StringBuilder();
				
				for (int i = GetLineForOffset(start) + 1; i < GetLineForOffset(end); i++) {
					builder.AppendLine(GetLineHex(i));
				}
				
				if (selregion.Length == 3) {
					TextRenderer.DrawText(hexView, GetHex(buffer.GetBytes(start, this.BytesPerLine)), this.Font, (Rectangle)selregion[0], Color.White, SystemColors.Highlight, TextFormatFlags.Left & TextFormatFlags.SingleLine);
					TextRenderer.DrawText(hexView, builder.ToString(), this.Font, (Rectangle)selregion[1], Color.White, SystemColors.Highlight, TextFormatFlags.Left);
					TextRenderer.DrawText(hexView, GetLineHex(GetLineForOffset(end)), this.Font, (Rectangle)selregion[2], Color.White, SystemColors.Highlight, TextFormatFlags.Left & TextFormatFlags.SingleLine);
				} else if (selregion.Length == 2) {
					TextRenderer.DrawText(hexView, GetHex(buffer.GetBytes(start, this.BytesPerLine)), this.Font, (Rectangle)selregion[0], Color.White, SystemColors.Highlight, TextFormatFlags.Left & TextFormatFlags.SingleLine);
					TextRenderer.DrawText(hexView, GetLineHex(GetLineForOffset(end)), this.Font, (Rectangle)selregion[1], Color.White, SystemColors.Highlight, TextFormatFlags.Left & TextFormatFlags.SingleLine);
				} else {
					TextRenderer.DrawText(hexView, GetHex(buffer.GetBytes(start, this.BytesPerLine)), this.Font, (Rectangle)selregion[0], Color.White, SystemColors.Highlight, TextFormatFlags.Left & TextFormatFlags.SingleLine);
				}
				
				if (!paintMarker) return;
				
				if ((selregion.Length > 1) && ((int)(Math.Abs(end - start)) <= this.BytesPerLine)) {
					if (selpoints.Length < 8) return;
					textView.DrawPolygon(Pens.Black, new Point[] {selpoints[0], selpoints[1], selpoints[6], selpoints[7]});
					textView.DrawPolygon(Pens.Black, new Point[] {selpoints[4], selpoints[5], selpoints[2], selpoints[3]});
				} else {
					textView.DrawPolygon(Pens.Black, selpoints);
				}
			} else if (textViewFocus) {
				StringBuilder builder = new StringBuilder();
				
				for (int i = GetLineForOffset(start) + 1; i < GetLineForOffset(end); i++) {
					builder.AppendLine(GetLineText(i));
				}
				
				if (selregion.Length == 3) {
					TextRenderer.DrawText(textView, GetText(buffer.GetBytes(start, this.BytesPerLine)), this.Font, (Rectangle)selregion[0], Color.White, SystemColors.Highlight, TextFormatFlags.Left & TextFormatFlags.SingleLine);
					TextRenderer.DrawText(textView, builder.ToString(), this.Font, (Rectangle)selregion[1], Color.White, SystemColors.Highlight, TextFormatFlags.Left);
					TextRenderer.DrawText(textView, GetLineText(GetLineForOffset(end)), this.Font, (Rectangle)selregion[2], Color.White, SystemColors.Highlight, TextFormatFlags.Left & TextFormatFlags.SingleLine);
				} else if (selregion.Length == 2) {
					TextRenderer.DrawText(textView, GetText(buffer.GetBytes(start, this.BytesPerLine)), this.Font, (Rectangle)selregion[0], Color.White, SystemColors.Highlight, TextFormatFlags.Left & TextFormatFlags.SingleLine);
					TextRenderer.DrawText(textView, GetLineText(GetLineForOffset(end)), this.Font, (Rectangle)selregion[1], Color.White, SystemColors.Highlight, TextFormatFlags.Left & TextFormatFlags.SingleLine);
				} else {
					TextRenderer.DrawText(textView, GetText(buffer.GetBytes(start, this.BytesPerLine)), this.Font, (Rectangle)selregion[0], Color.White, SystemColors.Highlight, TextFormatFlags.Left & TextFormatFlags.SingleLine);
				}
				
				if (!paintMarker) return;
				if ((selregion.Length > 1) && ((int)(Math.Abs(end - start)) <= this.BytesPerLine)) {
					hexView.DrawPolygon(Pens.Black, new Point[] {selpoints[0], selpoints[1], selpoints[6], selpoints[7]});
					hexView.DrawPolygon(Pens.Black, new Point[] {selpoints[4], selpoints[5], selpoints[2], selpoints[3]});
				} else {
					hexView.DrawPolygon(Pens.Black, selpoints);
				}
			}
		}
		#endregion
		
		#region Undo/Redo
		/*
		 * Undo/Redo handling for the buffer.
		 * */
		public void Redo()
		{
			EventArgs e2 = new EventArgs();
			OnDocumentChanged(e2);

			UndoStep step = undoStack.Redo(ref buffer);
			hexinputmode = false;
			hexinputmodepos = 0;
			selection.Clear();
			if (step != null) caret.SetToPosition(GetPositionForOffset(step.Start, this.charwidth));
			this.Invalidate();
		}
		
		public void Undo()
		{
			EventArgs e2 = new EventArgs();
			OnDocumentChanged(e2);

			UndoStep step = undoStack.Undo(ref buffer);
			hexinputmode = false;
			hexinputmodepos = 0;
			selection.Clear();
			if (step != null) {
				int offset = step.Start;
				if (offset > buffer.BufferSize) offset = buffer.BufferSize;
				caret.SetToPosition(GetPositionForOffset(offset, this.charwidth));
			}
			this.Invalidate();
		}
		
		public bool CanUndo {
			get { return undoStack.CanUndo; }
		}
		
		public bool CanRedo {
			get { return undoStack.CanRedo; }
		}
		#endregion
		
		#region Selection
		/*
		 * Selection handling
		 * */
		public void SetSelection(int start, int end)
		{
			if (start > buffer.BufferSize) start = buffer.BufferSize;
			if (start < 0) start = 0;
			selection.Start = start;
			if (end > buffer.BufferSize) end = buffer.BufferSize;
			selection.End = end;
			selection.HasSomethingSelected = true;
			hexinputmode = false;
			hexinputmodepos = 0;
			
			CalculateSelectionRegions();
			
			this.Invalidate();
		}
		
		public bool HasSelection {
			get { return selection.HasSomethingSelected; }
		}
		
		public void SelectAll()
		{
			SetSelection(0, this.buffer.BufferSize);
		}
		#endregion
		
		#region Clipboard Actions
		/*
		 * Clipboard handling
		 * */
		public string Copy()
		{
			return selection.SelectionText;
		}
		
		public void Paste(string text)
		{
			if (caret.Offset > buffer.BufferSize) caret.Offset = buffer.BufferSize;
			if (selection.HasSomethingSelected) {
				byte[] old = selection.GetSelectionBytes();
				int start = selection.Start;
				
				if (selection.Start > selection.End) start = selection.End;
				
				buffer.RemoveBytes(start, Math.Abs(selection.End - selection.Start));
				
				buffer.SetBytes(start, this.Encoding.GetBytes(text.ToCharArray()), false);
				UndoAction action = UndoAction.Overwrite;
				
				undoStack.AddUndoStep(new UndoStep(this.Encoding.GetBytes(text.ToCharArray()), old, caret.Offset, action));

				caret.Offset = start + ClipboardManager.Paste().Length;
				selection.Clear();
			} else {
				buffer.SetBytes(caret.Offset, this.Encoding.GetBytes(text.ToCharArray()), false);
				UndoAction action = UndoAction.Remove;
				
				undoStack.AddUndoStep(new UndoStep(this.Encoding.GetBytes(text.ToCharArray()), null, caret.Offset, action));

				caret.Offset += ClipboardManager.Paste().Length;
			}
			if (GetLineForOffset(caret.Offset) < this.topline) this.topline = GetLineForOffset(caret.Offset);
			if (GetLineForOffset(caret.Offset) > this.topline + this.GetMaxVisibleLines() - 2) this.topline = GetLineForOffset(caret.Offset) - this.GetMaxVisibleLines() + 2;
			if (this.topline < 0) this.topline = 0;
			if (this.topline > VScrollBar.Maximum) {
				AdjustScrollBar();
				if (this.topline > VScrollBar.Maximum) this.topline = VScrollBar.Maximum;
			}
			VScrollBar.Value = this.topline;
			this.Invalidate();
			
			EventArgs e2 = new EventArgs();
			OnDocumentChanged(e2);
		}
		
		public void Delete()
		{
			if (hexinputmode) return;
			if (selection.HasSomethingSelected) {
				byte[] old = selection.GetSelectionBytes();
				buffer.RemoveBytes(selection.Start, Math.Abs(selection.End - selection.Start));
				caret.Offset = selection.Start;
				
				UndoAction action = UndoAction.Add;
				
				undoStack.AddUndoStep(new UndoStep(old, null, selection.Start, action));
				
				selection.Clear();
			}
			this.Invalidate();
			caret.Show();
			
			EventArgs e2 = new EventArgs();
			OnDocumentChanged(e2);
		}
		#endregion
		
		/// <summary>
		/// Indicates either the hex or text view has the focus.
		/// </summary>
		public bool HasFocus {
			get { return this.hexViewFocus | this.textViewFocus; }
		}
		
		#region TextProcessing
		/// <summary>
		/// Generates a string out of a byte array. Unprintable chars are replaced by a ".".
		/// </summary>
		/// <param name="bytes">An array of bytes to convert to a string.</param>
		/// <returns>A string containing all bytes in the byte array.</returns>
		string GetText(byte[] bytes)
		{
			for (int i = 0; i < bytes.Length; i++) {
				if (bytes[i] < 32) bytes[i] = 46;
			}

			string text = this.Encoding.GetString(bytes);
			return text.Replace("&", "&&");
		}

		/// <summary>
		/// Gets the text from a line.
		/// </summary>
		/// <param name="line">The line number to get the text from.</param>
		/// <returns>A string, which contains the text on the given line.</returns>
		string GetLineText(int line)
		{
			return GetText(buffer.GetBytes(GetOffsetForLine(line), this.BytesPerLine));
		}
		
		/// <summary>
		/// Returns the text from a line in hex.
		/// </summary>
		/// <param name="line">The line number to get the text from.</param>
		/// <returns>A string, which contains the text on the given line in hex representation.</returns>
		string GetLineHex(int line)
		{
			return GetHex(buffer.GetBytes(GetOffsetForLine(line), this.BytesPerLine));
		}
		
		/// <summary>
		/// Converts a byte[] to its string representation.
		/// </summary>
		/// <param name="bytes">An array of bytes to convert</param>
		/// <returns>The string representation of the byte[]</returns>
		static string GetHex(byte[] bytes)
		{
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < bytes.Length; i++) {
				string num = string.Format("{0:X}", bytes[i]);
				if (num.Length < 2) {
					builder.Append("0" + num + " ");
				} else {
					builder.Append(num + " ");
				}
			}
			return builder.ToString();
		}
		#endregion
		
		/// <summary>
		/// Redraws the control after resizing.
		/// </summary>
		void HexEditSizeChanged(object sender, EventArgs e)
		{
			if (this.FitToWindowWidth) this.BytesPerLine = CalculateMaxBytesPerLine();

			this.Invalidate();
			SetViews();
		}
		
		/// <summary>
		/// Resets the current viewpanels to fit the new sizes and settings.
		/// </summary>
		void SetViews()
		{
			int sidetext = this.GetMaxLines() * this.BytesPerLine;
			int textwidth = MeasureStringWidth(this.TextView.CreateGraphics(), new string('_', this.BytesPerLine + 1), this.Font);
			int hexwidth = underscorewidth3 * this.BytesPerLine;
			int top = HexView.Top;
			this.HexView.Top = fontheight - 1;
			this.TextView.Top = fontheight - 1;
			this.Header.Top = 0;
			this.Header.Left = HexView.Left - 10;
			this.HexView.Height = this.Height - fontheight + top - 18;
			this.TextView.Height = this.Height - fontheight + top - 18;

			string st = String.Empty;

			switch (this.ViewMode) {
				case ViewMode.Hexadecimal:
					if (sidetext.ToString().Length < 8) {
						st = "  Offset";
					} else {
						st = "  " + string.Format("{0:X}", sidetext);
					}
					break;
				case ViewMode.Octal:
					if (sidetext.ToString().Length < 8) {
						st = "  Offset";
					} else {
						int tmp = sidetext;
						while (tmp != 0) {
							st = (tmp % 8).ToString() + st;
							tmp = (int)(tmp / 8);
						}
					}

					st = "  " + st;
					break;
				case ViewMode.Decimal:
					if (sidetext.ToString().Length < 8) {
						st = "  Offset";
					} else {
						st = "  " + sidetext.ToString();
					}
					break;
			}

			this.Side.Width = MeasureStringWidth(this.Side.CreateGraphics(), st, this.Font);
			this.Side.Left = 0;
			this.HexView.Left = this.Side.Width + 10;

			if ((textwidth + hexwidth + 25) > this.Width - this.Side.Width) {
				this.HexView.Width = this.Width - this.Side.Width - textwidth - 30;
				this.TextView.Width = textwidth;
				this.TextView.Left = this.Width - textwidth - 16;
			} else {
				this.HexView.Width = hexwidth;
				this.TextView.Width = textwidth;
				this.TextView.Left = hexwidth + this.HexView.Left + 20;
			}
			
			this.caret.SetToPosition(GetPositionForOffset(this.caret.Offset, this.charwidth));
			this.Header.Width = this.HexView.Width + 10;
			this.Header.Height = this.fontheight;
			AdjustScrollBar();
		}
		
		/// <summary>
		/// General handling of keyboard events, for non printable keys.
		/// </summary>
		void HexEditKeyDown(object sender, KeyEventArgs e)
		{
			int start = selection.Start;
			int end = selection.End;
			
			if (selection.Start > selection.End) {
				start = selection.End;
				end = selection.Start;
			}
			
			if (!hexViewFocus) {
				hexinputmode = false;
				hexinputmodepos = 0;
			}
			switch (e.KeyCode) {
				case Keys.Up:
				case Keys.Down:
				case Keys.Left:
				case Keys.Right:
					int oldoffset = caret.Offset;
					MoveCaret(e);
					if (GetLineForOffset(caret.Offset) < this.topline) this.topline = GetLineForOffset(caret.Offset);
					if (GetLineForOffset(caret.Offset) > this.topline + this.GetMaxVisibleLines() - 2) this.topline = GetLineForOffset(caret.Offset) - this.GetMaxVisibleLines() + 2;
					VScrollBar.Value = this.topline;
					if (e.Shift) {
						if (selection.HasSomethingSelected) {
							int offset = caret.Offset; // set offset to caretposition
							int oldstart = selection.Start; // copy old value
							
							// if shift wasn't pressed before set the start position
							if (!shiftwaspressed) { // to the current offset
								oldstart = caret.Offset;
							} else { // otherwise refresh the end of the selection.
								offset = caret.Offset;
							}
							
							this.SetSelection(oldstart, offset);
						} else {
							this.SetSelection(oldoffset, caret.Offset);
						}
						selection.HasSomethingSelected = true;
						selectionmode = true;
					} else {
						selection.Clear();
						selectionmode = false;
					}
					handled = true;
					break;
				case Keys.Insert:
					insertmode = !insertmode;
					if (!insertmode) {
						if (this.textViewFocus) caretwidth = underscorewidth;
						if (this.hexViewFocus) caretwidth = underscorewidth * 2;
					} else {
						caretwidth = 1;
					}

					Control currentinput;

					if (hexViewFocus & !textViewFocus) {
						currentinput = (Control)this.HexView;
					} else if (!hexViewFocus & textViewFocus) {
						currentinput = (Control)this.TextView;
					} else {
						return;
					}
					caret.Create(currentinput, caretwidth, fontheight);
					
					caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));
					caret.Show();
					handled = true;
					break;
				case Keys.Back:
					handled = true;
					if (hexinputmode) return;
					if (selection.HasSomethingSelected) {
						byte[] bytes = selection.GetSelectionBytes();
						
						buffer.RemoveBytes(start, Math.Abs(end - start));
						caret.Offset = start;
						
						UndoAction action = UndoAction.Add;
						
						undoStack.AddUndoStep(new UndoStep(bytes, null, start, action));
						
						selection.Clear();
					} else {
						byte b = buffer.GetByte(caret.Offset - 1);
						
						if (buffer.RemoveByte(caret.Offset - 1))
						{
							if (caret.Offset > -1) caret.Offset--;
							if (GetLineForOffset(caret.Offset) < this.topline) this.topline = GetLineForOffset(caret.Offset);
							if (GetLineForOffset(caret.Offset) > this.topline + this.GetMaxVisibleLines() - 2) this.topline = GetLineForOffset(caret.Offset) - this.GetMaxVisibleLines() + 2;
							
							UndoAction action = UndoAction.Add;
							
							undoStack.AddUndoStep(new UndoStep(new byte[] {b}, null, caret.Offset, action));
						}
					}
					
					EventArgs e2 = new EventArgs();
					OnDocumentChanged(e2);
					break;
				case Keys.Delete:
					handled = true;
					if (hexinputmode) return;
					if (selection.HasSomethingSelected) {
						byte[] old = selection.GetSelectionBytes();
						buffer.RemoveBytes(start, Math.Abs(selection.End - selection.Start));
						caret.Offset = selection.Start;
						
						UndoAction action = UndoAction.Add;
						
						undoStack.AddUndoStep(new UndoStep(old, null, selection.Start, action));
						
						selection.Clear();
					} else {
						byte b = buffer.GetByte(caret.Offset);
						
						buffer.RemoveByte(caret.Offset);
						
						UndoAction action = UndoAction.Remove;
						
						undoStack.AddUndoStep(new UndoStep(new byte[] {b}, null, caret.Offset, action));
						
						if (GetLineForOffset(caret.Offset) < this.topline) this.topline = GetLineForOffset(caret.Offset);
						if (GetLineForOffset(caret.Offset) > this.topline + this.GetMaxVisibleLines() - 2) this.topline = GetLineForOffset(caret.Offset) - this.GetMaxVisibleLines() + 2;
					}
					caret.Show();
					
					e2 = new EventArgs();
					OnDocumentChanged(e2);
					break;
				case Keys.CapsLock:
				case Keys.ShiftKey:
				case Keys.ControlKey:
					break;
				default:
					byte asc = (byte)e.KeyValue;
					
					if (e.Control) {
						handled = true;
						switch (asc) {
								// Ctrl-A is pressed -> select all
							case 65 :
								this.SetSelection(0, buffer.BufferSize);
								break;
								// Ctrl-C is pressed -> copy text to ClipboardManager
							case 67 :
								ClipboardManager.Copy(selection.SelectionText);
								break;
								// Ctrl-V is pressed -> paste from ClipboardManager
							case 86 :
								if (ClipboardManager.ContainsText) {
									if (caret.Offset > buffer.BufferSize) caret.Offset = buffer.BufferSize;
									if (selection.HasSomethingSelected) {
										byte[] old = selection.GetSelectionBytes();
										buffer.RemoveBytes(selection.Start, Math.Abs(selection.End - selection.Start));
										
										buffer.SetBytes(selection.Start, this.Encoding.GetBytes(ClipboardManager.Paste().ToCharArray()), false);
										UndoAction action = UndoAction.Overwrite;
										
										undoStack.AddUndoStep(new UndoStep(this.Encoding.GetBytes(ClipboardManager.Paste().ToCharArray()), old, caret.Offset, action));

										caret.Offset = selection.Start + ClipboardManager.Paste().Length;
										selection.Clear();
									} else {
										buffer.SetBytes(caret.Offset, this.Encoding.GetBytes(ClipboardManager.Paste().ToCharArray()), false);
										UndoAction action = UndoAction.Remove;
										
										undoStack.AddUndoStep(new UndoStep(this.Encoding.GetBytes(ClipboardManager.Paste().ToCharArray()), null, caret.Offset, action));

										caret.Offset += ClipboardManager.Paste().Length;
									}
									if (GetLineForOffset(caret.Offset) < this.topline) this.topline = GetLineForOffset(caret.Offset);
									if (GetLineForOffset(caret.Offset) > this.topline + this.GetMaxVisibleLines() - 2) this.topline = GetLineForOffset(caret.Offset) - this.GetMaxVisibleLines() + 2;
									if (this.topline < 0) this.topline = 0;
									if (this.topline > VScrollBar.Maximum) {
										AdjustScrollBar();
										if (this.topline > VScrollBar.Maximum) this.topline = VScrollBar.Maximum;
									}
									VScrollBar.Value = this.topline;
									
									e2 = new EventArgs();
									OnDocumentChanged(e2);

								}
								break;
								// Ctrl-X is pressed -> cut from document
							case 88 :
								if (selection.HasSomethingSelected) {
									ClipboardManager.Copy(selection.SelectionText);
									if (selection.End < selection.Start)
									{
										int help = selection.End;
										selection.End = selection.Start;
										selection.Start = help;
									}
									UndoAction action = UndoAction.Add;
									
									undoStack.AddUndoStep(new UndoStep(selection.GetSelectionBytes(), null, caret.Offset, action));

									buffer.RemoveBytes(selection.Start, selection.SelectionText.Length);
									selection.Clear();
									
									e2 = new EventArgs();
									OnDocumentChanged(e2);
								}
								break;
						}
						break;
					}
					if (hexViewFocus) {
						ProcessHexInput(e);
						handled = true;
						return;
					}

					break;
			}
			shiftwaspressed = e.Shift;
			if (handled) {
				this.Invalidate();
				caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));
				caret.Show();
			}
		}
		
		/// <summary>
		/// Handling of printable keys.
		/// </summary>
		void HexEditKeyPress(object sender, KeyPressEventArgs e)
		{
			if (handled) {
				handled = false;
			} else {
				byte[] old = buffer.GetBytes(caret.Offset, 1);
				try  {
					if (selection.HasSomethingSelected) {
						Delete();
						buffer.SetByte(caret.Offset, (byte)e.KeyChar, !insertmode);
					} else {
						buffer.SetByte(caret.Offset, (byte)e.KeyChar, !insertmode);
					}
				} catch (System.ArgumentOutOfRangeException) {}
				caret.Offset++;
				if (GetLineForOffset(caret.Offset) < this.topline) this.topline = GetLineForOffset(caret.Offset);
				if (GetLineForOffset(caret.Offset) > this.topline + this.GetMaxVisibleLines() - 2) this.topline = GetLineForOffset(caret.Offset) - this.GetMaxVisibleLines() + 2;
				VScrollBar.Value = this.topline;
				
				UndoAction action;
				
				if (insertmode) {
					action = UndoAction.Remove;
					old = null;
				} else {
					action = UndoAction.Overwrite;
				}
				
				undoStack.AddUndoStep(new UndoStep(new byte[] {(byte)e.KeyChar}, old, caret.Offset - 1, action));
			}
			caret.SetToPosition(GetPositionForOffset(caret.Offset, charwidth));
			
			EventArgs e2 = new EventArgs();
			OnDocumentChanged(e2);
			
			this.Invalidate();
		}
		
		/// <summary>
		/// Sets the caret according to the input.
		/// </summary>
		/// <param name="input">Keyboard input</param>
		void MoveCaret(KeyEventArgs input)
		{
			if (!input.Control) {
				hexinputmode = false;
				hexinputmodepos = 0;
			}
			switch (input.KeyCode) {
				case Keys.Up:
					if (caret.Offset >= this.BytesPerLine) {
						caret.Offset -= this.BytesPerLine;
						caret.SetToPosition(this.GetPositionForOffset(caret.Offset, this.charwidth));
					}
					break;
				case Keys.Down:
					if (caret.Offset <= this.Buffer.BufferSize - this.BytesPerLine) {
						caret.Offset += this.BytesPerLine;
						caret.SetToPosition(this.GetPositionForOffset(caret.Offset, this.charwidth));
					} else {
						caret.Offset = this.Buffer.BufferSize;
						caret.SetToPosition(this.GetPositionForOffset(caret.Offset, this.charwidth));
					}
					break;
				case Keys.Left:
					if (caret.Offset >= 1) {
						if (hexViewFocus) {
							if (input.Control) {
								hexinputmode = false;
								if (hexinputmodepos == 0) {
									caret.Offset--;
									hexinputmodepos = 1;
									hexinputmode = true;
								} else {
									hexinputmodepos--;
								}
							} else {
								caret.Offset--;
							}
						} else {
							caret.Offset--;
						}
						caret.SetToPosition(this.GetPositionForOffset(caret.Offset, this.charwidth));
					}
					break;
				case Keys.Right:
					if (caret.Offset <= this.Buffer.BufferSize - 1) {
						if (hexViewFocus) {
							if (input.Control) {
								hexinputmode = true;
								if (hexinputmodepos == 1) {
									caret.Offset++;
									hexinputmodepos = 0;
									hexinputmode = false;
								} else {
									hexinputmodepos++;
								}
							} else {
								caret.Offset++;
							}
						} else {
							caret.Offset++;
						}

						caret.SetToPosition(this.GetPositionForOffset(caret.Offset, this.charwidth));
					}
					break;
			}

		}
		
		/// <summary>
		/// Processes only 0-9 and A-F keys and handles the special hex-inputmode.
		/// </summary>
		/// <param name="input">Keyboard input</param>
		void ProcessHexInput(KeyEventArgs input)
		{
			int start = selection.Start;
			int end = selection.End;
			
			if (selection.Start > selection.End) {
				start = selection.End;
				end = selection.Start;
			}
			
			if (((input.KeyValue > 47) & (input.KeyValue < 58)) | ((input.KeyValue > 64) & (input.KeyValue < 71))) {
				hexinputmode = true;
				if (insertmode) {
					byte[] old;
					if (selection.HasSomethingSelected) {
						old = selection.GetSelectionBytes();
						
						buffer.RemoveBytes(start, Math.Abs(end - start));
					} else {
						old = null;
					}
					string @in = "";
					if (hexinputmodepos == 1) {
						@in = string.Format("{0:X}", buffer.GetByte(caret.Offset));
						
						// if @in is like 4 or A then make 04 or 0A out of it.
						if (@in.Length == 1) @in = "0" + @in;
						
						UndoAction action = UndoAction.Overwrite;
						
						undoStack.AddUndoStep(new UndoStep(new byte[] {(byte)(Convert.ToInt32(@in.Remove(1) + ((char)(input.KeyValue)).ToString(), 16))}, buffer.GetBytes(caret.Offset, 1), caret.Offset, action));
						
						@in = @in.Remove(1) + ((char)(input.KeyValue)).ToString();
						hexinputmodepos = 0;
						hexinputmode = false;
						
						buffer.SetByte(caret.Offset, (byte)(Convert.ToInt32(@in, 16)), true);
						caret.Offset++;
						
						caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));
					} else if (hexinputmodepos == 0) {
						UndoAction action;
						
						if (selection.HasSomethingSelected) {
							action = UndoAction.Overwrite;
							caret.Offset = start;
							selection.Clear();
						} else {
							action = UndoAction.Remove;
						}
						@in = (char)(input.KeyValue) + "0";
						if (caret.Offset > buffer.BufferSize) caret.Offset = buffer.BufferSize;
						buffer.SetByte(caret.Offset, (byte)(Convert.ToInt32(@in, 16)), false);
						hexinputmodepos = 1;
						
						undoStack.AddUndoStep(new UndoStep(new byte[] {(byte)(Convert.ToInt32(@in, 16))}, old, caret.Offset, action));

						caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));
					}

					caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));
				} else {
					UndoAction action;
					
					string @in = "";
					if (hexinputmodepos == 1) {
						byte[] _old = buffer.GetBytes(caret.Offset, 1);
						@in = string.Format("{0:X}", buffer.GetByte(caret.Offset));
						@in = @in.Remove(1) + ((char)(input.KeyValue)).ToString();
						hexinputmodepos = 0;
						hexinputmode = false;
						buffer.SetByte(caret.Offset, (byte)(Convert.ToInt32(@in, 16)), true);
						caret.Offset++;
						
						if (insertmode) {
							action = UndoAction.Add;
							_old = null;
						} else {
							action = UndoAction.Overwrite;
						}
						
						undoStack.AddUndoStep(new UndoStep(new byte[] {(byte)(Convert.ToInt32(@in, 16))}, _old, caret.Offset - 1, action));

						caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));

					} else if (hexinputmodepos == 0) {
						byte[] _old = buffer.GetBytes(caret.Offset, 1);
						@in = (char)(input.KeyValue) + "0";
						buffer.SetByte(caret.Offset, (byte)(Convert.ToInt32(@in, 16)), true);
						hexinputmodepos = 1;
						
						if (insertmode) {
							action = UndoAction.Add;
							_old = null;
						} else {
							action = UndoAction.Overwrite;
						}
						
						undoStack.AddUndoStep(new UndoStep(new byte[] {(byte)(Convert.ToInt32(@in, 16))}, _old, caret.Offset, action));
					}
					
					caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));
				}
				
				EventArgs e = new EventArgs();
				OnDocumentChanged(e);
				
				this.Invalidate();
			}
		}
		
		#region file functions
		/// <summary>
		/// Loads a file into the editor.
		/// </summary>
		/// <param name="file">The file-info to open.</param>
		/// <param name="stream">The stream to read from/write to.</param>
		public void LoadFile(OpenedFile file, Stream stream)
		{
			buffer.Load(file, stream);
			if (this.progressBar != null) {
				this.progressBar.Visible = false;
				this.progressBar.Available = false;
				this.progressBar.Value = 0;
			}
			//this.Cursor = Cursors.WaitCursor;
		}
		
		/// <summary>
		/// Called from the BufferManager when Loading is finished.
		/// </summary>
		/// <remarks>Currently not directly needed, because there's no thread in use to load the data.</remarks>
		internal void LoadingFinished()
		{
			if (this.InvokeRequired) {
				this.Invoke (new MethodInvoker (LoadingFinished));
				return;
			}
			this.FileName = fileName;
			selection.Clear();
			if (this.progressBar != null) {
				this.progressBar.Visible = false;
				this.progressBar.Available = false;
				this.progressBar.Value = 0;
			}
			
			this.Side.Cursor = this.Cursor = this.Header.Cursor = Cursors.Default;
			
			GC.Collect();
			this.Invalidate();
		}
		
		/// <summary>
		/// Saves the current buffer to a stream.
		/// </summary>
		public void SaveFile(OpenedFile file, Stream stream)
		{
			buffer.Save(file, stream);
		}
		#endregion
		
		/// <summary>
		/// Invalidates the control when the focus returns to it.
		/// </summary>
		private void HexEditGotFocus(object sender, EventArgs e)
		{
			//LoadSettings();
			this.Invalidate();
		}
		
		#region selection events
		/**
		 * Methods to control the current selection
		 * with mouse for both hex and text view.
		 * */
		
		void TextViewMouseDown(object sender, MouseEventArgs e)
		{
			this.textViewFocus = true;
			this.hexViewFocus = false;
			this.hexinputmode = false;
			this.hexinputmodepos = 0;
			if (e.Button == MouseButtons.Left) {
				if (selection.HasSomethingSelected) {
					selection.Start = 0;
					selection.End = 0;
					PaintText(this.TextView.CreateGraphics(), this.topline);
				}
				selectionmode = true;
				selection.Start = GetOffsetForPosition(e.Location, 1);
			}
			
			caret.Offset = GetOffsetForPosition(e.Location, 1);
			caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));
			caret.Show();
		}
		
		void TextViewMouseMove(object sender, MouseEventArgs e)
		{
			if ((e.Button == MouseButtons.Left) & selectionmode) {
				int end = selection.End;
				selection.End = GetOffsetForPosition(e.Location, 1);
				textViewFocus = true;
				hexViewFocus = false;
				moved = true;
				selection.HasSomethingSelected = true;
				if (end != selection.End) this.Invalidate();
				
				caret.Offset = GetOffsetForPosition(e.Location, 1);
				caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));
				caret.Show();
			}
		}
		
		void TextViewMouseUp(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right) return;
			if (selectionmode) {
				selection.HasSomethingSelected = true;
				if ((selection.End == selection.Start) | ((selection.Start == 0) & (selection.End == 0))) {
					selection.HasSomethingSelected = false;
					selectionmode = false;
				}
				this.Invalidate();
			} else {
				if (!moved) {
					selection.HasSomethingSelected = false;
					selection.Start = 0;
					selection.End = 0;
				}
				moved = false;
			}
			caret.Offset = GetOffsetForPosition(e.Location, 1);
			caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));
			caret.Show();
			
			selectionmode = false;
		}
		
		void HexViewMouseDown(object sender, MouseEventArgs e)
		{
			this.textViewFocus = false;
			this.hexViewFocus = true;
			this.hexinputmode = false;
			this.hexinputmodepos = 0;
			if (e.Button == MouseButtons.Left) {
				selectionmode = true;
				selection.Start = GetOffsetForPosition(e.Location, 3);
				selection.End = GetOffsetForPosition(e.Location, 3);
				this.Invalidate();
				
				caret.Offset = GetOffsetForPosition(e.Location, 3);
				caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));
				caret.Show();
			}
		}
		
		void HexViewMouseMove(object sender, MouseEventArgs e)
		{
			if ((e.Button == MouseButtons.Left) & selectionmode) {
				int end = selection.End;
				selection.End = GetOffsetForPosition(e.Location, 3);
				selection.HasSomethingSelected = true;
				textViewFocus = false;
				hexViewFocus = true;
				moved = true;
				caret.SetToPosition(GetPositionForOffset(GetOffsetForPosition(e.Location, 3), 3));
				if (end != selection.End) this.Invalidate();
				
				caret.Offset = GetOffsetForPosition(e.Location, 3);
				caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));
				caret.Show();
			}
		}
		
		void HexViewMouseUp(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right) return;

			if (selectionmode) {
				selection.HasSomethingSelected = true;
				if ((selection.End == selection.Start) || ((selection.Start == 0) && (selection.End == 0))) {
					selection.HasSomethingSelected = false;
					selectionmode = false;
				}
				this.Invalidate();
			} else {
				if (!moved) {
					selection.HasSomethingSelected = false;
					selection.End = 0;
					selection.Start = 0;
				}
				moved = false;
			}
			caret.Offset = GetOffsetForPosition(e.Location, 3);
			caret.SetToPosition(GetPositionForOffset(caret.Offset, this.charwidth));
			caret.Show();
			selectionmode = false;
		}
		#endregion
		
		/// <summary>
		/// Enables the control processing key commands.
		/// </summary>
		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			switch (keyData) {
				case Keys.Shift | Keys.Up :
				case Keys.Shift | Keys.Down :
				case Keys.Shift | Keys.Left :
				case Keys.Shift | Keys.Right :
					HexEditKeyDown(null, new KeyEventArgs(keyData));
					return true;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}
		
		/// <summary>
		/// Calculates the max possible bytes per line.
		/// </summary>
		/// <returns>Int32, containing the result</returns>
		int CalculateMaxBytesPerLine()
		{
			int width = this.Width - this.Side.Width - 90;
			int textwidth = 0, hexwidth = 0;
			int count = 0;
			// while the width of the textview + the width of the hexview is
			// smaller than the width of the whole control.
			while ((textwidth + hexwidth) < width) {
				// update counter and recalculate the sizes
				count++;
				textwidth = underscorewidth * count;
				hexwidth = underscorewidth3 * count;
			}

			return count;
		}
		
		/// <summary>
		/// Calculates the offset for a position.
		/// </summary>
		/// <param name="position">The position</param>
		/// <param name="charwidth">the width of one char, for example in the
		/// hexview the width is 3 because one char needs 2 chars to be
		/// displayed ("A" in text = "41 " in hex)</param>
		/// <returns>the offset for the position</returns>
		int GetOffsetForPosition(Point position, int charwidth)
		{
			// calculate the line: vertical position (Y) divided by the height of
			// one line (height of font = fontheight) = physical line + topline = virtual line.
			int line = (int)Math.Truncate((double)((float)position.Y / (float)fontheight)) + topline;
			//Debug.Print(line.ToString() + " " + ((double)((float)position.Y / (float)fontheight)).ToString());
			if (selection.HasSomethingSelected)	line++;
			// calculate the char: horizontal position (X) divided by the width of one char
			int ch = (int)Math.Truncate((double)(position.X / (charwidth * underscorewidth)));
			if (ch > this.BytesPerLine) ch = this.BytesPerLine;
			if (ch < 0) ch = 0;
			// calculate offset
			int offset = line * this.BytesPerLine + ch;
			
			// check
			if (offset < 0) return 0;
			if (offset < this.buffer.BufferSize) {
				return offset;
			} else {
				return this.buffer.BufferSize;
			}
		}
		
		/// <summary>
		/// Does the same as GetOffsetForPosition, but the other way round.
		/// </summary>
		/// <param name="offset">The offset to search</param>
		/// <param name="charwidth">the current width of one char. (Depends on the viewpanel we are using (3 in hex 1 in text view)</param>
		/// <returns>The Drawing.Point at which the offset is currently.</returns>
		Point GetPositionForOffset(int offset, int charwidth)
		{
			int line = (int)(offset / this.BytesPerLine) - this.topline;
			int pline = line * fontheight - 1 * (line - 1) - 1;
			int col = (offset % this.BytesPerLine) *
				MeasureStringWidth(this.CreateGraphics(),
				                   new string('_', charwidth), this.Font) + 4;
			if (hexinputmode && !selectionmode && !selection.HasSomethingSelected && this.insertmode) col += (hexinputmodepos * underscorewidth);
			return new Point(col, pline);
		}
		
		/// <summary>
		/// Returns the starting offset of a line.
		/// </summary>
		/// <param name="line">The line in the file, countings starts at 0.</param>
		/// <returns>The starting offset for a line.</returns>
		int GetOffsetForLine(int line)
		{
			return line * this.BytesPerLine;
		}
		
		/// <summary>
		/// Calculates the line on which the given offset is.
		/// </summary>
		/// <param name="offset">The offset to look up the line for.</param>
		/// <returns>The line on which the given offset is.</returns>
		/// <remarks>returns 0 for first line ...</remarks>
		int GetLineForOffset(int offset)
		{
			int line = (int)Math.Round((double)(offset / this.BytesPerLine));
			if ((offset != 0) & ((offset % this.BytesPerLine) == 0)) line--;
			return line;
		}
		
		/// <summary>
		/// Calculates the count of visible lines.
		/// </summary>
		/// <returns>The count of currently visible virtual lines.</returns>
		int GetMaxVisibleLines()
		{
			return (int)(this.HexView.Height / fontheight) + 3;
		}
		
		/// <summary>
		/// Calculates the count of all virtual lines in the buffer.
		/// </summary>
		/// <returns>Retrns 1 if the buffer is empty, otherwise the count of all virtual lines in the buffer.</returns>
		int GetMaxLines()
		{
			if (buffer == null) return 1;
			int lines = (int)(buffer.BufferSize / this.BytesPerLine);
			if ((buffer.BufferSize % this.BytesPerLine) != 0) lines++;
			return lines;
		}
		
		/// <summary>
		/// Calculates the count of digits of a given number.
		/// </summary>
		/// <param name="number">The number to calculate</param>
		/// <returns>the count of digits in the number</returns>
		static int GetLength(int number)
		{
			int count = 1;
			while (number > 9) {
				number = number / 10;
				count++;
			}
			return count;
		}
		
		/// <summary>
		/// Handles the context menu
		/// </summary>
		void HexEditControlContextMenuStripChanged(object sender, EventArgs e)
		{
			this.ContextMenuStrip.Closed += new ToolStripDropDownClosedEventHandler(ContextMenuStripClosed);
		}
		
		/// <summary>
		/// Invalidates the control after the context menu is closed.
		/// </summary>
		void ContextMenuStripClosed(object sender, EventArgs e)
		{
			this.Invalidate();
		}
	}
}