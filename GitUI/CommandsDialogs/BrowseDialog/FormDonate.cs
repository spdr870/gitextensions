using System;
using System.Diagnostics;

namespace GitUI.CommandsDialogs.BrowseDialog
{
    public partial class FormDonate : GitExtensionsForm
    {
        public static readonly string DonationUrl =
            @"https://opencollective.com/gitextensions";

        public FormDonate()
        {
            InitializeComponent();
            InitializeComplete();
        }

        private void PictureBox1Click(object sender, EventArgs e)
        {
            Process.Start(DonationUrl);
        }
    }
}