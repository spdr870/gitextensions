﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using GitCommands;
using GitExtUtils.GitUI;
using GitUI.Editor.RichTextBoxExtension;
using GitUIPluginInterfaces;
using ResourceManager;
using ResourceManager.CommitDataRenders;

namespace GitUI.CommitInfo
{
    public partial class CommitInfoHeader : GitModuleControl
    {
        private readonly IDateFormatter _dateFormatter = new DateFormatter();
        private readonly ILinkFactory _linkFactory = new LinkFactory();
        private readonly ICommitDataManager _commitDataManager;
        private readonly ICommitDataHeaderRenderer _commitDataHeaderRenderer;

        public event EventHandler<CommandEventArgs> CommandClicked;

        public CommitInfoHeader()
        {
            InitializeComponent();
            InitializeComplete();

            var labelFormatter = new TabbedHeaderLabelFormatter();
            var headerRenderer = new TabbedHeaderRenderStyleProvider();

            _commitDataManager = new CommitDataManager(() => Module);
            _commitDataHeaderRenderer = new CommitDataHeaderRenderer(labelFormatter, _dateFormatter, headerRenderer, _linkFactory);

            using (var g = CreateGraphics())
            {
                rtbRevisionHeader.Font = _commitDataHeaderRenderer.GetFont(g);
            }

            rtbRevisionHeader.SelectionTabs = _commitDataHeaderRenderer.GetTabStops().ToArray();
        }

        public void ShowCommitInfo(GitRevision revision, IReadOnlyList<ObjectId> children)
        {
            this.InvokeAsync(() =>
            {
                ////_RevisionHeader.BackColor = ColorHelper.MakeColorDarker(BackColor, 0.05);
                rtbRevisionHeader.Clear();

                var data = _commitDataManager.CreateFromRevision(revision, children);
                var header = _commitDataHeaderRenderer.Render(data, showRevisionsAsLinks: CommandClicked != null);
                rtbRevisionHeader.SetXHTMLText(header);
                LoadAuthorImage(revision);
            }).FileAndForget();
        }

        public string GetPlainText()
        {
            return rtbRevisionHeader.GetPlainText();
        }

        private void LoadAuthorImage(GitRevision revision)
        {
            var showAvatar = AppSettings.ShowAuthorAvatarInCommitInfo;
            avatarControl.Visible = showAvatar;

            if (!showAvatar)
            {
                return;
            }

            if (revision == null)
            {
                avatarControl.LoadImage(null);
                return;
            }

            avatarControl.LoadImage(revision.AuthorEmail ?? revision.CommitterEmail);
        }

        private void rtbRevisionHeader_ContentsResized(object sender, ContentsResizedEventArgs e)
        {
            rtbRevisionHeader.Height = Math.Max(e.NewRectangle.Height, DpiUtil.Scale(AppSettings.AuthorImageSizeInCommitInfo));
        }

        private void rtbRevisionHeader_KeyDown(object sender, KeyEventArgs e)
        {
            var rtb = sender as RichTextBox;
            if (rtb == null || !e.Control || e.KeyCode != Keys.C)
            {
                return;
            }

            // Override RichTextBox Ctrl-c handling to copy plain text
            Clipboard.SetText(rtb.GetSelectionPlainText());
            e.Handled = true;
        }

        private void rtbRevisionHeader_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            var link = _linkFactory.ParseLink(e.LinkText);

            try
            {
                var result = new Uri(link);
                if (result.Scheme == "gitext")
                {
                    CommandClicked?.Invoke(sender, new CommandEventArgs(result.Host, result.AbsolutePath.TrimStart('/')));
                }
                else
                {
                    using (var process = new Process
                    {
                        EnableRaisingEvents = false,
                        StartInfo = { FileName = result.AbsoluteUri }
                    })
                    {
                        process.Start();
                    }
                }
            }
            catch (UriFormatException)
            {
            }
        }

        private void rtbRevisionHeader_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.XButton1)
            {
                DoCommandClick("navigatebackward");
            }
            else if (e.Button == MouseButtons.XButton2)
            {
                DoCommandClick("navigateforward");
            }

            void DoCommandClick(string command)
            {
                CommandClicked?.Invoke(this, new CommandEventArgs(command, null));
            }
        }
    }
}
