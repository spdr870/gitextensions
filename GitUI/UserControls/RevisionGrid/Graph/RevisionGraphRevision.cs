﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GitCommands;
using GitUIPluginInterfaces;

namespace GitUI.UserControls.RevisionGrid.Graph
{
    // This class represents a revision, or node.
    //     *  <- child revision
    //     |
    //     *  <- parent revision
    public class RevisionGraphRevision
    {
        public RevisionGraphRevision(ObjectId objectId, int guessScore)
        {
            Objectid = objectId;

            Parents = new ConcurrentBag<RevisionGraphRevision>();
            Children = new ConcurrentBag<RevisionGraphRevision>();
            StartSegments = new SynchronizedCollection<RevisionGraphSegment>();

            Score = guessScore;
        }

        public void ApplyFlags(RevisionNodeFlags types)
        {
            IsRelative |= (types & RevisionNodeFlags.CheckedOut) != 0;
            HasRef = (types & RevisionNodeFlags.HasRef) != 0;
            IsCheckedOut = (types & RevisionNodeFlags.CheckedOut) != 0;
        }

        public bool IsRelative { get; set; }
        public bool HasRef { get; set; }
        public bool IsCheckedOut { get; set; }

        /// <summary>
        /// The score is used to order the revisions in topo-order. The initial score will be assigned when a revision is loaded
        /// from the commit log (the result of git.exe). The score will be adjusted, if required, when this revision is added as a parent
        /// to a revision with a higher score.
        /// </summary>
        public int Score { get; private set; }

        // This method is called to ensure that the score is higher than a given score.
        // E.g. the score needs to be higher that the score of its children.
        public int EnsureScoreIsAbove(int minimalScore)
        {
            if (minimalScore <= Score)
            {
                return Score;
            }

            Score = minimalScore;

            int maxScore = Score;
            foreach (RevisionGraphRevision parent in Parents)
            {
                maxScore = Math.Max(parent.EnsureScoreIsAbove(Score + 1), maxScore);
            }

            return maxScore;
        }

        public GitRevision GitRevision { get; set; }

        public ObjectId Objectid { get; set; }

        public ConcurrentBag<RevisionGraphRevision> Parents { get; }
        public ConcurrentBag<RevisionGraphRevision> Children { get; }
        public SynchronizedCollection<RevisionGraphSegment> StartSegments { get; }

        // Mark this commit, and all its parents, as relative. Used for branch highlighting.
        // By default, the current checkout will be marked relative.
        public void MakeRelative()
        {
            if (IsRelative)
            {
                return;
            }

            IsRelative = true;

            foreach (RevisionGraphRevision parent in Parents)
            {
                parent.MakeRelative();
            }
        }

        public void AddParent(RevisionGraphRevision parent, out int maxScore)
        {
            if (IsRelative)
            {
                parent.MakeRelative();
            }

            Parents.Add(parent);
            parent.AddChild(this);

            maxScore = parent.EnsureScoreIsAbove(Score + 1);

            StartSegments.Add(new RevisionGraphSegment(parent, this));
        }

        private void AddChild(RevisionGraphRevision child)
        {
            Children.Add(child);
        }
    }
}
