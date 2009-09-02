﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="David Srbecký" email="dsrbecky@gmail.com"/>
//     <version>$Revision$</version>
// </file>

using System;

namespace Debugger.Tests.TestPrograms
{
	public class StackFrame_LocalVariables
	{
		public static void Main()
		{
			int i = 0;
			string s = "S";
			string[] args = new string[] {"p1"};
			object n = null;
			object o = new object();
			System.Diagnostics.Debugger.Break();
		}
	}
}

#if TEST_CODE
namespace Debugger.Tests {
	public partial class DebuggerTests
	{
		[NUnit.Framework.Test]
		public void StackFrame_LocalVariables()
		{
			StartTest("StackFrame_LocalVariables.cs");
			
			ObjectDump("LocalVariables", process.SelectedStackFrame.GetLocalVariableValues());
			
			EndTest();
		}
	}
}
#endif

#if EXPECTED_OUTPUT
<?xml version="1.0" encoding="utf-8"?>
<DebuggerTests>
  <Test
    name="StackFrame_LocalVariables.cs">
    <ProcessStarted />
    <ModuleLoaded>mscorlib.dll (No symbols)</ModuleLoaded>
    <ModuleLoaded>StackFrame_LocalVariables.exe (Has symbols)</ModuleLoaded>
    <DebuggingPaused>Break StackFrame_LocalVariables.cs:21,4-21,40</DebuggingPaused>
    <LocalVariables
      Capacity="8"
      Count="5">
      <Item>
        <Value
          AsString="0"
          Expression="i"
          PrimitiveValue="0"
          Type="System.Int32" />
      </Item>
      <Item>
        <Value
          AsString="S"
          Expression="s"
          IsReference="True"
          PrimitiveValue="S"
          Type="System.String" />
      </Item>
      <Item>
        <Value
          ArrayDimensions="{1}"
          ArrayLength="1"
          ArrayRank="1"
          AsString="{System.String[]}"
          Expression="args"
          IsReference="True"
          PrimitiveValue="{Exception: Value is not a primitive type}"
          Type="System.String[]" />
      </Item>
      <Item>
        <Value
          AsString="null"
          Expression="n"
          IsNull="True"
          IsReference="True"
          PrimitiveValue="{Exception: Value is not a primitive type}"
          Type="System.Object" />
      </Item>
      <Item>
        <Value
          AsString="{System.Object}"
          Expression="o"
          IsReference="True"
          PrimitiveValue="{Exception: Value is not a primitive type}"
          Type="System.Object" />
      </Item>
    </LocalVariables>
    <ProcessExited />
  </Test>
</DebuggerTests>
#endif // EXPECTED_OUTPUT