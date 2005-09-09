﻿// <file>
//     <copyright see="prj:///doc/copyright.txt">2002-2005 AlphaSierraPapa</copyright>
//     <license see="prj:///doc/license.txt">GNU General Public License</license>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using ICSharpCode.Core;

namespace ICSharpCode.SharpDevelop.Dom
{
	[Serializable]
	public class ReflectionClass : DefaultClass
	{
		Type type;
		
		const BindingFlags flags = BindingFlags.Instance  |
			BindingFlags.Static    |
			BindingFlags.NonPublic |
			BindingFlags.DeclaredOnly |
			BindingFlags.Public;
		
		List<IClass> innerClasses;
		
		public override List<IClass> InnerClasses {
			get {
				if (innerClasses == null) {
					innerClasses = new List<IClass>();
					foreach (Type nestedType in type.GetNestedTypes(flags)) {
						string name = nestedType.FullName.Replace('+', '.');
						innerClasses.Add(new ReflectionClass(CompilationUnit, nestedType, name, this));
					}
				}
				return innerClasses;
			}
		}
		
		static Dictionary<ReflectionClass, List<IField>>    fieldCache    = new Dictionary<ReflectionClass, List<IField>>();
		static Dictionary<ReflectionClass, List<IProperty>> propertyCache = new Dictionary<ReflectionClass, List<IProperty>>();
		static Dictionary<ReflectionClass, List<IMethod>>   methodCache   = new Dictionary<ReflectionClass, List<IMethod>>();
		static Dictionary<ReflectionClass, List<IEvent>>    eventCache    = new Dictionary<ReflectionClass, List<IEvent>>();
		
		public static void ClearMemberCache()
		{
			fieldCache.Clear();
			propertyCache.Clear();
			methodCache.Clear();
			eventCache.Clear();
		}
		
		public override List<IField> Fields {
			get {
				List<IField> fields;
				if (!fieldCache.TryGetValue(this, out fields)) {
					fields = new List<IField>();
					foreach (FieldInfo field in type.GetFields(flags)) {
						if (!field.IsPublic && !field.IsFamily) continue;
						if (!field.IsSpecialName) {
							fields.Add(new ReflectionField(field, this));
						}
					}
					fieldCache.Add(this, fields);
				}
				return fields;
			}
		}
		
		public override List<IProperty> Properties {
			get {
				List<IProperty> properties;
				if (!propertyCache.TryGetValue(this, out properties)) {
					properties = new List<IProperty>();
					foreach (PropertyInfo propertyInfo in type.GetProperties(flags)) {
						ReflectionProperty prop = new ReflectionProperty(propertyInfo, this);
						if (prop.IsPublic || prop.IsProtected)
							properties.Add(prop);
					}
					propertyCache.Add(this, properties);
				}
				return properties;
			}
		}
		
		public override List<IMethod> Methods {
			get {
				List<IMethod> methods;
				if (!methodCache.TryGetValue(this, out methods)) {
					methods = new List<IMethod>();
					
					foreach (ConstructorInfo constructorInfo in type.GetConstructors(flags)) {
						if (!constructorInfo.IsPublic && !constructorInfo.IsFamily) continue;
						IMethod newMethod = new ReflectionMethod(constructorInfo, this);
						methods.Add(newMethod);
					}
					
					foreach (MethodInfo methodInfo in type.GetMethods(flags)) {
						if (!methodInfo.IsPublic && !methodInfo.IsFamily) continue;
						if (!methodInfo.IsSpecialName) {
							IMethod newMethod = new ReflectionMethod(methodInfo, this);
							methods.Add(newMethod);
						}
					}
					methodCache.Add(this, methods);
				}
				return methods;
			}
		}
		
		public override List<IEvent> Events {
			get {
				List<IEvent> events;
				if (!eventCache.TryGetValue(this, out events)) {
					events = new List<IEvent>();
					foreach (EventInfo eventInfo in type.GetEvents(flags)) {
						events.Add(new ReflectionEvent(eventInfo, this));
					}
					eventCache.Add(this, events);
				}
				return events;
			}
		}
		
		public static bool IsDelegate(Type type)
		{
			return type.IsSubclassOf(typeof(Delegate)) && type != typeof(MulticastDelegate);
		}
		
		#region VoidClass / VoidReturnType
		public class VoidClass : ReflectionClass
		{
			public VoidClass(ICompilationUnit compilationUnit) : base(compilationUnit, typeof(void), typeof(void).FullName, null) {}
			
			protected override IReturnType CreateDefaultReturnType() {
				return new VoidReturnType(this);
			}
		}
		
		private class VoidReturnType : DefaultReturnType
		{
			public VoidReturnType(IClass c) : base(c) {}
			public override List<IMethod> GetMethods() {
				return new List<IMethod>(1);
			}
			public override List<IProperty> GetProperties() {
				return new List<IProperty>(1);
			}
			public override List<IField> GetFields() {
				return new List<IField>(1);
			}
			public override List<IEvent> GetEvents() {
				return new List<IEvent>(1);
			}
		}
		#endregion
		
		static void AddAttributes(IProjectContent pc, List<IAttribute> list, IList<CustomAttributeData> attributes)
		{
			foreach (CustomAttributeData att in attributes) {
				DefaultAttribute a = new DefaultAttribute(att.Constructor.DeclaringType.FullName);
				foreach (CustomAttributeTypedArgument arg in att.ConstructorArguments) {
					IReturnType type = ReflectionReturnType.Create(pc, arg.ArgumentType, false);
					a.PositionalArguments.Add(new AttributeArgument(type, arg.Value));
				}
				foreach (CustomAttributeNamedArgument arg in att.NamedArguments) {
					IReturnType type = ReflectionReturnType.Create(pc, arg.TypedValue.ArgumentType, false);
					a.NamedArguments.Add(arg.MemberInfo.Name, new AttributeArgument(type, arg.TypedValue.Value));
				}
				list.Add(a);
			}
		}
		
		public ReflectionClass(ICompilationUnit compilationUnit, Type type, string fullName, IClass declaringType) : base(compilationUnit, declaringType)
		{
			this.type = type;
			if (fullName.Length > 2 && fullName[fullName.Length - 2] == '`') {
				FullyQualifiedName = fullName.Substring(0, fullName.Length - 2);
			} else {
				FullyQualifiedName = fullName;
			}
			
			this.UseInheritanceCache = true;
			
			try {
				AddAttributes(compilationUnit.ProjectContent, this.Attributes, CustomAttributeData.GetCustomAttributes(type));
			} catch (Exception ex) {
				ICSharpCode.Core.MessageService.ShowError(ex);
			}
			
			// set classtype
			if (type.IsInterface) {
				this.ClassType = ClassType.Interface;
			} else if (type.IsEnum) {
				this.ClassType = ClassType.Enum;
			} else if (type.IsValueType) {
				this.ClassType = ClassType.Struct;
			} else if (IsDelegate(type)) {
				this.ClassType = ClassType.Delegate;
			} else {
				this.ClassType = ClassType.Class;
				foreach (IAttribute att in this.Attributes) {
					if (att.Name == "Microsoft.VisualBasic.CompilerServices.StandardModuleAttribute") {
						this.ClassType = ClassType.Module;
						break;
					}
				}
			}
			if (type.IsGenericTypeDefinition) {
				foreach (Type g in type.GetGenericArguments()) {
					this.TypeParameters.Add(new DefaultTypeParameter(this, g));
				}
			}
			
			ModifierEnum modifiers  = ModifierEnum.None;
			
			if (type.IsNestedAssembly) {
				modifiers |= ModifierEnum.Internal;
			}
			if (type.IsSealed) {
				modifiers |= ModifierEnum.Sealed;
			}
			if (type.IsAbstract) {
				modifiers |= ModifierEnum.Abstract;
			}
			
			if (type.IsNestedPrivate ) { // I assume that private is used most and public last (at least should be)
				modifiers |= ModifierEnum.Private;
			} else if (type.IsNestedFamily ) {
				modifiers |= ModifierEnum.Protected;
			} else if (type.IsNestedPublic || type.IsPublic) {
				modifiers |= ModifierEnum.Public;
			} else if (type.IsNotPublic) {
				modifiers |= ModifierEnum.Internal;
			} else if (type.IsNestedFamORAssem) {
				modifiers |= ModifierEnum.ProtectedOrInternal;
			} else if (type.IsNestedFamANDAssem) {
				modifiers |= ModifierEnum.Protected;
				modifiers |= ModifierEnum.Internal;
			}
			this.Modifiers = modifiers;
			
			// set base classes
			if (type.BaseType != null) { // it's null for System.Object ONLY !!!
				BaseTypes.Add(ReflectionReturnType.Create(compilationUnit.ProjectContent, type.BaseType, false));
			}
			
			foreach (Type iface in type.GetInterfaces()) {
				BaseTypes.Add(ReflectionReturnType.Create(compilationUnit.ProjectContent, iface, false));
			}
		}
	}
	
}
