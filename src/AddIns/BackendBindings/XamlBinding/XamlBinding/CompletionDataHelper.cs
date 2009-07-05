// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Siegfried Pammer" email="sie_pam@gmx.at"/>
//     <version>$Revision$</version>
// </file>

using ICSharpCode.AvalonEdit.Document;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Editor.CodeCompletion;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.XmlEditor;
using LoggingService = ICSharpCode.Core.LoggingService;

namespace ICSharpCode.XamlBinding
{
	public static class CompletionDataHelper
	{
		#region Pre-defined lists
		static readonly List<ICompletionItem> standardElements = new List<ICompletionItem> {
			new DefaultCompletionItem("!--"),
			new DefaultCompletionItem("![CDATA["),
			new DefaultCompletionItem("?")
		};
		
		static readonly List<ICompletionItem> standardAttributes = new List<ICompletionItem> {
			new DefaultCompletionItem("xmlns:")
		};
		
		static readonly List<string> xamlNamespaceAttributes = new List<string> {
			"Class", "ClassModifier", "FieldModifier", "Name", "Subclass", "TypeArguments", "Uid", "Key"
		};
		#endregion
		
		public const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
		
		public static XamlContext ResolveContext(string text, string fileName, int line, int col)
		{
			DebugTimer.Start();

			int offset = Utils.GetOffsetFromFilePos(text, line, col);
			
			if (offset == -1)
				throw new InvalidOperationException("No valid file position: " + line + " " + col);
			
			ParseInformation info = ParserService.GetParseInformation(fileName);
			string attribute = XmlParser.GetAttributeNameAtIndex(text, offset);
			bool inAttributeValue = XmlParser.IsInsideAttributeValue(text, offset);
			string attributeValue = XmlParser.GetAttributeValueAtIndex(text, offset);
			int offsetFromValueStart = Utils.GetOffsetFromValueStart(text, offset);
			
			AttributeValue value = MarkupExtensionParser.ParseValue(attributeValue);
			XamlContextDescription description = XamlContextDescription.None;
			
			Dictionary<string, string> xmlnsDefs;
			QualifiedName active;
			bool isParent;
			int elementStartIndex;
			
			Utils.LookUpInfoAtTarget(text, line, col, offset, out xmlnsDefs, out active, out isParent, out elementStartIndex);
			
			string wordBeforeIndex = text.GetWordBeforeOffset(offset);
			
			if (active != null && !isParent)
				description = XamlContextDescription.AtTag;
			
			if (elementStartIndex > -1 &&
			    (char.IsWhiteSpace(text[offset]) || !string.IsNullOrEmpty(attribute) ||
			     Extensions.Is(text[offset], '"', '\'') || !wordBeforeIndex.StartsWith("<")))
				description = XamlContextDescription.InTag;

			if (inAttributeValue) {
				description = XamlContextDescription.InAttributeValue;

				if (value != null && !value.IsString)
					description = XamlContextDescription.InMarkupExtension;

				if (attributeValue.StartsWith("{}", StringComparison.Ordinal) && attributeValue.Length > 2)
					description = XamlContextDescription.InAttributeValue;
			}
			
			if (Utils.IsInsideXmlComment(text, offset))
				description = XamlContextDescription.InComment;
			
			var context = new XamlContext() {
				Description = description,
				ActiveElement = active,
				AttributeName = attribute,
				AttributeValue = value,
				RawAttributeValue = attributeValue,
				ValueStartOffset = offsetFromValueStart,
				XmlnsDefinitions = xmlnsDefs,
				ParseInformation = info
			};

			DebugTimer.Stop("ResolveContext");
			
			return context;
		}
		
		public static QualifiedName ResolveCurrentElement(string text, int offset, Dictionary<string, string> xmlnsDefinitions)
		{
			if (offset < 0)
				return null;
			
			string elementName = text.GetWordAfterOffset(offset + 1);
			
			string prefix = "";
			string element = "";
			
			if (elementName.IndexOf(':') > -1) {
				string[] data = elementName.Split(':');
				prefix = data[0];
				element = data[1];
			} else {
				element = elementName;
			}
			
			string xmlns = xmlnsDefinitions.ContainsKey(prefix) ? xmlnsDefinitions[prefix] : string.Empty;
			
			return new QualifiedName(element, xmlns, prefix);
		}
		
		public static XamlCompletionContext ResolveCompletionContext(ITextEditor editor, char typedValue)
		{
			var context = new XamlCompletionContext(ResolveContext(editor.Document.Text, editor.FileName, editor.Caret.Line, editor.Caret.Column)) {
				PressedKey = typedValue,
				Editor = editor
			};
			
			return context;
		}
		
		static List<ICompletionItem> CreateListForAttributeName(XamlCompletionContext context, string[] existingItems, bool includeEvents)
		{
			QualifiedName lastElement = context.ActiveElement;
			XamlCompilationUnit cu = context.ParseInformation.BestCompilationUnit as XamlCompilationUnit;
			if (cu == null)
				return null;
			IReturnType rt = cu.CreateType(lastElement.Namespace, lastElement.Name.Trim('.'));
			if (rt == null)
				return null;
			var list = new List<ICompletionItem>();
			
			foreach (IProperty p in rt.GetProperties()) {
				if (p.IsPublic && (p.CanSet || p.ReturnType.IsCollectionReturnType()) && !existingItems.Contains(p.Name)) {
					list.Add(new XamlCodeCompletionItem(p));
				}
			}
			
			if (includeEvents) {
				foreach (IEvent e in rt.GetEvents()) {
					if (e.IsPublic && !existingItems.Contains(e.Name)) {
						list.Add(new XamlCodeCompletionItem(e));
					}
				}
			}
			
			if (!lastElement.Name.EndsWith(".", StringComparison.OrdinalIgnoreCase) && context.PressedKey != '.') {
				list.AddRange(GetListOfAttachedProperties(context, existingItems));
				
				if (includeEvents) {
					list.AddRange(GetListOfAttachedEvents(context, existingItems));
				}
				
				string xamlPrefix = Utils.GetXamlNamespacePrefix(context);
				
				foreach (string item in xamlNamespaceAttributes) {
					if (!existingItems.Contains(xamlPrefix + ":" + item))
						list.Add(new XamlCompletionItem(xamlPrefix, XamlNamespace, item));
				}
			}
			
			return list;
		}
		
		public static IEnumerable<ICompletionItem> CreateListForXmlnsCompletion(IProjectContent projectContent)
		{
			List<XmlnsCompletionItem> list = new List<XmlnsCompletionItem>();
			
			foreach (IProjectContent content in projectContent.ReferencedContents) {
				foreach (IAttribute att in content.GetAssemblyAttributes()) {
					if (att.PositionalArguments.Count == 2
					    && att.AttributeType.FullyQualifiedName == "System.Windows.Markup.XmlnsDefinitionAttribute") {
						list.Add(new XmlnsCompletionItem(att.PositionalArguments[0] as string, true));
					}
				}
				
				foreach (string @namespace in content.NamespaceNames) {
					if (!string.IsNullOrEmpty(@namespace))
						list.Add(new XmlnsCompletionItem(@namespace, content.AssemblyName));
				}
			}
			
			foreach (string @namespace in projectContent.NamespaceNames) {
				if (!string.IsNullOrEmpty(@namespace))
					list.Add(new XmlnsCompletionItem(@namespace, false));
			}
			
			return list
				.Distinct(new XmlnsEqualityComparer())
				.OrderBy(item => item, new XmlnsComparer())
				.Cast<ICompletionItem>();
		}
		
		sealed class XmlnsEqualityComparer : IEqualityComparer<XmlnsCompletionItem> {
			public bool Equals(XmlnsCompletionItem x, XmlnsCompletionItem y)
			{
				return x.Namespace == y.Namespace && x.Assembly == y.Assembly;
			}
			
			public int GetHashCode(XmlnsCompletionItem obj)
			{
				return string.IsNullOrEmpty(obj.Assembly) ? obj.Namespace.GetHashCode() : obj.Namespace.GetHashCode() ^ obj.Assembly.GetHashCode();
			}
		}
		
		sealed class XmlnsComparer : IComparer<XmlnsCompletionItem> {
			public int Compare(XmlnsCompletionItem x, XmlnsCompletionItem y)
			{
				if (x.IsUrl && y.IsUrl)
					return string.CompareOrdinal(x.Namespace, y.Namespace);
				if (x.IsUrl)
					return -1;
				if (y.IsUrl)
					return 1;
				if (x.Assembly == y.Assembly)
					return string.CompareOrdinal(x.Namespace, y.Namespace);
				else
					return string.CompareOrdinal(x.Assembly, y.Assembly);
			}
		}
		
		static string GetContentPropertyName(IReturnType type)
		{
			if (type == null)
				return string.Empty;
			
			IClass c = type.GetUnderlyingClass();
			
			if (c == null)
				return string.Empty;
			
			IAttribute contentProperty = c.Attributes
				.FirstOrDefault(attribute => attribute.AttributeType.FullyQualifiedName == "System.Windows.Markup.ContentPropertyAttribute");
			if (contentProperty != null) {
				return contentProperty.PositionalArguments.FirstOrDefault() as string
					?? (contentProperty.NamedArguments.ContainsKey("Name") ? contentProperty.NamedArguments["Name"] as string : string.Empty);
			}
			
			return string.Empty;
		}
		
		public static IList<ICompletionItem> CreateListForElement(XamlCompletionContext context, bool addOpeningBrace, bool classesOnly)
		{
			DebugTimer.Start();
			
			var items = GetClassesFromContext(context);
			var result = new List<ICompletionItem>();

			var last = context.ActiveElement;

			XamlCompilationUnit cu = context.ParseInformation.BestCompilationUnit as XamlCompilationUnit;
			
			IReturnType rt = null;
			
			bool isMember = false;
			
			if (last != null && cu != null) {
				if (!last.Name.Contains(".") || last.Name.EndsWith(".")) {
					rt = cu.CreateType(last.Namespace, last.Name.Trim('.'));
					string contentPropertyName = GetContentPropertyName(rt);
					if (!string.IsNullOrEmpty(contentPropertyName)) {
						string fullName = string.IsNullOrEmpty(last.Prefix) ? last.Name + "." + contentPropertyName : last.Prefix + ":" + last.Name + "." + contentPropertyName;
						MemberResolveResult mrr = XamlResolver.Resolve(fullName, context) as MemberResolveResult;
						
						if (mrr != null) {
							rt = mrr.ResolvedType;
							isMember = true;
						}
					}
				} else {
					string fullName = string.IsNullOrEmpty(last.Prefix) ? last.Name : last.Prefix + ":" + last.Name;
					MemberResolveResult mrr = XamlResolver.Resolve(fullName, context) as MemberResolveResult;
					
					if (mrr != null) {
						rt = mrr.ResolvedType;
						isMember = true;
					}
				}
			}
			
			bool isList = rt != null && rt.IsListReturnType();
			
			foreach (var ns in items) {
				foreach (var c in ns.Value) {
					if (!(c.ClassType == ClassType.Class && !c.IsAbstract && !c.IsStatic &&
					      !c.DerivesFrom("System.Attribute") &&
					      c.Methods.Any(m => m.IsConstructor && m.IsPublic)))
						continue;
					
					if (last != null && isList) {
						var possibleTypes = rt.GetMethods()
							.Where(a => a.Parameters.Count == 1 && a.Name == "Add")
							.Select(method => method.Parameters.First().ReturnType.GetUnderlyingClass());
						
						if (!possibleTypes.Any(t => c.ClassInheritanceTreeClassesOnly.Any(c2 => c2.FullyQualifiedName == t.FullyQualifiedName)))
							continue;
					}
					
					result.Add(new XamlCodeCompletionItem(c, ns.Key, addOpeningBrace));
				}
			}
			
			if (!(rt == null || isMember || classesOnly)) {
				foreach (IProperty p in rt.GetProperties()) {
					if (p.IsPublic && (p.CanSet || p.ReturnType.IsCollectionReturnType()))
						result.Add(new XamlCodeCompletionItem(p, last.Prefix, last.Name, addOpeningBrace));
				}
			}
			
			DebugTimer.Stop("CreateListForElement");
			
			return result;
		}

		public static IList<ICompletionItem> CreateListOfMarkupExtensions(XamlCompletionContext context)
		{
			var list = CreateListForElement(context, false, true);
			
			var neededItems = list
				.Where(i => ((i as XamlCodeCompletionItem).Entity as IClass).DerivesFrom("System.Windows.Markup.MarkupExtension"))
				.Select(
					selItem => {
						var it = selItem as XamlCodeCompletionItem;
						string text = it.Text;
						if (it.Text.EndsWith("Extension", StringComparison.Ordinal))
							text = text.Remove(it.Text.Length - "Extension".Length);
						return new XamlCodeCompletionItem(it.Entity, text);
					}
				)
				.Cast<ICompletionItem>();
			
			return neededItems.ToList();
		}

		public static ICompletionItemList CreateListForContext(XamlCompletionContext context)
		{
			XamlCompletionItemList list = new XamlCompletionItemList();
			
			ParseInformation info = context.ParseInformation;
			ITextEditor editor = context.Editor;
			
			switch (context.Description) {
				case XamlContextDescription.None:
					if (context.Forced) {
						list.Items.AddRange(standardElements.Select(item => new DefaultCompletionItem("<" + item.Text)).Cast<ICompletionItem>());
						list.Items.AddRange(CreateListForElement(context, true, false));
					}
					break;
				case XamlContextDescription.AtTag:
					if ((editor.Caret.Offset > 0 && editor.Document.GetCharAt(editor.Caret.Offset - 1) == '.') || context.PressedKey == '.') {
						var loc = editor.Document.OffsetToPosition(Utils.GetParentElementStart(editor));
						var existing = Utils.GetListOfExistingAttributeNames(editor.Document.Text, loc.Line, loc.Column);
						list.Items.AddRange(CreateListForAttributeName(context, existing, false));
					} else {
						list.Items.AddRange(standardElements);
						list.Items.AddRange(CreateListForElement(context, false, false));
					}
					break;
				case XamlContextDescription.InTag:
					var existingAttribs = Utils.GetListOfExistingAttributeNames(editor.Document.Text, editor.Caret.Line, editor.Caret.Column);
					list.Items.AddRange(CreateListForAttributeName(context, existingAttribs, true));
					
					QualifiedName last = context.ActiveElement;
					
					TypeResolveResult trr = new XamlResolver().Resolve(new ExpressionResult(last.Name, context), info, editor.Document.Text) as TypeResolveResult;
					
					IClass typeClass = (trr != null && trr.ResolvedType != null) ? trr.ResolvedType.GetUnderlyingClass() : null;
					
					if (typeClass != null && typeClass.DerivesFrom("System.Windows.DependencyObject")) {
						list.Items.AddRange(GetListOfAttachedProperties(context, existingAttribs));
						list.Items.AddRange(GetListOfAttachedEvents(context, existingAttribs));
					}
					
					list.Items.AddRange(standardAttributes);
					break;
				case XamlContextDescription.InAttributeValue:
					XamlCodeCompletionBinding.Instance.CtrlSpace(editor);
					break;
			}
			
			list.SortItems();
			
			return list;
		}

		public static IEnumerable<IInsightItem> CreateMarkupExtensionInsight(XamlCompletionContext context)
		{
			var markup = Utils.GetMarkupExtensionAtPosition(context.AttributeValue.ExtensionValue, context.Editor.Caret.Offset);
			var trr = ResolveMarkupExtensionType(markup, context);
			
			if (trr != null) {
				var ctors = trr.ResolvedType
					.GetMethods()
					.Where(m => m.IsPublic && m.IsConstructor && m.Parameters.Count >= markup.PositionalArguments.Count + 1)
					.OrderBy(m => m.Parameters.Count);
				
				yield return new MarkupExtensionInsightItem(new DefaultMethod(trr.ResolvedClass, trr.ResolvedClass.Name));
				
				foreach (var ctor in ctors)
					yield return new MarkupExtensionInsightItem(ctor);
			}
		}

		public static ICompletionItemList CreateMarkupExtensionCompletion(XamlCompletionContext context)
		{
			var list = new XamlCompletionItemList();
			var markup = Utils.GetMarkupExtensionAtPosition(context.AttributeValue.ExtensionValue, context.Editor.Caret.Offset);
			var trr = ResolveMarkupExtensionType(markup, context);
			
			if (trr == null) {
				list.Items.AddRange(CreateListOfMarkupExtensions(context));
				list.PreselectionLength = markup.ExtensionType.Length;
			} else {
				if (trr.ResolvedType != null) {
					if (markup.NamedArguments.Count == 0) {
						if (DoPositionalArgsCompletion(list, context, trr))
							DoNamedArgsCompletion(list, trr, markup);
					} else
						DoNamedArgsCompletion(list, trr, markup);
				}
			}
			
			list.SortItems();
			
			return list;
		}

		static void DoNamedArgsCompletion(XamlCompletionItemList list, TypeResolveResult trr, MarkupExtensionInfo markup)
		{
			var ctors = trr.ResolvedType.GetMethods().Where(m => m.IsConstructor && m.Parameters.Count >= markup.PositionalArguments.Count);
			if (ctors.Any(ctor => ctor.Parameters.Count >= markup.PositionalArguments.Count)) {
				list.Items.AddRange(trr.ResolvedType.GetProperties().Where(p => p.CanSet && p.IsPublic).Select(p => new XamlCodeCompletionItem(p, p.Name + "=")).Cast<ICompletionItem>());
			}
		}

		/// <remarks>returns true if elements from named args completion should be added afterwards.</remarks>
		static bool DoPositionalArgsCompletion(XamlCompletionItemList list, XamlCompletionContext context, TypeResolveResult trr)
		{
			switch (trr.ResolvedType.FullyQualifiedName) {
				case "System.Windows.Markup.ArrayExtension":
				case "System.Windows.Markup.NullExtension":
					// x:Null/x:Array does not need completion, ignore it
					break;
				case "System.Windows.Markup.StaticExtension":
					if (context.AttributeValue.ExtensionValue.PositionalArguments.Count <= 1)
						return DoStaticExtensionCompletion(list, context);
					break;
				case "System.Windows.Markup.TypeExtension":
					if (context.AttributeValue.ExtensionValue.PositionalArguments.Count <= 1) {
						list.Items.AddRange(CreateListForElement(context, false, true));
						AttributeValue selItem = Utils.GetInnermostMarkupExtensionInfo(context.AttributeValue.ExtensionValue)
							.PositionalArguments.LastOrDefault();
						string word = context.Editor.GetWordBeforeCaret().TrimEnd();
						if (selItem != null && selItem.IsString && word == selItem.StringValue) {
							list.PreselectionLength = selItem.StringValue.Length;
						}
					}
					break;
				default:
//							var ctors = trr.ResolvedType
//								.GetMethods()
//								.Where(m => m.IsPublic && m.IsConstructor && m.Parameters.Count >= markup.PositionalArguments.Count + 1)
//								.OrderBy(m => m.Parameters.Count);
//
//							//var ctor = FindCompletableCtor(ctors, markup.PositionalArguments.Count)
					break;
			}
			
			return true;
		}

		public static IEnumerable<IInsightItem> MemberInsight(MemberResolveResult result)
		{
			switch (result.ResolvedType.FullyQualifiedName) {
				case "System.Windows.Thickness":
					yield return new MemberInsightItem(result.ResolvedMember, "left");
					yield return new MemberInsightItem(result.ResolvedMember, "left, top");
					yield return new MemberInsightItem(result.ResolvedMember, "left, top, right, bottom");
					break;
				case "System.Windows.Size":
					yield return new MemberInsightItem(result.ResolvedMember, "width, height");
					break;
				case "System.Windows.Point":
					yield return new MemberInsightItem(result.ResolvedMember, "x, y");
					break;
				case "System.Windows.Rect":
					yield return new MemberInsightItem(result.ResolvedMember, "x, y, width, height");
					break;
			}
		}

		public static IEnumerable<ICompletionItem> MemberCompletion(XamlCompletionContext context, IReturnType type, string textPrefix)
		{
			if (type == null || type.GetUnderlyingClass() == null)
				yield break;
			
			var c = type.GetUnderlyingClass();
			
			switch (c.ClassType) {
				case ClassType.Class:
					if (context.Description == XamlContextDescription.InMarkupExtension) {
						foreach (IField f in c.Fields)
							yield return new XamlCodeCompletionItem(f, textPrefix + f.Name);
						foreach (IProperty p in c.Properties.Where(pr => pr.IsPublic && pr.IsStatic && pr.CanGet))
							yield return new XamlCodeCompletionItem(p, textPrefix + p.Name);
					}
					break;
				case ClassType.Enum:
					foreach (IField f in c.Fields)
						yield return new XamlCodeCompletionItem(f, textPrefix + f.Name);
					foreach (IProperty p in c.Properties.Where(pr => pr.IsPublic && pr.IsStatic && pr.CanGet))
						yield return new XamlCodeCompletionItem(p, textPrefix + p.Name);
					break;
				case ClassType.Struct:
					if (c.FullyQualifiedName == "System.Boolean") {
						yield return new DefaultCompletionItem("True");
						yield return new DefaultCompletionItem("False");
					}
					break;
				case ClassType.Delegate:
					IMethod invoker = c.Methods.Where(method => method.Name == "Invoke").FirstOrDefault();
					if (invoker != null && context.ActiveElement != null) {
						var item = context.ActiveElement;
						var evt = ResolveAttribute(context.AttributeName, context) as IEvent;
						if (evt == null)
							break;
						
						int offset = XmlParser.GetActiveElementStartIndex(context.Editor.Document.Text, context.Editor.Caret.Offset);
						
						if (offset == -1)
							break;
						
						var loc = context.Editor.Document.OffsetToPosition(offset);
						
						string prefix = Utils.GetXamlNamespacePrefix(context);
						string name = Utils.GetAttributeValue(context.Editor.Document.Text, loc.Line, loc.Column + 1, "name");
						if (string.IsNullOrEmpty(name))
							name = Utils.GetAttributeValue(context.Editor.Document.Text, loc.Line, loc.Column + 1, (string.IsNullOrEmpty(prefix) ? "" : prefix + ":") + "name");
						
						yield return new NewEventCompletionItem(evt, (string.IsNullOrEmpty(name)) ? item.Name : name);
						
						foreach (var eventItem in CompletionDataHelper.AddMatchingEventHandlers(context.Editor, invoker))
							yield return eventItem;
					}
					break;
			}
			
			var classes = c.ProjectContent.Classes.Where(
				cla => (cla.FullyQualifiedName == c.FullyQualifiedName + "s" ||
				        cla.FullyQualifiedName == c.FullyQualifiedName + "es"));
			foreach (var coll in classes) {
				foreach (var item in coll.Properties)
					yield return new DefaultCompletionItem(item.Name);
				foreach (var item in coll.Fields.Where(f => f.IsPublic && f.IsStatic && f.ReturnType.FullyQualifiedName == c.FullyQualifiedName))
					yield return new DefaultCompletionItem(item.Name);
			}
		}

		static IEntity ResolveAttribute(string attribute, XamlCompletionContext context)
		{
			XamlResolver resolver = new XamlResolver();
			var exp = new ExpressionResult(attribute, context);
			var mrr = resolver.Resolve(exp, context.ParseInformation, context.Editor.Document.Text) as MemberResolveResult;
			
			return mrr.ResolvedMember;
		}

		static bool DoStaticExtensionCompletion(XamlCompletionItemList list, XamlCompletionContext context)
		{
			AttributeValue selItem = Utils.GetInnermostMarkupExtensionInfo(context.AttributeValue.ExtensionValue)
				.PositionalArguments.LastOrDefault();
			if (context.PressedKey == '.') {
				if (selItem != null && selItem.IsString) {
					var rr = ResolveStringValue(selItem.StringValue, context) as TypeResolveResult;
					if (rr != null)
						list.Items.AddRange(MemberCompletion(context, rr.ResolvedType, string.Empty));
					return false;
				}
			} else {
				if (selItem != null && selItem.IsString) {
					int index = selItem.StringValue.IndexOf('.');
					string s = (index > -1) ? selItem.StringValue.Substring(0, index) : selItem.StringValue;
					var rr = ResolveStringValue(s, context) as TypeResolveResult;
					if (rr != null) {
						list.Items.AddRange(MemberCompletion(context, rr.ResolvedType, (index == -1) ? "." : string.Empty));
						
						list.PreselectionLength = (index > -1) ? selItem.StringValue.Length - index - 1 : 0;
						
						return false;
					} else
						DoStaticTypeCompletion(selItem, list, context);
				} else {
					DoStaticTypeCompletion(selItem, list, context);
				}
			}
			
			return true;
		}

		static void DoStaticTypeCompletion(AttributeValue selItem, XamlCompletionItemList list, XamlCompletionContext context)
		{
			var items = GetClassesFromContext(context);
			foreach (var ns in items) {
				list.Items.AddRange(ns.Value.Where(c => c.Fields.Any(f => f.IsStatic) || c.Properties.Any(p => p.IsStatic))
				                    .Select(c => new XamlCodeCompletionItem(c, ns.Key, false))
				                    .Cast<ICompletionItem>());
			}
			if (selItem != null && selItem.IsString) {
				list.PreselectionLength = selItem.StringValue.Length;
			}
		}

		static ResolveResult ResolveStringValue(string value, XamlCompletionContext context)
		{
			var resolver = new XamlResolver();
			var rr = resolver.Resolve(new ExpressionResult(value, context), context.ParseInformation, context.Editor.Document.Text);
			return rr;
		}

		public static TypeResolveResult ResolveMarkupExtensionType(MarkupExtensionInfo markup, XamlCompletionContext context)
		{
			XamlResolver resolver = new XamlResolver();
			XamlContextDescription desc = context.Description;
			context.Description = XamlContextDescription.AtTag;
			TypeResolveResult trr = resolver.Resolve(new ExpressionResult(markup.ExtensionType, context), context.ParseInformation, context.Editor.Document.Text) as TypeResolveResult;
			if (trr == null) trr = resolver.Resolve(new ExpressionResult(markup.ExtensionType + "Extension", context), context.ParseInformation, context.Editor.Document.Text) as TypeResolveResult;
			context.Description = desc;
			return trr;
		}

		public static IReturnType ResolveType(string name, XamlContext context)
		{
			XamlCompilationUnit cu = context.ParseInformation.BestCompilationUnit as XamlCompilationUnit;
			if (cu == null)
				return null;
			string prefix = "";
			int len = name.IndexOf(':');
			if (len > 0) {
				prefix = name.Substring(0, len);
				name = name.Substring(len + 1, name.Length - len - 1);
			}
			string namespaceName = "";
			if (context.XmlnsDefinitions.TryGetValue(prefix, out namespaceName)) {
				IReturnType rt = cu.CreateType(namespaceName, name);
				if (rt != null)
					return rt;
			}
			return null;
		}

		public static IEnumerable<ICompletionItem> AddMatchingEventHandlers(ITextEditor editor, IMethod delegateInvoker)
		{
			ParseInformation p = ParserService.GetParseInformation(editor.FileName);
			var unit = p.MostRecentCompilationUnit;
			var loc = editor.Document.OffsetToPosition(editor.Caret.Offset);
			IClass c = unit.GetInnermostClass(loc.Line, loc.Column);
			if (c == null)
				yield break;
			CompoundClass compound = c.GetCompoundClass() as CompoundClass;
			if (compound != null) {
				foreach (IClass part in compound.Parts) {
					foreach (IMethod m in part.Methods) {
						if (m.Parameters.Count != delegateInvoker.Parameters.Count)
							continue;
						
						if ((m.ReturnType != null && delegateInvoker.ReturnType != null) && m.ReturnType.DotNetName != delegateInvoker.ReturnType.DotNetName)
							continue;
						
						bool equal = true;
						for (int i = 0; i < m.Parameters.Count; i++) {
							equal &= CompareParameter(m.Parameters[i], delegateInvoker.Parameters[i]);
							if (!equal)
								break;
						}
						if (equal) {
							yield return new XamlCodeCompletionItem(m);
						}
					}
				}
			}
		}

		static bool CompareParameter(IParameter p1, IParameter p2)
		{
			bool result = p1.ReturnType.DotNetName == p2.ReturnType.DotNetName;
			
			result &= (p1.IsOut == p2.IsOut);
			result &= (p1.IsParams == p2.IsParams);
			result &= (p1.IsRef == p2.IsRef);
			
			return result;
		}

		static IDictionary<string, IEnumerable<IClass>> GetClassesFromContext(XamlCompletionContext context)
		{
			IProjectContent pc = context.ParseInformation.BestCompilationUnit.ProjectContent;
			
			var result = new Dictionary<string, IEnumerable<IClass>>();
			
			foreach (var ns in context.XmlnsDefinitions) {
				result.Add(ns.Key, XamlCompilationUnit.GetNamespaceMembers(pc, ns.Value));
			}
			
			return result;
		}

		static List<ICompletionItem> GetListOfAttachedProperties(XamlCompletionContext context, string[] existingItems)
		{
			List<ICompletionItem> result = new List<ICompletionItem>();
			IProjectContent pc = context.ParseInformation.BestCompilationUnit.ProjectContent;
			
			foreach (var ns in context.XmlnsDefinitions) {
				var list = XamlCompilationUnit.GetNamespaceMembers(pc, ns.Value);
				if (list != null) {
					foreach (IClass c in list.OfType<IClass>()) {
						if (c.ClassType != ClassType.Class)
							continue;
						if (c.IsAbstract && c.IsStatic)
							continue;
						if (c.ClassInheritanceTree.Any(b => b.FullyQualifiedName == "System.Attribute"))
							continue;
						if (!c.Methods.Any(m => m.IsConstructor && m.IsPublic))
							continue;
						
						var attachedProperties = c.Fields
							.Where(f =>
							       f.IsPublic &&
							       f.IsStatic &&
							       f.IsReadonly &&
							       f.ReturnType != null &&
							       f.ReturnType.FullyQualifiedName == "System.Windows.DependencyProperty" &&
							       f.Name.Length > "Property".Length &&
							       f.Name.EndsWith("Property", StringComparison.Ordinal) &&
							       c.Methods.Any(m =>
							                     m.IsPublic &&
							                     m.IsStatic &&
							                     m.Name.Length > 3 &&
							                     (m.Name.StartsWith("Get", StringComparison.Ordinal) || m.Name.StartsWith("Set", StringComparison.Ordinal)) &&
							                     m.Name.Remove(0, 3) == f.Name.Remove(f.Name.Length - "Property".Length)
							                    )
							      );
						
						result.AddRange(attachedProperties
						                .Select(item => {
						                        	string name = (!string.IsNullOrEmpty(ns.Key)) ? ns.Key + ":" : "";
						                        	string property = item.Name.Remove(item.Name.Length - "Property".Length);
						                        	name += c.Name + "." + item.Name.Remove(item.Name.Length - "Property".Length);
						                        	return new XamlCodeCompletionItem(new DefaultProperty(c, property) { ReturnType = GetAttachedPropertyType(item, c) }, name);
						                        }
						                       )
						                .Where(item => !existingItems.Any(str => str == item.Text))
						                .Cast<ICompletionItem>()
						               );
					}
				}
			}
			
			return result;
		}

		static List<ICompletionItem> GetListOfAttachedEvents(XamlCompletionContext context, string[] existingItems)
		{
			var items = GetClassesFromContext(context);
			var result = new List<ICompletionItem>();
			
			foreach (var ns in items) {
				foreach (IClass c in ns.Value) {
					if (c.ClassType != ClassType.Class)
						continue;
					if (c.IsAbstract && c.IsStatic)
						continue;
					if (c.ClassInheritanceTree.Any(b => b.FullyQualifiedName == "System.Attribute"))
						continue;
					if (!c.Methods.Any(m => m.IsConstructor && m.IsPublic))
						continue;
					
					var attachedEvents = c.Fields
						.Where(f =>
						       f.IsPublic &&
						       f.IsStatic &&
						       f.IsReadonly &&
						       f.ReturnType != null &&
						       f.ReturnType.FullyQualifiedName == "System.Windows.RoutedEvent" &&
						       f.Name.Length > "Event".Length &&
						       f.Name.EndsWith("Event", StringComparison.Ordinal) &&
						       c.Methods.Any(m =>
						                     m.IsPublic &&
						                     m.IsStatic &&
						                     m.Name.Length > 3 &&
						                     (m.Name.StartsWith("Add", StringComparison.Ordinal) || m.Name.StartsWith("Remove", StringComparison.Ordinal)) &&
						                     m.Name.EndsWith("Handler", StringComparison.Ordinal) &&
						                     IsMethodFromEvent(f, m)
						                    )
						      );
					
					result.AddRange(attachedEvents
					                .Select(
					                	item => new XamlCodeCompletionItem(
					                		new DefaultEvent(c, GetEventNameFromField(item)) {
					                			ReturnType = GetAttachedEventDelegateType(item, c)
					                		},
					                		(string.IsNullOrEmpty(ns.Key) ? "" : ns.Key + ":") + c.Name + "." + item.Name.Remove(item.Name.Length - "Event".Length)
					                	)
					                )
					                .Where(item => !existingItems.Any(str => str == item.Text))
					                .Cast<ICompletionItem>()
					               );
				}
			}
			
			return result;
		}

		static IReturnType GetAttachedEventDelegateType(IField field, IClass c)
		{
			if (c == null || field == null)
				return null;
			
			string eventName = field.Name.Remove(field.Name.Length - "Event".Length);
			
			IMethod method = c.Methods
				.Where(m =>
				       m.IsPublic &&
				       m.IsStatic &&
				       m.Parameters.Count == 2 &&
				       (m.Name == "Add" + eventName + "Handler" ||
				        m.Name == "Remove" + eventName + "Handler"))
				.FirstOrDefault();
			
			if (method == null)
				return null;
			
			return method.Parameters[1].ReturnType;
		}

		static IReturnType GetAttachedPropertyType(IField field, IClass c)
		{
			if (c == null || field == null)
				return null;
			
			string propertyName = field.Name.Remove(field.Name.Length - "Property".Length);
			
			IMethod method = c.Methods
				.Where(m =>
				       m.IsPublic &&
				       m.IsStatic &&
				       m.Name == "Get" + propertyName)
				.FirstOrDefault();
			
			if (method == null)
				return null;
			
			return method.ReturnType;
		}

		static string GetEventNameFromMethod(IMethod m)
		{
			string mName = m.Name;
			if (mName.StartsWith("Add", StringComparison.Ordinal))
				mName = mName.Remove(0, 3);
			else if (mName.StartsWith("Remove", StringComparison.Ordinal))
				mName = mName.Remove(0, 6);
			if (mName.EndsWith("Handler", StringComparison.Ordinal))
				mName = mName.Remove(mName.Length - "Handler".Length);
			
			return mName;
		}

		static string GetEventNameFromField(IField f)
		{
			string fName = f.Name;
			if (fName.EndsWith("Event", StringComparison.Ordinal))
				fName = fName.Remove(fName.Length - "Event".Length);
			
			return fName;
		}

		static bool IsMethodFromEvent(IField f, IMethod m)
		{
			return GetEventNameFromField(f) == GetEventNameFromMethod(m);
		}
	}
}