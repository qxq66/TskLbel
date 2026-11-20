using UnityEngine;
using Verse;

namespace PawnTaskDisplay
{
    public enum TaskLabelPosition
    {
        BelowName,
        AboveName,
        LeftOfPawn,
        RightOfPawn,
        Custom
    }

    public enum TaskLabelTextCase
    {
        Unchanged,
        Sentence,
        Upper,
        Lower
    }

    public enum TaskLabelZoomMode
    {
        Off,
        HideFromZoom,
        ShowFromZoom
    }

    public sealed class PawnTaskDisplaySettings : ModSettings
    {
        public bool ShowTaskLabels = true;
        public float RefreshIntervalSeconds = 0.5f;
        public TaskLabelPosition Position = TaskLabelPosition.BelowName;
        public float OffsetX;
        public float OffsetY;
        public int FontSize = 12;
        public FontStyle FontStyle = FontStyle.Normal;
        public Color LabelColor = Color.white;
        public bool DrawShadow = true;
        public string LabelFormat = "{task}";
        public TaskLabelTextCase TextCase = TaskLabelTextCase.Sentence;
        public bool LimitLength = true;
        public int MaximumCharacters = 60;
        public bool HoldKeyToShow;
        public KeyCode HoldKey = KeyCode.LeftShift;

        public TaskLabelZoomMode ZoomMode = TaskLabelZoomMode.Off;
        public CameraZoomRange ZoomLevel = CameraZoomRange.Middle;
        public bool AutoHide;
        public float AutoHideSeconds = 5f;
        public bool WrapLines;
        public int WrapCharacters = 25;
        public int MaxLines = 2;
        public bool HoverOnly;
        public float HoverSeconds = 3f;
        public bool ExpandOnHover;
        public bool UseForcedColor = true;
        public Color ForcedColor = new Color(1f, 0.8f, 0.2f, 1f);
        public bool ShowColonists = true;
        public bool ShowAnimals;
        public bool ShowVisitors;
        public bool ShowSlaves;
        public bool ShowPrisoners;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShowTaskLabels, "showTaskLabels", true);
            Scribe_Values.Look(ref RefreshIntervalSeconds, "refreshIntervalSeconds", 0.5f);
            Scribe_Values.Look(ref Position, "position", TaskLabelPosition.BelowName);
            Scribe_Values.Look(ref OffsetX, "offsetX", 0f);
            Scribe_Values.Look(ref OffsetY, "offsetY", 0f);
            Scribe_Values.Look(ref FontSize, "fontSize", 12);
            Scribe_Values.Look(ref FontStyle, "fontStyle", FontStyle.Normal);
            Scribe_Values.Look(ref LabelColor, "labelColor", Color.white);
            Scribe_Values.Look(ref DrawShadow, "drawShadow", true);
            Scribe_Values.Look(ref LabelFormat, "labelFormat", "{task}");
            Scribe_Values.Look(ref TextCase, "textCase", TaskLabelTextCase.Sentence);
            Scribe_Values.Look(ref LimitLength, "limitLength", true);
            Scribe_Values.Look(ref MaximumCharacters, "maximumCharacters", 60);
            Scribe_Values.Look(ref HoldKeyToShow, "holdKeyToShow", false);
            Scribe_Values.Look(ref HoldKey, "holdKey", KeyCode.LeftShift);

            Scribe_Values.Look(ref ZoomMode, "zoomMode", TaskLabelZoomMode.Off);
            Scribe_Values.Look(ref ZoomLevel, "zoomLevel", CameraZoomRange.Middle);
            Scribe_Values.Look(ref AutoHide, "autoHide", false);
            Scribe_Values.Look(ref AutoHideSeconds, "autoHideSeconds", 5f);
            Scribe_Values.Look(ref WrapLines, "wrapLines", false);
            Scribe_Values.Look(ref WrapCharacters, "wrapCharacters", 25);
            Scribe_Values.Look(ref MaxLines, "maxLines", 2);
            Scribe_Values.Look(ref HoverOnly, "hoverOnly", false);
            Scribe_Values.Look(ref HoverSeconds, "hoverSeconds", 3f);
            Scribe_Values.Look(ref ExpandOnHover, "expandOnHover", false);
            Scribe_Values.Look(ref UseForcedColor, "useForcedColor", true);
            Scribe_Values.Look(ref ForcedColor, "forcedColor", new Color(1f, 0.8f, 0.2f, 1f));
            Scribe_Values.Look(ref ShowColonists, "showColonists", true);
            Scribe_Values.Look(ref ShowAnimals, "showAnimals", false);
            Scribe_Values.Look(ref ShowVisitors, "showVisitors", false);
            Scribe_Values.Look(ref ShowSlaves, "showSlaves", false);
            Scribe_Values.Look(ref ShowPrisoners, "showPrisoners", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Sanitize();
            }
        }

        public void Sanitize()
        {
            RefreshIntervalSeconds = Mathf.Clamp(RefreshIntervalSeconds, 0.1f, 3f);
            OffsetX = Mathf.Clamp(OffsetX, -200f, 200f);
            OffsetY = Mathf.Clamp(OffsetY, -200f, 200f);
            FontSize = Mathf.Clamp(FontSize, 6, 32);
            MaximumCharacters = Mathf.Clamp(MaximumCharacters, 1, 200);
            LabelColor = Clamp(LabelColor);
            ForcedColor = Clamp(ForcedColor);
            AutoHideSeconds = Mathf.Clamp(AutoHideSeconds, 1f, 20f);
            HoverSeconds = Mathf.Clamp(HoverSeconds, 1f, 20f);
            WrapCharacters = Mathf.Clamp(WrapCharacters, 5, 100);
            MaxLines = Mathf.Clamp(MaxLines, 1, 3);
            if (string.IsNullOrEmpty(LabelFormat))
            {
                LabelFormat = "{task}";
            }
        }

        private static Color Clamp(Color c)
        {
            return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
        }

        public void ResetToDefaults()
        {
            ShowTaskLabels = true;
            RefreshIntervalSeconds = 0.5f;
            Position = TaskLabelPosition.BelowName;
            OffsetX = 0f;
            OffsetY = 0f;
            FontSize = 12;
            FontStyle = FontStyle.Normal;
            LabelColor = Color.white;
            DrawShadow = true;
            LabelFormat = "{task}";
            TextCase = TaskLabelTextCase.Sentence;
            LimitLength = true;
            MaximumCharacters = 60;
            HoldKeyToShow = false;
            HoldKey = KeyCode.LeftShift;
            ZoomMode = TaskLabelZoomMode.Off;
            ZoomLevel = CameraZoomRange.Middle;
            AutoHide = false;
            AutoHideSeconds = 5f;
            WrapLines = false;
            WrapCharacters = 25;
            MaxLines = 2;
            HoverOnly = false;
            HoverSeconds = 3f;
            ExpandOnHover = false;
            UseForcedColor = true;
            ForcedColor = new Color(1f, 0.8f, 0.2f, 1f);
            ShowColonists = true;
            ShowAnimals = false;
            ShowVisitors = false;
            ShowSlaves = false;
            ShowPrisoners = false;
        }
    }
}
