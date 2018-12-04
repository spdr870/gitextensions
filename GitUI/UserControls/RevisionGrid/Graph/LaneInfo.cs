using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GitCommands;
using GitUIPluginInterfaces;

namespace GitUI.UserControls.RevisionGrid.Graph
{
    public class LaneInfo
    {
        private static Random random = new Random();

        public LaneInfo(RevisionGraphRevision startRevision, RevisionGraphRevision orderRevision, LaneInfo derivedFrom)
        {
            Color = RevisionGraphLaneColor.GetColorForLane(startRevision.Objectid.GetHashCode());

            if (derivedFrom != null && Color == derivedFrom.Color)
            {
                Color = RevisionGraphLaneColor.GetColorForLane(startRevision.Objectid.GetHashCode() % 13);
            }

            IsNewLane = derivedFrom == null;

            OrderRevision = orderRevision;

            StartRevision = startRevision;
        }

        public RevisionGraphRevision StartRevision { get; set; }

        public RevisionGraphRevision OrderRevision { get; set; }

        public bool IsMergeLane { get; set; }

        public bool IsNewLane { get; private set; }

        public int StartScore
        {
            get
            {
                return StartRevision.Score;
            }
        }

        public int OrderScore
        {
            get
            {
                return OrderRevision.Score;
            }
        }

        public int Color { get; private set; }
    }
}
