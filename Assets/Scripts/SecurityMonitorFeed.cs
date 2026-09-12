using System;
using System.Collections.Generic;
using UnityEngine;

namespace RunawayChimps.Surveillance
{
    [CreateAssetMenu(
        menuName = "Runaway Chimps/Surveillance/Monitor Feed",
        fileName = "SecurityMonitorFeed")]
    public class SecurityMonitorFeed : ScriptableObject
    {
        [Serializable]
        public class SurveillanceView
        {
            [Tooltip("Still image shown on the monitor for this surveillance view.")]
            public Texture2D Image;

            [Min(0.1f)]
            [Tooltip("Seconds to show this view before the next static transition.")]
            public float Duration = 5f;
        }

        [Header("On-screen label")]
        [Min(0)]
        [SerializeField] private int sectorNumber = 1;
        [SerializeField] private string sectorName = "PRIMATE CONTAINMENT";

        [Header("Recorded surveillance views")]
        [SerializeField] private List<SurveillanceView> views = new();

        [Header("Optional rare interrupt")]
        [SerializeField] private Texture2D rareInterruptImage;
        [Min(1)]
        [SerializeField] private int rareInterruptEveryLoops = 4;
        [Min(0.1f)]
        [SerializeField] private float rareInterruptDuration = 1f;

        public string SectorLabel => $"SECTOR {Mathf.Max(0, sectorNumber):00}";
        public string SectorName => string.IsNullOrWhiteSpace(sectorName) ? "UNNAMED SECTOR" : sectorName.Trim();
        public IReadOnlyList<SurveillanceView> Views => views;
        public Texture2D RareInterruptImage => rareInterruptImage;
        public int RareInterruptEveryLoops => Mathf.Max(1, rareInterruptEveryLoops);
        public float RareInterruptDuration => Mathf.Max(0.1f, rareInterruptDuration);

        public bool HasViews
        {
            get
            {
                if (views == null || views.Count == 0)
                {
                    return false;
                }

                foreach (var view in views)
                {
                    if (view != null && view.Image != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int GetNextValidViewIndex(int startIndex)
        {
            if (views == null || views.Count == 0)
            {
                return -1;
            }

            int normalizedStart = Mathf.Clamp(startIndex, 0, views.Count - 1);
            for (int offset = 0; offset < views.Count; offset++)
            {
                int index = (normalizedStart + offset) % views.Count;
                var view = views[index];
                if (view != null && view.Image != null)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
