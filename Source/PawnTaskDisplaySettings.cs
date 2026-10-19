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
            LabelColor.r = Mathf.Clamp01(LabelColor.r);
            LabelColor.g = Mathf.Clamp01(LabelColor.g);
            LabelColor.b = Mathf.Clamp01(LabelColor.b);
            LabelColor.a = Mathf.Clamp01(LabelColor.a);
            if (string.IsNullOrEmpty(LabelFormat))
            {
                LabelFormat = "{task}";
            }
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
        }
    }
}
