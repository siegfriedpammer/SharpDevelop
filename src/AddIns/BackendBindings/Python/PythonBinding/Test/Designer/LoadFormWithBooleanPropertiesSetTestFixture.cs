// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using ICSharpCode.PythonBinding;
using NUnit.Framework;
using PythonBinding.Tests.Utils;

namespace PythonBinding.Tests.Designer
{
	[TestFixture]
	public class LoadFormWithBooleanPropertiesSetTestFixture : LoadFormTestFixtureBase
	{		
		string pythonCode = "class TestForm(System.Windows.Forms.Form):\r\n" +
							"    def InitializeComponent(self):\r\n" +
							"        self.SuspendLayout()\r\n" +
							"        # \r\n" +
							"        # TestForm\r\n" +
							"        # \r\n" +
							"        self.AllowDrop = False\r\n" +
							"        self.Enabled = False\r\n" +
							"        self.Name = \"TestForm\"\r\n" +
							"        self.ResumeLayout(False)\r\n";
		Form form;
		
		[TestFixtureSetUp]
		public void SetUpFixture()
		{
			PythonFormWalker walker = new PythonFormWalker(this, new MockDesignerLoaderHost());
			form = walker.CreateForm(pythonCode);
		}

		[TestFixtureTearDown]
		public void TearDownFixture()
		{
			form.Dispose();
		}
								
		[Test]
		public void FormEnabledIsFalse()
		{
			Assert.IsFalse(form.Enabled);
		}
		
		[Test]
		public void FormAllowDropIsFalse()
		{
			Assert.IsFalse(form.AllowDrop);
		}		
	}
}