using System;
using System.Collections.Generic;
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
        private const float SettingsHeight = 2300f;
        private static readonly Dictionary<string, string> hexBuffers = new Dictionary<string, string>();

        public PawnTaskDisplayMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<PawnTaskDisplaySettings>();
            Settings.Sanitize();
        }

        public override string SettingsCategory()
        {
            return "PTD_ModName".Translate();
        }

        private static string T(string key, string fallback)
        {
            return key.CanTranslate() ? key.Translate().ToString() : fallback;
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

            Section(listing, T("PTD_WhoSection", "Wer bekommt ein Label"));
            listing.CheckboxLabeled(T("PTD_Colonists", "Eigene Siedler"), ref Settings.ShowColonists);
            listing.CheckboxLabeled(T("PTD_Animals", "Eigene Tiere"), ref Settings.ShowAnimals);
            listing.CheckboxLabeled(T("PTD_Visitors", "Besucher"), ref Settings.ShowVisitors);
            listing.CheckboxLabeled(T("PTD_Slaves", "Sklaven"), ref Settings.ShowSlaves);
            listing.CheckboxLabeled(T("PTD_Prisoners", "Gefangene"), ref Settings.ShowPrisoners);

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
            Settings.LabelColor = ColorEditor(listing, "label", T("PTD_Color", "Textfarbe"), Settings.LabelColor);
            listing.CheckboxLabeled(T("PTD_UseForced", "Andere Farbe bei erzwungener Tätigkeit"), ref Settings.UseForcedColor);
            if (Settings.UseForcedColor)
            {
                Settings.ForcedColor = ColorEditor(listing, "forced", T("PTD_ForcedColor", "Farbe (erzwungen)"), Settings.ForcedColor);
            }

            Section(listing, "PTD_FormatSection".Translate());
            listing.Label("PTD_FormatHelp".Translate());
            Settings.LabelFormat = TextFieldRow(listing, "PTD_Format".Translate(), Settings.LabelFormat);
            listing.CheckboxLabeled("PTD_LimitLength".Translate(), ref Settings.LimitLength, "PTD_LimitLengthTip".Translate());
            int maximumCharacters = Settings.MaximumCharacters;
            IntSlider(listing, "PTD_MaxCharacters".Translate(), ref maximumCharacters, 1, 200, string.Empty);
            Settings.MaximumCharacters = maximumCharacters;
            listing.CheckboxLabeled(T("PTD_Wrap", "Zeilenumbruch (nur ganze Wörter)"), ref Settings.WrapLines);
            if (Settings.WrapLines)
            {
                int wrapChars = Settings.WrapCharacters;
                IntSlider(listing, T("PTD_WrapChars", "Umbruch nach Zeichen"), ref wrapChars, 5, 100, string.Empty);
                Settings.WrapCharacters = wrapChars;
                int maxLines = Settings.MaxLines;
                IntSlider(listing, T("PTD_MaxLines", "Maximale Zeilen"), ref maxLines, 1, 3, string.Empty);
                Settings.MaxLines = maxLines;
            }

            Section(listing, T("PTD_ZoomSection", "Zoom"));
            EnumButton(listing, T("PTD_ZoomMode", "Zoom-Modus"), Settings.ZoomMode, value => Settings.ZoomMode = value);
            if (Settings.ZoomMode != TaskLabelZoomMode.Off)
            {
                EnumButton(listing, T("PTD_ZoomLevel", "Zoomstufe"), Settings.ZoomLevel, value => Settings.ZoomLevel = value);
            }

            Section(listing, T("PTD_TimingSection", "Zeit und Maus"));
            listing.CheckboxLabeled(T("PTD_AutoHide", "Nach Zeit automatisch ausblenden"), ref Settings.AutoHide);
            if (Settings.AutoHide)
            {
                Slider(listing, T("PTD_AutoHideSec", "Ausblenden nach"), ref Settings.AutoHideSeconds, 1f, 20f, "0.0 s");
            }
            listing.CheckboxLabeled(T("PTD_HoverOnly", "Nur bei Mausover anzeigen"), ref Settings.HoverOnly);
            if (Settings.HoverOnly)
            {
                Slider(listing, T("PTD_HoverSec", "Anzeigedauer"), ref Settings.HoverSeconds, 1f, 20f, "0.0 s");
            }
            listing.CheckboxLabeled(T("PTD_ExpandHover", "Gekürztes Label bei Mausover ungekürzt zeigen"), ref Settings.ExpandOnHover);

            Section(listing, "PTD_KeySection".Translate());
            listing.CheckboxLabeled("PTD_HoldMode".Translate(), ref Settings.HoldKeyToShow, "PTD_HoldModeTip".Translate());
            DrawKeyCapture(listing);

            listing.GapLine();
            if (listing.ButtonText("PTD_Reset".Translate()))
            {
                Settings.ResetToDefaults();
                hexBuffers.Clear();
                waitingForKey = false;
                NotifySettingsChanged();
            }

            listing.End();
            Widgets.EndScrollView();
            Settings.Sanitize();
            NotifySettingsChanged();
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

        private static Color ColorEditor(Listing_Standard listing, string id, string label, Color current)
        {
            listing.Label(label);

            Rect preview = listing.GetRect(34f);
            Rect dark = new Rect(preview.x, preview.y, preview.width / 2f, preview.height);
            Rect light = new Rect(dark.xMax, preview.y, preview.width / 2f, preview.height);
            Widgets.DrawBoxSolid(dark, new Color(0.12f, 0.14f, 0.12f));
            Widgets.DrawBoxSolid(light, new Color(0.75f, 0.72f, 0.65f));
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = current;
            Widgets.Label(dark, T("PTD_Sample", "Beispiel: Holz hacken"));
            Widgets.Label(light, T("PTD_Sample", "Beispiel: Holz hacken"));
            GUI.color = oldColor;
            Text.Anchor = oldAnchor;
            listing.Gap(4f);

            float red = current.r;
            float green = current.g;
            float blue = current.b;
            float alpha = current.a;
            ColorSlider(listing, T("PTD_ColorRed", "Rot"), ref red);
            ColorSlider(listing, T("PTD_ColorGreen", "Grün"), ref green);
            ColorSlider(listing, T("PTD_ColorBlue", "Blau"), ref blue);
            ColorSlider(listing, T("PTD_ColorAlpha", "Deckkraft"), ref alpha);
            Color result = new Color(red, green, blue, alpha);

            string buffer;
            if (!hexBuffers.TryGetValue(id, out buffer) || result != current)
            {
                buffer = ColorUtility.ToHtmlStringRGB(result);
            }
            string edited = TextFieldRow(listing, "Hex (#RRGGBB)", buffer);
            if (edited != buffer)
            {
                buffer = edited;
                Color parsed;
                if (ColorUtility.TryParseHtmlString("#" + edited.TrimStart('#'), out parsed))
                {
                    result = new Color(parsed.r, parsed.g, parsed.b, alpha);
                }
            }
            hexBuffers[id] = buffer;
            listing.Gap(6f);
            return result;
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

        private static void EnumButton<T2>(Listing_Standard listing, string label, T2 current, Action<T2> setter) where T2 : struct
        {
            Rect row = listing.GetRect(30f);
            float labelWidth = row.width * 0.55f;
            Widgets.Label(new Rect(row.x, row.y, labelWidth, row.height), label);
            Rect buttonRect = new Rect(row.x + labelWidth, row.y, row.width - labelWidth, row.height);
            if (!Widgets.ButtonText(buttonRect, EnumLabel(current)))
            {
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (T2 value in Enum.GetValues(typeof(T2)))
            {
                T2 captured = value;
                options.Add(new FloatMenuOption(EnumLabel(captured), () => setter(captured)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string EnumLabel<T2>(T2 value) where T2 : struct
        {
            string key = "PTD_Enum_" + value;
            return key.CanTranslate() ? key.Translate().ToString() : value.ToString();
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
