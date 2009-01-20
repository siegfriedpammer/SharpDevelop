// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.ComponentModel;

namespace ICSharpCode.PythonBinding
{
	/// <summary>
	/// Interface that can:
	/// 
	/// 1) Create an IComponent given a type. 
	/// 2) Create a new object given its type name.
	/// 
	/// Used by the PythonFormVisitor class so it can be wired up to an 
	/// IDesignerHost and an IDesignerSerializationManager.
	/// </summary>
	public interface IComponentCreator
	{
		/// <summary>
		/// Creates a named component of the specified type.
		/// </summary>
		/// <param name="componentClass">The type of the component to be created.</param>
		/// <param name="name">The component name.</param>
		IComponent CreateComponent(Type componentClass, string name);
		
		/// <summary>
		/// Adds a component to the component creator.
		/// </summary>
		void Add(IComponent component, string name);
		
		/// <summary>
		/// Creates a new instance of the object given its type.
		/// </summary>
		/// <param name="arguments">Arguments passed to the type's constructor.</param>
		/// <param name="name">Name of the object.</param>
		/// <param name="addToContainer">If set to true then the is added to the design container.</param>
		object CreateInstance(Type type, ICollection arguments, string name, bool addToContainer);
		
		/// <summary>
		/// Gets the type given its name.
		/// </summary>
		Type GetType(string typeName);
	}
}