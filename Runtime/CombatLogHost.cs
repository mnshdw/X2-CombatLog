using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Xenonauts.Screens.LoadingScreen;

namespace CombatLog.Runtime
{
    [DefaultExecutionOrder(int.MinValue)]
    public class CombatLogHost : MonoBehaviour
    {
        // Hard cap on lines kept in memory.
        private const int MaxLines = 500;

        // Layout. Anchored at the very bottom-left and kept narrow so it occupies the empty
        // strip to the left of the throwable/weapon panels at common resolutions. The user can
        // see longer lines in the expanded view.
        private const float WidthFraction = 0.20f;
        private const float MinWidth = 320f;
        private const float MaxWidth = 380f;
        private const float ExpandedHeightFraction = 0.14f;
        private const float MinExpandedHeight = 100f;
        private const float MaxExpandedHeight = 220f;
        private const int CollapsedLines = 1;
        private const int LineHeight = 20;
        private const int FontSize = 13;
        private const int ArrowFontSize = 18;
        private const int ArrowButtonWidth = 26;
        private const int Border = 1;
        private const int Padding = 4;
        private const int ScreenMargin = 8;

        private readonly List<CombatEntry> _entries = new();
        private bool _expanded;
        private Vector2 _scroll;
        private bool _followBottom = true;

        private GUIStyle? _lineStyle;          // wraps; used in expanded view
        private GUIStyle? _collapsedLineStyle; // single-line clipped; used in collapsed view
        private GUIStyle? _arrowStyle;
        private GUIStyle? _panelStyle;
        private readonly GUIContent _calcContent = new();
        private static Texture2D? _panelTex;
        private static Texture2D? _borderTex;

        private LoadingScreenBehavior? _loadingScreen;
        private float _nextLoadingScreenLookupAt;
        private const float LoadingScreenLookupCooldown = 2f;

        private void Update()
        {
            while (CombatLogFeed.TryDequeue(out var entry))
            {
                if (TryMergeIntoLast(entry))
                    continue;
                _entries.Add(entry);
                while (_entries.Count > MaxLines)
                    _entries.RemoveAt(0);
                if (_followBottom)
                    _scroll.y = float.MaxValue;
            }
        }

        // When mergeKey matches the most recent entry, the system has rebuilt the full text for
        // the same logical action (e.g. shot impact) - replace the previous text rather than
        // append, so each shot stays on a single line.
        private bool TryMergeIntoLast(CombatEntry incoming)
        {
            if (incoming.MergeKey == null || _entries.Count == 0)
                return false;
            var last = _entries[_entries.Count - 1];
            if (!ReferenceEquals(last.MergeKey, incoming.MergeKey))
                return false;
            last.Text = incoming.Text;
            return true;
        }

        private void OnGUI()
        {
            if (!CombatLogFeed.InGroundCombat)
                return;
            if (IsLoadingScreenActive())
                return;

            EnsureStyles();

            var screenW = Screen.width;
            var screenH = Screen.height;

            var width = Mathf.Clamp(screenW * WidthFraction, MinWidth, MaxWidth);
            var collapsedHeight = LineHeight * CollapsedLines + Padding * 2 + Border * 2;
            var expandedHeight = Mathf.Clamp(
                screenH * ExpandedHeightFraction,
                MinExpandedHeight,
                MaxExpandedHeight
            );
            var height = _expanded ? expandedHeight : collapsedHeight;

            var x = ScreenMargin;
            var y = screenH - height - ScreenMargin;
            var panel = new Rect(x, y, width, height);
            CombatLogState.LastPanelRect = panel;

            DrawPanelBackground(panel);

            var arrowRect = new Rect(
                panel.xMax - Border - ArrowButtonWidth,
                panel.y + Border,
                ArrowButtonWidth,
                panel.height - Border * 2
            );
            var contentRect = new Rect(
                panel.x + Border + Padding,
                panel.y + Border + Padding,
                panel.width - Border * 2 - Padding * 2 - ArrowButtonWidth,
                panel.height - Border * 2 - Padding * 2
            );

            // Order matters: the arrow button and any scroll view inside the content draw
            // first so they get first dibs on the click/scroll events. ConsumePanelMouseEvents
            // then mops up anything left inside the panel rect that the controls didn't claim
            // - it skips already-Used events so it never steals from a button.
            DrawArrowButton(arrowRect);
            if (_expanded)
                DrawExpandedContent(contentRect);
            else
                DrawCollapsedContent(contentRect);
            ConsumePanelMouseEvents(panel);
        }

        private void DrawPanelBackground(Rect panel)
        {
            // Background fill.
            GUI.DrawTexture(panel, _panelTex, ScaleMode.StretchToFill, alphaBlend: true);
            var b = (float)Border;
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, b), _borderTex);
            GUI.DrawTexture(new Rect(panel.x, panel.yMax - b, panel.width, b), _borderTex);
            GUI.DrawTexture(new Rect(panel.x, panel.y, b, panel.height), _borderTex);
            GUI.DrawTexture(new Rect(panel.xMax - b, panel.y, b, panel.height), _borderTex);
            // Vertical separator before the arrow button.
            GUI.DrawTexture(
                new Rect(panel.xMax - b - ArrowButtonWidth, panel.y, b, panel.height),
                _borderTex
            );
        }

        private void DrawCollapsedContent(Rect rect)
        {
            if (_entries.Count == 0)
                return;
            // Collapsed view shows only the most recent entry on a single clipped line. Long
            // text gets cut off here on purpose; the expand button reveals the full content.
            var last = _entries[_entries.Count - 1];
            GUI.Label(rect, last.Text, _collapsedLineStyle);
        }

        private void DrawExpandedContent(Rect rect)
        {
            // Expanded view word-wraps long lines. Each entry's height is computed against
            // the available content width so wrapped entries push later ones downward.
            var innerWidth = rect.width - 16; // leave room for the vertical scrollbar
            float contentH = 0f;
            foreach (var entry in _entries)
                contentH += HeightFor(entry.Text, innerWidth);

            var maxScroll = Mathf.Max(0, contentH - rect.height);
            if (_followBottom)
                _scroll.y = maxScroll;

            var viewRect = new Rect(0, 0, innerWidth, contentH);
            _scroll = GUI.BeginScrollView(rect, _scroll, viewRect);
            var lineY = 0f;
            foreach (var entry in _entries)
            {
                var h = HeightFor(entry.Text, innerWidth);
                GUI.Label(new Rect(0, lineY, innerWidth, h), entry.Text, _lineStyle);
                lineY += h;
            }
            GUI.EndScrollView();

            _followBottom = _scroll.y >= maxScroll - 1f;
        }

        private float HeightFor(string text, float width)
        {
            if (_lineStyle == null)
                return LineHeight;
            _calcContent.text = text;
            return Mathf.Max(LineHeight, _lineStyle.CalcHeight(_calcContent, width));
        }

        private void DrawArrowButton(Rect rect)
        {
            var label = _expanded ? "▼" : "▲";
            if (GUI.Button(rect, label, _arrowStyle))
            {
                _expanded = !_expanded;
                if (_expanded)
                {
                    // Reset to bottom on expand so the user lands on the latest entry.
                    _scroll.y = float.MaxValue;
                    _followBottom = true;
                }
            }
        }

        private static void ConsumePanelMouseEvents(Rect panel)
        {
            var ev = Event.current;
            if (ev == null)
                return;
            switch (ev.type)
            {
                case EventType.MouseDown:
                case EventType.MouseUp:
                case EventType.MouseDrag:
                case EventType.ScrollWheel:
                    if (panel.Contains(ev.mousePosition))
                        ev.Use();
                    break;
            }
        }

        private void EnsureStyles()
        {
            if (_lineStyle != null)
                return;

            _panelTex ??= MakeTex(new Color(0.05f, 0.05f, 0.05f, 0.72f));
            _borderTex ??= MakeTex(new Color(0.45f, 0.45f, 0.45f, 1f));

            Font? gameFont = null;
            try
            {
                var fonts = Resources.FindObjectsOfTypeAll<Font>();
                gameFont =
                    fonts.FirstOrDefault(f => f.name == "LegacyRuntime")
                    ?? fonts.FirstOrDefault(f => f.dynamic)
                    ?? fonts.FirstOrDefault();
            }
            catch { }

            var textColor = new Color(0.95f, 0.95f, 0.95f);
            _lineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = FontSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true, // expanded view wraps long lines onto extra rows
                richText = true,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(0, 0, 0, 0),
            };
            _lineStyle.normal.textColor = textColor;
            _collapsedLineStyle = new GUIStyle(_lineStyle)
            {
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            _collapsedLineStyle.normal.textColor = textColor;

            _arrowStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = ArrowFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
            };
            _arrowStyle.normal.background = null;
            _arrowStyle.hover.background = _borderTex;
            _arrowStyle.active.background = _borderTex;
            _arrowStyle.normal.textColor = textColor;
            _arrowStyle.hover.textColor = textColor;
            _arrowStyle.active.textColor = textColor;

            _panelStyle = new GUIStyle();
            _panelStyle.normal.background = _panelTex;

            if (gameFont != null)
            {
                _lineStyle.font = gameFont;
                _collapsedLineStyle.font = gameFont;
                _arrowStyle.font = gameFont;
            }
        }

        private bool IsLoadingScreenActive()
        {
            if (_loadingScreen == null)
            {
                if (Time.unscaledTime < _nextLoadingScreenLookupAt)
                    return false;
                _nextLoadingScreenLookupAt = Time.unscaledTime + LoadingScreenLookupCooldown;
                foreach (var b in Resources.FindObjectsOfTypeAll<LoadingScreenBehavior>())
                {
                    if (b.gameObject.scene.IsValid())
                    {
                        _loadingScreen = b;
                        break;
                    }
                }
                if (_loadingScreen == null)
                    return false;
            }
            return _loadingScreen.isActiveAndEnabled;
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }
}
