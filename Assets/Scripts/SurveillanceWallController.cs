using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RunawayChimps.Surveillance
{
    public class SurveillanceWallController : MonoBehaviour
    {
        private sealed class FeedPlaybackState
        {
            public int ViewIndex;
            public int CompletedLoops;
        }

        [Header("Physical monitors")]
        [SerializeField] private SecurityMonitorDisplay[] monitors;

        [Header("Available level feeds")]
        [SerializeField] private List<SecurityMonitorFeed> feeds = new();

        [Header("Playback")]
        [Min(0f)]
        [SerializeField] private float staticTransitionSeconds = 0.18f;
        [Min(0f)]
        [SerializeField] private float initialMonitorStaggerSeconds = 0.35f;
        [SerializeField] private bool rotateAdditionalFeeds = true;

        private readonly Dictionary<SecurityMonitorFeed, FeedPlaybackState> _feedStates = new();
        private readonly Dictionary<int, SecurityMonitorFeed> _slotAssignments = new();
        private readonly List<Coroutine> _playbackCoroutines = new();
        private int _nextFeedCursor;

        private void OnEnable()
        {
            StartPlayback();
        }

        private void OnDisable()
        {
            StopPlayback();
        }

        [ContextMenu("Restart Surveillance Playback")]
        public void StartPlayback()
        {
            StopPlayback();
            _slotAssignments.Clear();
            _nextFeedCursor = 0;

            if (monitors == null || monitors.Length == 0)
            {
                return;
            }

            int feedCount = feeds != null ? feeds.Count : 0;
            for (int slot = 0; slot < monitors.Length; slot++)
            {
                SecurityMonitorDisplay monitor = monitors[slot];
                if (monitor == null)
                {
                    continue;
                }

                SecurityMonitorFeed feed = slot < feedCount ? feeds[slot] : null;
                _slotAssignments[slot] = feed;

                if (feed == null)
                {
                    monitor.ShowNoSignal();
                    continue;
                }

                _nextFeedCursor = Mathf.Max(_nextFeedCursor, slot + 1);
                _playbackCoroutines.Add(StartCoroutine(RunMonitor(slot, monitor)));
            }
        }

        private void StopPlayback()
        {
            foreach (var routine in _playbackCoroutines)
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }
            }

            _playbackCoroutines.Clear();
        }

        private IEnumerator RunMonitor(int slot, SecurityMonitorDisplay monitor)
        {
            if (initialMonitorStaggerSeconds > 0f && slot > 0)
            {
                yield return new WaitForSecondsRealtime(initialMonitorStaggerSeconds * slot);
            }

            while (isActiveAndEnabled && monitor != null)
            {
                if (!_slotAssignments.TryGetValue(slot, out SecurityMonitorFeed feed) || feed == null)
                {
                    monitor.ShowNoSignal();
                    yield return new WaitForSecondsRealtime(0.5f);
                    continue;
                }

                if (!feed.HasViews)
                {
                    monitor.ShowNoSignal();
                    yield return new WaitForSecondsRealtime(0.5f);
                    continue;
                }

                FeedPlaybackState state = GetState(feed);
                int viewIndex = feed.GetNextValidViewIndex(state.ViewIndex);
                if (viewIndex < 0)
                {
                    monitor.ShowNoSignal();
                    yield return new WaitForSecondsRealtime(0.5f);
                    continue;
                }

                SecurityMonitorFeed.SurveillanceView view = feed.Views[viewIndex];
                state.ViewIndex = viewIndex;
                monitor.ShowFeed(feed, view.Image);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, view.Duration));

                if (staticTransitionSeconds > 0f)
                {
                    monitor.SetTransitionStatic(true);
                    yield return new WaitForSecondsRealtime(staticTransitionSeconds);
                    monitor.SetTransitionStatic(false);
                }

                int nextIndex = FindNextValidViewIndex(feed, viewIndex + 1);
                bool completedLoop = nextIndex >= 0 && nextIndex <= viewIndex;
                state.ViewIndex = nextIndex >= 0 ? nextIndex : viewIndex;

                if (!completedLoop)
                {
                    continue;
                }

                state.CompletedLoops++;
                if (ShouldShowRareInterrupt(feed, state.CompletedLoops))
                {
                    monitor.ShowFeed(feed, feed.RareInterruptImage);
                    yield return new WaitForSecondsRealtime(feed.RareInterruptDuration);

                    if (staticTransitionSeconds > 0f)
                    {
                        monitor.SetTransitionStatic(true);
                        yield return new WaitForSecondsRealtime(staticTransitionSeconds);
                        monitor.SetTransitionStatic(false);
                    }
                }

                if (rotateAdditionalFeeds && feeds != null && feeds.Count > monitors.Length)
                {
                    SecurityMonitorFeed replacement = FindNextUnassignedFeed(feed);
                    if (replacement != null)
                    {
                        _slotAssignments[slot] = replacement;
                    }
                }
            }
        }

        private FeedPlaybackState GetState(SecurityMonitorFeed feed)
        {
            if (!_feedStates.TryGetValue(feed, out FeedPlaybackState state))
            {
                state = new FeedPlaybackState();
                _feedStates.Add(feed, state);
            }

            return state;
        }

        private static int FindNextValidViewIndex(SecurityMonitorFeed feed, int startIndex)
        {
            if (feed == null || feed.Views == null || feed.Views.Count == 0)
            {
                return -1;
            }

            int normalized = startIndex % feed.Views.Count;
            return feed.GetNextValidViewIndex(normalized);
        }

        private static bool ShouldShowRareInterrupt(SecurityMonitorFeed feed, int completedLoops)
        {
            return feed != null
                && feed.RareInterruptImage != null
                && completedLoops > 0
                && completedLoops % feed.RareInterruptEveryLoops == 0;
        }

        private SecurityMonitorFeed FindNextUnassignedFeed(SecurityMonitorFeed currentFeed)
        {
            if (feeds == null || feeds.Count == 0)
            {
                return null;
            }

            for (int offset = 0; offset < feeds.Count; offset++)
            {
                int index = (_nextFeedCursor + offset) % feeds.Count;
                SecurityMonitorFeed candidate = feeds[index];
                if (candidate == null || candidate == currentFeed || IsAssigned(candidate))
                {
                    continue;
                }

                _nextFeedCursor = (index + 1) % feeds.Count;
                return candidate;
            }

            return null;
        }

        private bool IsAssigned(SecurityMonitorFeed feed)
        {
            foreach (var assignment in _slotAssignments.Values)
            {
                if (assignment == feed)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
