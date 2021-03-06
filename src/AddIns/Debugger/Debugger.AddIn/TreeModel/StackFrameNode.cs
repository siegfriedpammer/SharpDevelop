﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the BSD license (for details please see \src\AddIns\Debugger\Debugger.AddIn\license.txt)

using Debugger.MetaData;
using System.Collections.Generic;
using ICSharpCode.NRefactory.Ast;
using ICSharpCode.SharpDevelop.Debugging;

namespace Debugger.AddIn.TreeModel
{
	public class StackFrameNode: TreeNode
	{
		StackFrame stackFrame;
		
		public StackFrame StackFrame {
			get { return stackFrame; }
		}
		
		public StackFrameNode(StackFrame stackFrame)
		{
			this.stackFrame = stackFrame;
			
			this.Name = stackFrame.MethodInfo.Name;
			this.ChildNodes = LazyGetChildNodes();
		}
		
		IEnumerable<TreeNode> LazyGetChildNodes()
		{
			foreach(DebugParameterInfo par in stackFrame.MethodInfo.GetParameters()) {
				string imageName;
				var image = ExpressionNode.GetImageForParameter(out imageName);
				var expression = new ExpressionNode(image, par.Name, par.GetExpression());
				expression.ImageName = imageName;
				yield return expression;
			}
			foreach(DebugLocalVariableInfo locVar in stackFrame.MethodInfo.GetLocalVariables(this.StackFrame.IP)) {
				string imageName;
				var image = ExpressionNode.GetImageForLocalVariable(out imageName);
				var expression = new ExpressionNode(image, locVar.Name, locVar.GetExpression());
				expression.ImageName = imageName;
				yield return expression;
			}
			if (stackFrame.Thread.CurrentException != null) {
				yield return new ExpressionNode(null, "__exception", new IdentifierExpression("__exception"));
			}
		}
	}
}
