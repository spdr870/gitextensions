﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GitCommands;
using GitUIPluginInterfaces;

namespace GitUI.UserControls.RevisionGrid.Graph
{
    // The RevisionGraph contains all the basic structures needed to render the graph.
    public class RevisionGraph
    {
        // Some unordered collections with raw data
        private ConcurrentDictionary<ObjectId, RevisionGraphRevision> _nodeByObjectId = new ConcurrentDictionary<ObjectId, RevisionGraphRevision>();
        private ConcurrentBag<RevisionGraphRevision> _nodes = new ConcurrentBag<RevisionGraphRevision>();

        // The maxscore is used to keep a chronological order during graph buiding. It is cheaper than doing _nodes.Max(n => n.Score)
        private int _maxScore = 0;

        // The nodecache is an ordered list with the nodes. This is used to be able to draw commits before the graph is completed.
        private List<RevisionGraphRevision> _orderedNodesCache = null;
        private bool _reorder = true;
        private int _orderedUntillScore = -1;

        // The orderedrowcache contains the rows with the segments stored in lanes.
        private List<RevisionGraphRow> _orderedRowCache = null;
        private bool _rebuild = true;
        private int _buildUntillScore = -1;

        // When the cache is updated, this action can be used to invalidate the UI
        public event Action Updated;

        public void Clear()
        {
            _maxScore = 0;
            _nodeByObjectId = new ConcurrentDictionary<ObjectId, RevisionGraphRevision>();
            _nodes = new ConcurrentBag<RevisionGraphRevision>();
            _orderedNodesCache = null;
            _orderedRowCache = null;
        }

        public int Count => _nodes.Count;

        public int GetCachedCount()
        {
            if (_orderedRowCache == null)
            {
                return 0;
            }

            return _orderedRowCache.Count;
        }

        // Build the revision graph. There are two caches that are build in this method.
        // cache 1: an ordered list of the revisions. This is very cheap to build. (_orderedNodesCache)
        // cache 2: an ordered list of all prepared graphrows. This is expensive to build. (_orderedRowCache)
        // untillRow: the row that needs to be displayed. This ensures the orded revisions are available untill this index.
        // graphUntillRow: the graph can be build per x rows. This defines the row index that the graph will be cached until.
        public void CacheTo(int untillRow, int graphUntillRow)
        {
            if (_orderedNodesCache == null || _reorder || _orderedNodesCache.Count < untillRow)
            {
                _orderedNodesCache = _nodes.OrderBy(n => n.Score).ToList();
                if (_orderedNodesCache.Count > 0)
                {
                    _orderedUntillScore = _orderedNodesCache.Last().Score;
                }

                _reorder = false;
            }

            if (_orderedRowCache == null || _rebuild)
            {
                _orderedRowCache = new List<RevisionGraphRow>(untillRow);
                _rebuild = false;
            }

            int nextIndex = _orderedRowCache.Count;
            if (nextIndex <= graphUntillRow)
            {
                int cacheCount = _orderedNodesCache.Count;
                while (nextIndex <= graphUntillRow && cacheCount > nextIndex)
                {
                    bool startSegmentsAdded = false;

                    RevisionGraphRevision revision = _orderedNodesCache[nextIndex];

                    // The list containing the segments is created later. We can set the correct capacity then, to prevent resizing
                    List<RevisionGraphSegment> segments;

                    if (nextIndex > 0)
                    {
                        // Copy lanes from last row
                        RevisionGraphRow previousRevisionGraphRow = _orderedRowCache[nextIndex - 1];

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

                    _orderedRowCache.Add(new RevisionGraphRow(revision, segments));
                    _buildUntillScore = revision.Score;
                    nextIndex++;
                }

                Updated?.Invoke();
            }
        }

        public bool IsRowRelative(int row)
        {
            if (_orderedNodesCache == null || _orderedNodesCache.Count < row)
            {
                return false;
            }

            return _orderedNodesCache[row].IsRelative;
        }

        public bool IsRevisionRelative(ObjectId objectId)
        {
            if (_nodeByObjectId.TryGetValue(objectId, out RevisionGraphRevision revision))
            {
                return revision.IsRelative;
            }

            return false;
        }

        public bool TryGetNode(ObjectId objectId, out RevisionGraphRevision revision)
        {
            return _nodeByObjectId.TryGetValue(objectId, out revision);
        }

        public bool TryGetRowIndex(ObjectId objectId, out int index)
        {
            if (_orderedNodesCache == null || !TryGetNode(objectId, out RevisionGraphRevision revision))
            {
                index = 0;
                return false;
            }

            index = _orderedNodesCache.IndexOf(revision);
            return index >= 0;
        }

        public RevisionGraphRevision GetNodeForRow(int row)
        {
            if (_orderedNodesCache == null || row >= _orderedNodesCache.Count)
            {
                return null;
            }

            return _orderedNodesCache.ElementAt(row);
        }

        public RevisionGraphRow GetSegmentsForRow(int row)
        {
            if (_orderedRowCache == null || row >= _orderedRowCache.Count)
            {
                return null;
            }

            return _orderedRowCache[row];
        }

        public void HighlightBranch(ObjectId id)
        {
            // Clear current higlighting
            foreach (var revision in _nodes)
            {
                revision.IsRelative = false;
            }

            // Highlight revision
            if (TryGetNode(id, out RevisionGraphRevision revisionGraphRevision))
            {
                revisionGraphRevision.MakeRelative();
            }
        }

        // This method will validate the topo order in brute force.
        // Only used for unit testing.
        public bool ValidateTopoOrder()
        {
            int currentIndex = 0;
            foreach (var node in _orderedNodesCache)
            {
                foreach (var parent in node.Parents)
                {
                    if (!TryGetRowIndex(parent.Objectid, out int parentIndex) || parentIndex < currentIndex)
                    {
                        return false;
                    }
                }

                foreach (var child in node.Children)
                {
                    if (!TryGetRowIndex(child.Objectid, out int childIndex) || childIndex > currentIndex)
                    {
                        return false;
                    }
                }

                currentIndex++;
            }

            return true;
        }

        // Add a single revision from the git log.
        public void Add(GitRevision revision, RevisionNodeFlags types)
        {
            if (!_nodeByObjectId.TryGetValue(revision.ObjectId, out RevisionGraphRevision revisionGraphRevision))
            {
                // This revision is added from the log, but not seen before. This is probably a root node (new branch) OR the revisions
                // are not in topo order. If this the case, we deal with it later.
                revisionGraphRevision = new RevisionGraphRevision(revision.ObjectId, ++_maxScore);
                revisionGraphRevision.ApplyFlags(types);
                revisionGraphRevision.LaneColor = revisionGraphRevision.IsCheckedOut ? 0 : _maxScore;

                revisionGraphRevision.GitRevision = revision;
                _nodeByObjectId.TryAdd(revision.ObjectId, revisionGraphRevision);
                _nodes.Add(revisionGraphRevision);
            }
            else
            {
                // This revision was added as a parent before. Probably only the objectid is known. Set all the other properties.
                revisionGraphRevision.GitRevision = revision;
                revisionGraphRevision.ApplyFlags(types);

                // Since this revision was added earlier, but is now found in the log. Increase the score to the current maxScore
                // to keep the order ok.
                revisionGraphRevision.EnsureScoreIsAbove(++_maxScore);
                _nodes.Add(revisionGraphRevision);
            }

            // No build the revisions parent/child structure. The parents need to added here. The child structure is kept in synch in
            // the RevisionGraphRevision class.
            foreach (ObjectId parentObjectId in revision.ParentIds)
            {
                if (!_nodeByObjectId.TryGetValue(parentObjectId, out RevisionGraphRevision parentRevisionGraphRevision))
                {
                    // This parent is not loaded before. Create a new (partial) revision. We will complete the info in the revision
                    // when this revision is loaded from the log.
                    parentRevisionGraphRevision = new RevisionGraphRevision(parentObjectId, ++_maxScore);
                    _nodeByObjectId.TryAdd(parentObjectId, parentRevisionGraphRevision);

                    // Store the newly created segment (connection between 2 revisions)
                    revisionGraphRevision.AddParent(parentRevisionGraphRevision, out int newMaxScore);
                    _maxScore = Math.Max(_maxScore, newMaxScore);
                }
                else
                {
                    // This revision is already loaded, add the existing revision to the parents list of new revision.
                    // If the current score is lower, cache is invalid. The new score will (probably) be higher.
                    ResetCacheIfNeeded(parentRevisionGraphRevision);

                    // Store the newly created segment (connection between 2 revisions)
                    revisionGraphRevision.AddParent(parentRevisionGraphRevision, out int newMaxScore);
                    _maxScore = Math.Max(_maxScore, newMaxScore);
                }
            }

            ResetCacheIfNeeded(revisionGraphRevision);
        }

        private void ResetCacheIfNeeded(RevisionGraphRevision revisionGraphRevision)
        {
            if (revisionGraphRevision.Score <= _orderedUntillScore)
            {
                _reorder = true;
            }

            if (revisionGraphRevision.Score <= _buildUntillScore)
            {
                _rebuild = true;
            }
        }
    }
}
