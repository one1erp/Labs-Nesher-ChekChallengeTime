using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Telerik.WinControls;

namespace CheckChallengeTime
{
    public partial class CheckChallengeCtrl : Telerik.WinControls.UI.RadForm
    {
      
 


        public CheckChallengeCtrl(IEnumerable<string> iEnumerable, string message,bool visibleTitle)
        {
            InitializeComponent();
            radListView1.AllowEdit = false;
            radLabel1.Visible = visibleTitle;
           radLabel1.Text = message;
            foreach (var item in iEnumerable)
            {


                radListView1.Items.Add(item);
            }
        }



        private void radButton1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }


}
