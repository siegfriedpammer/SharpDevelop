// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <author name="Daniel Grunwald"/>
//     <version>$Revision$</version>
// </file>

using ICSharpCode.TextEditor.Gui.CompletionWindow;
using System;
using System.Linq;
using System.Diagnostics;
using ICSharpCode.SharpDevelop.DefaultEditor.Gui.Editor;
using ICSharpCode.SharpDevelop.Refactoring;
using ICSharpCode.TextEditor;
using ICSharpCode.TextEditor.Document;

namespace ICSharpCode.SharpDevelop
{
	public class TextEditorAdapter : ITextEditor
	{
		readonly SharpDevelopTextAreaControl sdtac;
		readonly TextEditorControl editor;
		
		public TextEditorAdapter(TextEditorControl editor)
		{
			if (editor == null)
				throw new ArgumentNullException("editor");
			this.editor = editor;
			this.sdtac = editor as SharpDevelopTextAreaControl;
			this.Document = new TextEditorDocument(editor.Document);
			this.Caret = new CaretAdapter(this, editor.ActiveTextAreaControl.Caret);
		}
		
		sealed class CaretAdapter : ITextEditorCaret
		{
			readonly TextEditorAdapter parent;
			readonly Caret caret;
			
			public CaretAdapter(TextEditorAdapter parent, Caret caret)
			{
				Debug.Assert(parent != null && caret != null);
				
				this.parent = parent;
				this.caret = caret;
			}
			
			public int Offset {
				get { return caret.Offset; }
				set { caret.Position = parent.editor.Document.OffsetToPosition(value); }
			}
			
			public int Line {
				get { return caret.Line + 1; }
				set { caret.Line = value - 1; }
			}
			
			public int Column {
				get { return caret.Column + 1; }
				set { caret.Column = value - 1; }
			}
			
			public ICSharpCode.NRefactory.Location Position {
				get {
					return ToLocation(caret.Position);
				}
				set {
					caret.Position = ToPosition(value);
				}
			}
		}
		
		static ICSharpCode.NRefactory.Location ToLocation(TextLocation position)
		{
			return new ICSharpCode.NRefactory.Location(position.Column + 1, position.Line + 1);
		}
		
		static TextLocation ToPosition(ICSharpCode.NRefactory.Location location)
		{
			return new TextLocation(location.Column - 1, location.Line - 1);
		}
		
		public TextAreaControl ActiveTextAreaControl {
			get {
				return editor.ActiveTextAreaControl;
			}
		}
		
		public ICSharpCode.SharpDevelop.Dom.Refactoring.IDocument Document { get; private set; }
		public ITextEditorCaret Caret { get; private set; }
		
		public string FileName {
			get { return editor.FileName; }
		}
		
		public void ShowInsightWindow(ICSharpCode.TextEditor.Gui.InsightWindow.IInsightDataProvider provider)
		{
			if (sdtac != null)
				sdtac.ShowInsightWindow(provider);
		}
		
		public void ShowCompletionWindow(ICompletionDataProvider provider, char ch)
		{
			if (sdtac != null) {
				sdtac.ShowCompletionWindow(provider, ch);
			}
		}
		
		public void ShowCompletionWindow(ICompletionItemList items)
		{
			if (sdtac != null) {
				sdtac.ShowCompletionWindow(new CompletionItemListAdapter(items), '.');
			}
		}
		
		public string GetWordBeforeCaret()
		{
			if (sdtac != null)
				return sdtac.GetWordBeforeCaret();
			else
				return "";
		}
		
		public object GetService(Type serviceType)
		{
			if (serviceType == typeof(TextArea))
				return sdtac.ActiveTextAreaControl.TextArea;
			else
				return null;
		}
		
		public int SelectionStart {
			get {
				var sel = sdtac.ActiveTextAreaControl.SelectionManager;
				if (sel.HasSomethingSelected)
					return sel.SelectionCollection[0].Offset;
				else
					return this.Caret.Offset;
			}
		}
		
		public int SelectionLength {
			get {
				var sel = sdtac.ActiveTextAreaControl.SelectionManager;
				if (sel.HasSomethingSelected)
					return sel.SelectionCollection[0].Length;
				else
					return 0;
			}
		}
		
		public void Select(int selectionStart, int selectionLength)
		{
			var doc = sdtac.Document;
			sdtac.ActiveTextAreaControl.SelectionManager.SetSelection(
				doc.OffsetToPosition(selectionStart), doc.OffsetToPosition(selectionStart + selectionLength));
		}
		
		public void JumpTo(int line, int column)
		{
			sdtac.ActiveTextAreaControl.JumpTo(line - 1, column - 1);
		}
	}
	
	sealed class CompletionItemListAdapter : ICompletionDataProvider
	{
		readonly ICompletionItemList list;
		
		public CompletionItemListAdapter(ICompletionItemList list)
		{
			if (list == null)
				throw new ArgumentNullException("list");
			this.list = list;
		}
		
		public System.Windows.Forms.ImageList ImageList {
			get {
				return ClassBrowserIconService.ImageList;
			}
		}
		
		public string PreSelection {
			get {
				return null;
			}
		}
		
		public int DefaultIndex {
			get {
				return 0;
			}
		}
		
		public CompletionDataProviderKeyResult ProcessKey(char key)
		{
			return CompletionDataProviderKeyResult.NormalKey;
		}
		
		public bool InsertAction(ICompletionData data, TextArea textArea, int insertionOffset, char key)
		{
			return false;
		}
		
		public ICompletionData[] GenerateCompletionData(string fileName, TextArea textArea, char charTyped)
		{
			return list.Items.Select(item => new CompletionItemAdapter(item)).ToArray();
		}
	}
	
	sealed class CompletionItemAdapter : ICompletionData
	{
		readonly ICompletionItem item;
		
		public CompletionItemAdapter(ICompletionItem item)
		{
			if (item == null)
				throw new ArgumentNullException("item");
			this.item = item;
		}
		
		public int ImageIndex {
			get {
				return -1;
			}
		}
		
		public string Text {
			get { return item.Text; }
			set {
				throw new NotSupportedException();
			}
		}
		
		public string Description {
			get {
				return item.Description;
			}
		}
		
		public double Priority {
			get {
				return 0;
			}
		}
		
		public bool InsertAction(TextArea textArea, char ch)
		{
			return false;
		}
	}
}