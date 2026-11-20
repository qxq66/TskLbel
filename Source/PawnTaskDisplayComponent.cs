using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
            public string ShortLabel;
            public string FullLabel;
            public bool Forced;
            public int JobId = -1;
            public float JobStart;
            public float LastHover = -999f;
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

            if (!ZoomAllows(settings))
            {
                return;
            }

            EnsureStyles(settings);
            CellRect viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(1);
            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < renderEntries.Count; i++)
            {
                PawnLabelEntry entry = renderEntries[i];
                Pawn pawn = entry.Pawn;
                if (pawn == null || !pawn.Spawned || pawn.Map != map || !viewRect.Contains(pawn.Position))
                {
                    continue;
                }

                bool hovered = (settings.HoverOnly || settings.ExpandOnHover) && IsHovered(pawn);
                if (hovered)
                {
                    entry.LastHover = now;
                }

                if (settings.HoverOnly)
                {
                    if (now - entry.LastHover > settings.HoverSeconds)
                    {
                        continue;
                    }
                }
                else if (settings.AutoHide && now - entry.JobStart > settings.AutoHideSeconds)
                {
                    continue;
                }

                string text = (settings.ExpandOnHover && hovered) ? entry.FullLabel : entry.ShortLabel;
                Color color = (settings.UseForcedColor && entry.Forced) ? settings.ForcedColor : settings.LabelColor;
                DrawLabel(pawn, text, color, settings);
            }
        }

        private static bool ShouldShow(Pawn pawn, PawnTaskDisplaySettings settings)
        {
            if (pawn == null || pawn.RaceProps == null)
            {
                return false;
            }

            Faction player = Faction.OfPlayer;
            if (pawn.RaceProps.Animal)
            {
                return settings.ShowAnimals && pawn.Faction == player;
            }
            if (!pawn.RaceProps.Humanlike)
            {
                return false;
            }
            if (pawn.IsPrisonerOfColony)
            {
                return settings.ShowPrisoners;
            }
            if (pawn.Faction == player)
            {
                return pawn.IsSlave ? settings.ShowSlaves : settings.ShowColonists;
            }
            return settings.ShowVisitors && pawn.Faction != null && !pawn.HostileTo(player) &&
                pawn.HostFaction == null && !pawn.IsPrisoner;
        }

        private static bool ZoomAllows(PawnTaskDisplaySettings settings)
        {
            if (settings.ZoomMode == TaskLabelZoomMode.Off)
            {
                return true;
            }
            bool reached = Find.CameraDriver.CurrentZoom >= settings.ZoomLevel;
            return settings.ZoomMode == TaskLabelZoomMode.HideFromZoom ? !reached : reached;
        }

        private static bool IsHovered(Pawn pawn)
        {
            Vector3 mouse = UI.MouseMapPosition();
            Vector3 draw = pawn.DrawPos;
            return Mathf.Abs(mouse.x - draw.x) < 0.6f && Mathf.Abs(mouse.z - draw.z) < 0.6f;
        }

        private void RefreshCache(PawnTaskDisplaySettings settings)
        {
            seenPawns.Clear();
            renderEntries.Clear();

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!ShouldShow(pawn, settings))
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

                PawnLabelEntry entry;
                if (!entriesByPawn.TryGetValue(pawn, out entry))
                {
                    entry = new PawnLabelEntry { Pawn = pawn };
                    entriesByPawn.Add(pawn, entry);
                }

                if (entry.JobId != job.loadID)
                {
                    entry.JobId = job.loadID;
                    entry.JobStart = Time.realtimeSinceStartup;
                }
                entry.Forced = job.playerForced;

                BuildLabels(entry, report, pawn, job, settings);
                if (string.IsNullOrEmpty(entry.ShortLabel))
                {
                    continue;
                }

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

        private static void BuildLabels(PawnLabelEntry entry, string report, Pawn pawn, Job job, PawnTaskDisplaySettings settings)
        {
            string task = ApplyTextCase(report.Trim(), settings.TextCase);
            string format = string.IsNullOrEmpty(settings.LabelFormat) ? "{task}" : settings.LabelFormat;
            string full = format
                .Replace("{task}", task)
                .Replace("{pawn}", pawn.LabelShort ?? string.Empty)
                .Replace("{job}", job.def.defName ?? string.Empty);
            full = full.Replace("\\n", "\n");

            string shortText = full;
            if (settings.LimitLength && shortText.Length > settings.MaximumCharacters)
            {
                int keep = Math.Max(0, settings.MaximumCharacters - 1);
                shortText = shortText.Substring(0, keep) + "…";
            }

            if (settings.WrapLines)
            {
                entry.ShortLabel = Wrap(shortText, settings.WrapCharacters, settings.MaxLines);
                entry.FullLabel = Wrap(full, settings.WrapCharacters, int.MaxValue);
            }
            else
            {
                entry.ShortLabel = shortText;
                entry.FullLabel = full;
            }
        }

        private static string Wrap(string text, int width, int maxLines)
        {
            List<string> lines = new List<string>();
            string[] paragraphs = text.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                StringBuilder line = new StringBuilder();
                string[] words = paragraphs[p].Split(' ');
                for (int w = 0; w < words.Length; w++)
                {
                    string word = words[w];
                    if (word.Length == 0)
                    {
                        continue;
                    }
                    if (line.Length == 0)
                    {
                        line.Append(word);
                    }
                    else if (line.Length + 1 + word.Length <= width)
                    {
                        line.Append(' ').Append(word);
                    }
                    else
                    {
                        lines.Add(line.ToString());
                        line.Length = 0;
                        line.Append(word);
                    }
                }
                if (line.Length > 0)
                {
                    lines.Add(line.ToString());
                }
            }

            if (lines.Count > maxLines)
            {
                lines = lines.GetRange(0, maxLines);
                string last = lines[maxLines - 1].TrimEnd('…', ' ');
                lines[maxLines - 1] = last + "…";
            }
            return string.Join("\n", lines.ToArray());
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

        private void DrawLabel(Pawn pawn, string label, Color color, PawnTaskDisplaySettings settings)
        {
            Vector2 origin = GenMapUI.LabelDrawPosFor(pawn, -0.6f);
            labelContent.text = label;
            Vector2 size = labelStyle.CalcSize(labelContent);
            size.x = Mathf.Ceil(size.x) + 4f;
            size.y = Mathf.Ceil(size.y) + 2f;

            Rect rect = CalculateRect(origin, size, settings);
            if (settings.DrawShadow)
            {
                shadowStyle.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.8f);
                Rect shadowRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height);
                GUI.Label(shadowRect, labelContent, shadowStyle);
            }
            labelStyle.normal.textColor = color;
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
