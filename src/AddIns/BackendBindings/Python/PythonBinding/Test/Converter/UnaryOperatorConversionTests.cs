// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using ICSharpCode.PythonBinding;
using NUnit.Framework;

namespace PythonBinding.Tests.Converter
{
	[TestFixture]
	public class UnaryOperatorConversionTests
	{	
		[Test]
		public void MinusOne()
		{
			string csharp = "class Foo\r\n" +
							"{\r\n" +
							"    public void Run(int i)\r\n" +
							"    {\r\n" +
							"        i = -1;\r\n" +
							"    }\r\n" +
							"}";

			string expectedPython = "class Foo(object):\r\n" +
									"\tdef Run(self, i):\r\n" +
									"\t\ti = -1";
	
			CSharpToPythonConverter converter = new CSharpToPythonConverter();
			string code = converter.Convert(csharp);
			
			Assert.AreEqual(expectedPython, code);
		}
		
		[Test]
		public void PlusOne()
		{
			string csharp = "class Foo\r\n" +
							"{\r\n" +
							"    public void Run(int i)\r\n" +
							"    {\r\n" +
							"        i = +1;\r\n" +
							"    }\r\n" +
							"}";

			string expectedPython = "class Foo(object):\r\n" +
									"\tdef Run(self, i):\r\n" +
									"\t\ti = +1";
		
			CSharpToPythonConverter converter = new CSharpToPythonConverter();
			string code = converter.Convert(csharp);
			
			Assert.AreEqual(expectedPython, code);
		}
		
		[Test]
		public void NotOperator()
		{
			string csharp = "class Foo\r\n" +
							"{\r\n" +
							"    public void Run(bool i)\r\n" +
							"    {\r\n" +
							"        j = !i;\r\n" +
							"    }\r\n" +
							"}";

			string expectedPython = "class Foo(object):\r\n" +
									"\tdef Run(self, i):\r\n" +
									"\t\tj = not i";
		
			CSharpToPythonConverter converter = new CSharpToPythonConverter();
			string code = converter.Convert(csharp);
			
			Assert.AreEqual(expectedPython, code);
		}		
		
		[Test]
		public void BitwiseNotOperator()
		{
			string csharp = "class Foo\r\n" +
							"{\r\n" +
							"    public void Run(bool i)\r\n" +
							"    {\r\n" +
							"        j = ~i;\r\n" +
							"    }\r\n" +
							"}";

			string expectedPython = "class Foo(object):\r\n" +
									"\tdef Run(self, i):\r\n" +
									"\t\tj = ~i";
		
			CSharpToPythonConverter converter = new CSharpToPythonConverter();
			string code = converter.Convert(csharp);
			
			Assert.AreEqual(expectedPython, code);
		}			
	}
}