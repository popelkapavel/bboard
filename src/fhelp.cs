using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace bboard {
  public partial class fHelp:Form {
    public fHelp(string file) {
      InitializeComponent();      
      rtb.LoadFile(file);
    }
  }
}
