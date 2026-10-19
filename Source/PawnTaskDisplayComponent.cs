using System;
using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PawnTaskDisplay
{
    public sealed class PawnTaskDisplayComponent : MapComponent
    {
        private sealed class PawnLabelEntry
        {
            public Pawn Pawn;
            public string Label;
        }

        private readonly Dictionary<Pawn, PawnLabelEntry> entriesByPawn = new Dictionary<Pawn, PawnLabelEntry>();
        private readonly List<PawnLabelEntry> renderEntries = new List<PawnLabelEntry>();
        private readonly HashSet<Pawn> seenPawns = new HashSet<Pawn>();
        private readonly List<Pawn> stalePawns = new List<Pawn>();
        private readonly GUIContent labelContent = new GUIContent();

        private GUIStyle labelStyle;
        private GUIStyle shadowStyle;
        private float nextRefreshAt;
        private bool wasDisplayEligible;
        private int observedSettingsRevision = -1;
        private int styleRevision = -1;

        public PawnTaskDisplayComponent(Map map) : base(map)
        {
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();

            PawnTaskDisplaySettings settings = PawnTaskDisplayMod.Settings;
            if (settings == null || Find.CurrentMap != map || !settings.ShowTaskLabels)
            {
                DisableAndClear();
                return;
            }

            bool eligible = IsHoldConditionMet(settings);
            if (!eligible)
            {
                wasDisplayEligible = false;
                return;
            }

            float now = Time.realtimeSinceStartup;
            bool settingsChanged = observedSettingsRevision != PawnTaskDisplayMod.SettingsRevision;
            if (!wasDisplayEligible || settingsChanged || now >= nextRefreshAt)
            {
                observedSettingsRevision = PawnTaskDisplayMod.SettingsRevision;
                EnsureStyles(settings);
                RefreshCache(settings);
                nextRefreshAt = now + settings.RefreshIntervalSeconds;
            }

            wasDisplayEligible = true;
        }

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();

            PawnTaskDisplaySettings settings = PawnTaskDisplayMod.Settings;
            Event currentEvent = Event.current;
            if (settings == null || !settings.ShowTaskLabels || Find.CurrentMap != map ||
                currentEvent == null || currentEvent.type != EventType.Repaint ||
                !IsHoldConditionMet(settings) || renderEntries.Count == 0)
            {
                return;
            }

            EnsureStyles(settings);
            CellRect viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(1);
            for (int i = 0; i < renderEntries.Count; i++)
            {
                PawnLabelEntry entry = renderEntries[i];
                Pawn pawn = entry.Pawn;
                if (pawn == null || !pawn.Spawned || pawn.Map != map || !viewRect.Contains(pawn.Position))
                {
                    continue;
                }

                DrawLabel(pawn, entry.Label, settings);
            }
        }

        private void RefreshCache(PawnTaskDisplaySettings settings)
        {
            seenPawns.Clear();
            renderEntries.Clear();

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Faction != Faction.OfPlayer || pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
                {
                    continue;
                }

                Job job = pawn.CurJob;
                if (job == null || job.def == null)
                {
                    continue;
                }

                string report;
                try
                {
                    report = job.GetReport(pawn);
                }
                catch (Exception exception)
                {
                    Log.ErrorOnce("[Pawn Task Display] Failed to obtain a job report for " + pawn.LabelShort + ": " + exception, pawn.thingIDNumber ^ 0x51A7D);
                    continue;
                }

                if (string.IsNullOrEmpty(report))
                {
                    continue;
                }

                string label = BuildLabel(report, pawn, job, settings);
                if (string.IsNullOrEmpty(label))
                {
                    continue;
                }

                PawnLabelEntry entry;
                if (!entriesByPawn.TryGetValue(pawn, out entry))
                {
                    entry = new PawnLabelEntry { Pawn = pawn };
                    entriesByPawn.Add(pawn, entry);
                }

                entry.Label = label;
                renderEntries.Add(entry);
                seenPawns.Add(pawn);
            }

            stalePawns.Clear();
            foreach (KeyValuePair<Pawn, PawnLabelEntry> pair in entriesByPawn)
            {
                if (!seenPawns.Contains(pair.Key))
                {
                    stalePawns.Add(pair.Key);
                }
            }
            for (int i = 0; i < stalePawns.Count; i++)
            {
                entriesByPawn.Remove(stalePawns[i]);
            }
        }

        private static string BuildLabel(string report, Pawn pawn, Job job, PawnTaskDisplaySettings settings)
        {
            string task = ApplyTextCase(report.Trim(), settings.TextCase);
            string format = string.IsNullOrEmpty(settings.LabelFormat) ? "{task}" : settings.LabelFormat;
            string label = format
                .Replace("{task}", task)
                .Replace("{pawn}", pawn.LabelShort ?? string.Empty)
                .Replace("{job}", job.def.defName ?? string.Empty);

            label = label.Replace("\\n", "\n");
            if (settings.LimitLength && label.Length > settings.MaximumCharacters)
            {
                int keep = Math.Max(0, settings.MaximumCharacters - 1);
                label = label.Substring(0, keep) + "…";
            }
            return label;
        }

        private static string ApplyTextCase(string value, TaskLabelTextCase textCase)
        {
            switch (textCase)
            {
                case TaskLabelTextCase.Sentence:
                    if (value.Length == 0)
                    {
                        return value;
                    }
                    return char.ToUpper(value[0], CultureInfo.CurrentCulture) + value.Substring(1);
                case TaskLabelTextCase.Upper:
                    return value.ToUpper(CultureInfo.CurrentCulture);
                case TaskLabelTextCase.Lower:
                    return value.ToLower(CultureInfo.CurrentCulture);
                default:
                    return value;
            }
        }

        private void DrawLabel(Pawn pawn, string label, PawnTaskDisplaySettings settings)
        {
            Vector2 origin = GenMapUI.LabelDrawPosFor(pawn, -0.6f);
            labelContent.text = label;
            Vector2 size = labelStyle.CalcSize(labelContent);
            size.x = Mathf.Ceil(size.x) + 4f;
            size.y = Mathf.Ceil(size.y) + 2f;

            Rect rect = CalculateRect(origin, size, settings);
            if (settings.DrawShadow)
            {
                Rect shadowRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height);
                GUI.Label(shadowRect, labelContent, shadowStyle);
            }
            GUI.Label(rect, labelContent, labelStyle);
        }

        private static Rect CalculateRect(Vector2 origin, Vector2 size, PawnTaskDisplaySettings settings)
        {
            float x;
            float y;
            switch (settings.Position)
            {
                case TaskLabelPosition.AboveName:
                    x = origin.x - size.x * 0.5f;
                    y = origin.y - size.y - 4f;
                    break;
                case TaskLabelPosition.LeftOfPawn:
                    x = origin.x - size.x - 34f;
                    y = origin.y;
                    break;
                case TaskLabelPosition.RightOfPawn:
                    x = origin.x + 34f;
                    y = origin.y;
                    break;
                case TaskLabelPosition.Custom:
                    x = origin.x - size.x * 0.5f;
                    y = origin.y;
                    break;
                default:
                    x = origin.x - size.x * 0.5f;
                    y = origin.y + 15f;
                    break;
            }

            return new Rect(x + settings.OffsetX, y + settings.OffsetY, size.x, size.y);
        }

        private void EnsureStyles(PawnTaskDisplaySettings settings)
        {
            if (labelStyle != null && styleRevision == PawnTaskDisplayMod.SettingsRevision)
            {
                return;
            }

            GameFont previousFont = Text.Font;
            Text.Font = GameFont.Tiny;
            labelStyle = new GUIStyle(Text.CurFontStyle);
            Text.Font = previousFont;

            labelStyle.fontSize = settings.FontSize;
            labelStyle.fontStyle = settings.FontStyle;
            labelStyle.alignment = TextAnchor.UpperCenter;
            labelStyle.wordWrap = false;
            labelStyle.clipping = TextClipping.Overflow;
            labelStyle.normal.textColor = settings.LabelColor;

            shadowStyle = new GUIStyle(labelStyle);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, settings.LabelColor.a * 0.8f);
            styleRevision = PawnTaskDisplayMod.SettingsRevision;
        }

        private static bool IsHoldConditionMet(PawnTaskDisplaySettings settings)
        {
            return !settings.HoldKeyToShow || (settings.HoldKey != KeyCode.None && Input.GetKey(settings.HoldKey));
        }

        private void DisableAndClear()
        {
            wasDisplayEligible = false;
            if (entriesByPawn.Count == 0)
            {
                return;
            }
            entriesByPawn.Clear();
            renderEntries.Clear();
            seenPawns.Clear();
            stalePawns.Clear();
        }
    }
}
