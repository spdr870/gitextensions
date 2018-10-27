﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GitCommands;
using GitUIPluginInterfaces;

namespace GitUI.UserControls.RevisionGrid.Graph
{
    // This class represents a revision, or node.
    //     *  <- revision
    //     |
    //     *  <- revision
    public class RevisionGraphRevision
    {
        public RevisionGraphRevision(ObjectId objectId, int guessScore)
        {
            Objectid = objectId;

            Parents = new ConcurrentBag<RevisionGraphRevision>();
            Children = new ConcurrentBag<RevisionGraphRevision>();
            StartSegments = new SynchronizedCollection<RevisionGraphSegment>();
            EndSegments = new ConcurrentBag<RevisionGraphSegment>();

            Score = guessScore;

            LaneColor = -1;
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

        // The score is used to order the revisions in topo-order. The initial score will be assigned when the revision is loaded
        // from the commit log (the result of git.exe). The score will be adjusted if required when this revision is added as a parent
        // to a revision with a higher score.
        public int Score { get; private set; }

        public int LaneColor { get; set; }

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

        public ConcurrentBag<RevisionGraphRevision> Parents { get; private set; }
        public ConcurrentBag<RevisionGraphRevision> Children { get; private set; }
        public SynchronizedCollection<RevisionGraphSegment> StartSegments { get; private set; }
        public ConcurrentBag<RevisionGraphSegment> EndSegments { get; private set; }

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

        public RevisionGraphSegment AddParent(RevisionGraphRevision parent, out int maxScore)
        {
            // Generate a LaneColor used for rendering
            if (Parents.Any())
            {
                parent.LaneColor = RevisionGraphLaneColor.GetLaneColor(parent.Objectid.GetHashCode());

                if (LaneColor == parent.LaneColor)
                {
                    parent.LaneColor += 1;
                }
            }
            else
            {
                if (parent.LaneColor == -1)
                {
                    parent.LaneColor = LaneColor;
                }
            }

            if (IsRelative)
            {
                parent.MakeRelative();
            }

            Parents.Add(parent);
            parent.AddChild(this);

            maxScore = parent.EnsureScoreIsAbove(Score);

            RevisionGraphSegment revisionGraphSegment = new RevisionGraphSegment(parent, this);
            parent.EndSegments.Add(revisionGraphSegment);
            StartSegments.Add(revisionGraphSegment);

            return revisionGraphSegment;
        }

        private void AddChild(RevisionGraphRevision child)
        {
            Children.Add(child);
        }
    }
}
