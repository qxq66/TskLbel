using System;
using UnityEngine;
using Verse;

namespace PawnTaskDisplay
{
    public sealed class PawnTaskDisplayMod : Mod
    {
        public static PawnTaskDisplaySettings Settings { get; private set; }
        public static int SettingsRevision { get; private set; }

        private static Vector2 scrollPosition;
        private static bool waitingForKey;
        private const float SettingsHeight = 1120f;

        public PawnTaskDisplayMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<PawnTaskDisplaySettings>();
            Settings.Sanitize();
        }

        public override string SettingsCategory()
        {
            return "PTD_ModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect outRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);
            Rect viewRect = new Rect(0f, 0f, inRect.width - 18f, SettingsHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            Header(listing, "PTD_SettingsTitle".Translate());
            listing.CheckboxLabeled("PTD_Enable".Translate(), ref Settings.ShowTaskLabels, "PTD_EnableTip".Translate());
            Slider(listing, "PTD_Refresh".Translate(), ref Settings.RefreshIntervalSeconds, 0.1f, 3f, "0.0 s");

            Section(listing, "PTD_PositionSection".Translate());
            EnumButton(listing, "PTD_Position".Translate(), Settings.Position, value => Settings.Position = value);
            Slider(listing, "PTD_OffsetX".Translate(), ref Settings.OffsetX, -200f, 200f, "0 px");
            Slider(listing, "PTD_OffsetY".Translate(), ref Settings.OffsetY, -200f, 200f, "0 px");

            Section(listing, "PTD_AppearanceSection".Translate());
            int fontSize = Settings.FontSize;
            IntSlider(listing, "PTD_FontSize".Translate(), ref fontSize, 6, 32, " px");
            Settings.FontSize = fontSize;
            EnumButton(listing, "PTD_FontStyle".Translate(), Settings.FontStyle, value => Settings.FontStyle = value);
            EnumButton(listing, "PTD_TextCase".Translate(), Settings.TextCase, value => Settings.TextCase = value);
            listing.CheckboxLabeled("PTD_Shadow".Translate(), ref Settings.DrawShadow, "PTD_ShadowTip".Translate());
            Color labelColor = Settings.LabelColor;
            float red = labelColor.r;
            float green = labelColor.g;
            float blue = labelColor.b;
            float alpha = labelColor.a;
            ColorSlider(listing, "PTD_ColorRed".Translate(), ref red);
            ColorSlider(listing, "PTD_ColorGreen".Translate(), ref green);
            ColorSlider(listing, "PTD_ColorBlue".Translate(), ref blue);
            ColorSlider(listing, "PTD_ColorAlpha".Translate(), ref alpha);
            Settings.LabelColor = new Color(red, green, blue, alpha);

            Section(listing, "PTD_FormatSection".Translate());
            listing.Label("PTD_FormatHelp".Translate());
            Settings.LabelFormat = TextFieldRow(listing, "PTD_Format".Translate(), Settings.LabelFormat);
            listing.CheckboxLabeled("PTD_LimitLength".Translate(), ref Settings.LimitLength, "PTD_LimitLengthTip".Translate());
            int maximumCharacters = Settings.MaximumCharacters;
            IntSlider(listing, "PTD_MaxCharacters".Translate(), ref maximumCharacters, 1, 200, string.Empty);
            Settings.MaximumCharacters = maximumCharacters;

            Section(listing, "PTD_KeySection".Translate());
            listing.CheckboxLabeled("PTD_HoldMode".Translate(), ref Settings.HoldKeyToShow, "PTD_HoldModeTip".Translate());
            DrawKeyCapture(listing);

            listing.GapLine();
            if (listing.ButtonText("PTD_Reset".Translate()))
            {
                Settings.ResetToDefaults();
                waitingForKey = false;
                NotifySettingsChanged();
            }

            listing.End();
            Widgets.EndScrollView();
            Settings.Sanitize();
        }

        public override void WriteSettings()
        {
            Settings.Sanitize();
            NotifySettingsChanged();
            base.WriteSettings();
        }

        private static void NotifySettingsChanged()
        {
            unchecked
            {
                SettingsRevision++;
            }
        }

        private static void Header(Listing_Standard listing, string text)
        {
            GameFont previous = Text.Font;
            Text.Font = GameFont.Medium;
            listing.Label(text);
            Text.Font = previous;
            listing.GapLine();
        }

        private static void Section(Listing_Standard listing, string text)
        {
            listing.Gap(8f);
            listing.GapLine();
            GameFont previous = Text.Font;
            Text.Font = GameFont.Small;
            listing.Label(text);
            Text.Font = previous;
        }

        private static void Slider(Listing_Standard listing, string label, ref float value, float min, float max, string format)
        {
            listing.Label(label + ": " + value.ToString(format));
            value = Widgets.HorizontalSlider(listing.GetRect(24f), value, min, max, false);
            listing.Gap(3f);
        }

        private static void IntSlider(Listing_Standard listing, string label, ref int value, int min, int max, string suffix)
        {
            listing.Label(label + ": " + value + suffix);
            value = Mathf.RoundToInt(Widgets.HorizontalSlider(listing.GetRect(24f), value, min, max, false));
            listing.Gap(3f);
        }

        private static void ColorSlider(Listing_Standard listing, string label, ref float value)
        {
            listing.Label(label + ": " + Mathf.RoundToInt(value * 255f));
            value = Widgets.HorizontalSlider(listing.GetRect(22f), value, 0f, 1f, false);
        }

        private static string TextFieldRow(Listing_Standard listing, string label, string value)
        {
            Rect row = listing.GetRect(30f);
            float labelWidth = row.width * 0.35f;
            Widgets.Label(new Rect(row.x, row.y, labelWidth, row.height), label);
            return Widgets.TextField(new Rect(row.x + labelWidth, row.y, row.width - labelWidth, row.height), value ?? string.Empty);
        }

        private static void EnumButton<T>(Listing_Standard listing, string label, T current, Action<T> setter) where T : struct
        {
            Rect row = listing.GetRect(30f);
            float labelWidth = row.width * 0.55f;
            Widgets.Label(new Rect(row.x, row.y, labelWidth, row.height), label);
            Rect buttonRect = new Rect(row.x + labelWidth, row.y, row.width - labelWidth, row.height);
            if (!Widgets.ButtonText(buttonRect, EnumLabel(current)))
            {
                return;
            }

            System.Collections.Generic.List<FloatMenuOption> options = new System.Collections.Generic.List<FloatMenuOption>();
            foreach (T value in Enum.GetValues(typeof(T)))
            {
                T captured = value;
                options.Add(new FloatMenuOption(EnumLabel(captured), () => setter(captured)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string EnumLabel<T>(T value) where T : struct
        {
            string key = "PTD_Enum_" + value;
            TaggedString translated = key.Translate();
            return translated.ToString();
        }

        private static void DrawKeyCapture(Listing_Standard listing)
        {
            Rect row = listing.GetRect(32f);
            float labelWidth = row.width * 0.55f;
            Widgets.Label(new Rect(row.x, row.y, labelWidth, row.height), "PTD_HoldKey".Translate());
            Rect buttonRect = new Rect(row.x + labelWidth, row.y, row.width - labelWidth, row.height);
            string buttonText = waitingForKey ? "PTD_PressKey".Translate().ToString() : Settings.HoldKey.ToString();
            if (Widgets.ButtonText(buttonRect, buttonText))
            {
                waitingForKey = true;
            }

            Event current = Event.current;
            if (!waitingForKey || current == null || current.type != EventType.KeyDown)
            {
                return;
            }

            if (current.keyCode == KeyCode.Escape)
            {
                waitingForKey = false;
                current.Use();
                return;
            }

            if (current.keyCode != KeyCode.None)
            {
                Settings.HoldKey = current.keyCode;
                waitingForKey = false;
                current.Use();
            }
        }
    }
}
