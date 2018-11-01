using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace GitUI.UserControls.RevisionGrid.Graph
{
    internal static class OrderedRowCacheBuilder
    {
        [Pure]
        public static int Rebuild(List<RevisionGraphRow> orderedRowCache, int currentRowIndex, int lastToCacheRowIndex, int nextIndex, in IReadOnlyList<RevisionGraphRevision> orderedNodesCache)
        {
            int buildUntilScore = 0;

            int cacheCount = orderedNodesCache.Count;
            while (nextIndex <= lastToCacheRowIndex && cacheCount > nextIndex)
            {
                bool startSegmentsAdded = false;

                RevisionGraphRevision revision = orderedNodesCache[nextIndex];

                // The list containing the segments is created later. We can set the correct capacity then, to prevent resizing
                List<RevisionGraphSegment> segments;

                if (nextIndex > 0)
                {
                    // Copy lanes from last row
                    RevisionGraphRow previousRevisionGraphRow = orderedRowCache[nextIndex - 1];

                    // Create segments list with te correct capacity
                    segments = new List<RevisionGraphSegment>(previousRevisionGraphRow.Segments.Count + revision.StartSegments.Count);

                    // Loop through all segments that do not end in this row
                    foreach (var segment in previousRevisionGraphRow.Segments.Where(s => s.Parent != previousRevisionGraphRow.Revision))
                    {
                        segments.Add(segment);

                        // This segments continues in the next row. Copy all other segments that start from this revision to this lane.
                        if (revision == segment.Parent && !startSegmentsAdded)
                        {
                            startSegmentsAdded = true;
                            segments.AddRange(revision.StartSegments);
                        }
                    }
                }
                else
                {
                    // Create segments list with te correct capacity
                    segments = new List<RevisionGraphSegment>(revision.StartSegments.Count);
                }

                if (!startSegmentsAdded)
                {
                    // Add new segments started by this revision to the end
                    segments.AddRange(revision.StartSegments);
                }

                orderedRowCache.Add(new RevisionGraphRow(revision, segments));
                buildUntilScore = revision.Score;
                nextIndex++;
            }

            return buildUntilScore;
        }
    }
}