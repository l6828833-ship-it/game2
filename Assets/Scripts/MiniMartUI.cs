using UnityEngine;
using UnityEngine.UI;

namespace MiniMart
{
    /// <summary>
    /// Runtime HUD built entirely in code: cash, the day clock, reputation, daily stats,
    /// a context prompt for whatever is in reach, plus pause and end-of-day overlays.
    /// </summary>
    public class MiniMartUI : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.09f, 0.13f, 0.18f, 0.74f);
        private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color Ink = new Color(0.97f, 0.98f, 1f);
        private static readonly Color Muted = new Color(0.76f, 0.83f, 0.9f);

        private MiniMartGameManager game;
        private Font font;

        private Text moneyText;
        private Text dayText;
        private Text carryText;
        private Text statsText;
        private Text hintText;
        private Text promptText;
        private Text notificationText;
        private Image promptBackground;
        private RectTransform clockFill;
        private RectTransform reputationFill;
        private Image reputationImage;
        private GameObject pausePanel;
        private GameObject summaryPanel;
        private Text summaryText;

        private float notificationUntil;
        private int cachedMoney = int.MinValue;
        private int cachedDay = int.MinValue;
        private int cachedMinute = int.MinValue;
        private int cachedServed = int.MinValue;
        private int cachedLost = int.MinValue;
        private int cachedRent = int.MinValue;
        private string cachedCarry = string.Empty;
        private string cachedHint = string.Empty;
        private string cachedPrompt = string.Empty;

        public static MiniMartUI Create(MiniMartGameManager manager)
        {
            GameObject root = new GameObject("MiniMart_UI");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            root.AddComponent<GraphicRaycaster>();

            MiniMartUI ui = root.AddComponent<MiniMartUI>();
            ui.game = manager;
            ui.Build();
            return ui;
        }

        // ------------------------------------------------------------------ layout

        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform status = Panel("Status_Card", transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -20f), new Vector2(330f, 152f), PanelColor);
            moneyText = Label("Money", status, new Vector2(18f, -12f), new Vector2(294f, 46f), 38, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            dayText = Label("Day", status, new Vector2(18f, -60f), new Vector2(294f, 24f), 19, TextAnchor.UpperLeft, Muted, FontStyle.Normal);
            clockFill = Track(status, new Vector2(18f, -88f), new Vector2(294f, 8f), new Color(1f, 0.83f, 0.32f));
            Label("Rep_Caption", status, new Vector2(18f, -102f), new Vector2(294f, 22f), 16, TextAnchor.UpperLeft, Muted, FontStyle.Normal)
                .text = "Reputation";
            reputationFill = Track(status, new Vector2(18f, -126f), new Vector2(294f, 10f), new Color(0.36f, 0.87f, 0.45f));
            reputationImage = reputationFill.GetComponent<Image>();

            carryText = Label("Carry", transform, new Vector2(24f, -184f), new Vector2(520f, 26f), 19, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            hintText = Label("Hint", transform, new Vector2(24f, -210f), new Vector2(560f, 26f), 17, TextAnchor.UpperLeft, new Color(1f, 0.72f, 0.36f), FontStyle.Normal);

            RectTransform stats = Panel("Stats_Card", transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-20f, -20f), new Vector2(250f, 104f), PanelColor);
            statsText = Label("Stats", stats, new Vector2(16f, -12f), new Vector2(220f, 84f), 18, TextAnchor.UpperLeft, Muted, FontStyle.Normal);

            RectTransform promptCard = Panel("Prompt_Card", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 118f), new Vector2(620f, 46f), PanelColor);
            promptBackground = promptCard.GetComponent<Image>();
            promptText = Label("Prompt", promptCard, Vector2.zero, new Vector2(600f, 40f), 21, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            CenterInParent(promptText.rectTransform, Vector2.zero);

            notificationText = Label("Notification", transform, new Vector2(0f, 78f), new Vector2(880f, 30f), 22, TextAnchor.LowerCenter, Ink, FontStyle.Normal);
            RectTransform noteRect = notificationText.rectTransform;
            noteRect.anchorMin = new Vector2(0.5f, 0f);
            noteRect.anchorMax = new Vector2(0.5f, 0f);
            noteRect.pivot = new Vector2(0.5f, 0f);
            noteRect.anchoredPosition = new Vector2(0f, 78f);

            Text controls = Label("Controls", transform, new Vector2(24f, 26f), new Vector2(720f, 24f), 17, TextAnchor.LowerLeft, Muted, FontStyle.Normal);
            RectTransform controlsRect = controls.rectTransform;
            controlsRect.anchorMin = Vector2.zero;
            controlsRect.anchorMax = Vector2.zero;
            controlsRect.pivot = Vector2.zero;
            controlsRect.anchoredPosition = new Vector2(24f, 24f);
            controls.text = "WASD move   Shift run   E interact   Q drop crate   Esc pause   F5 x2 reset save";

            BuildPauseOverlay();
            BuildSummaryOverlay();
        }

        private void BuildPauseOverlay()
        {
            GameObject overlay = new GameObject("Pause_Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(transform, false);
            RectTransform rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f, 0.6f);

            Text text = Label("Pause_Text", overlay.transform, Vector2.zero, new Vector2(560f, 90f), 34, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            CenterInParent(text.rectTransform, Vector2.zero);
            text.text = "Paused\nEsc to get back to work";

            pausePanel = overlay;
            pausePanel.SetActive(false);
        }

        private void BuildSummaryOverlay()
        {
            RectTransform card = Panel("Summary_Card", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(460f, 300f), new Color(0.07f, 0.11f, 0.16f, 0.92f));
            summaryText = Label("Summary_Text", card, Vector2.zero, new Vector2(410f, 260f), 21, TextAnchor.UpperLeft, Ink, FontStyle.Normal);
            RectTransform rect = summaryText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(26f, -22f);

            summaryPanel = card.gameObject;
            summaryPanel.SetActive(false);
        }

        // ----------------------------------------------------------------- refresh

        public void Refresh()
        {
            if (moneyText == null) return;

            if (game.Money != cachedMoney)
            {
                cachedMoney = game.Money;
                moneyText.text = "$ " + cachedMoney;
            }

            int minute = Mathf.FloorToInt(game.ClockHour * 60f / 5f) * 5;
            if (game.Day != cachedDay || minute != cachedMinute)
            {
                cachedDay = game.Day;
                cachedMinute = minute;
                dayText.text = game.Phase == DayPhase.Closing
                    ? "Day " + cachedDay + "   Closed for the night"
                    : "Day " + cachedDay + "   " + FormatClock(minute);
            }

            clockFill.sizeDelta = new Vector2(294f * (game.Phase == DayPhase.Closing ? 1f : game.DayProgress), 8f);

            float reputation = Mathf.Clamp01(game.Reputation / 100f);
            reputationFill.sizeDelta = new Vector2(294f * reputation, 10f);
            if (reputationImage != null)
                reputationImage.color = reputation > 0.6f ? new Color(0.36f, 0.87f, 0.45f)
                    : reputation > 0.3f ? new Color(1f, 0.79f, 0.2f)
                    : new Color(0.95f, 0.26f, 0.25f);

            if (game.ServedToday != cachedServed || game.LostToday != cachedLost || game.RentDue != cachedRent)
            {
                cachedServed = game.ServedToday;
                cachedLost = game.LostToday;
                cachedRent = game.RentDue;
                statsText.text = "Served today   " + cachedServed
                    + "\nWalked out     " + cachedLost
                    + "\nRent tonight   $" + cachedRent
                    + "\nEarned today   $" + game.EarnedToday;
            }

            string carry = game.Player == null || game.Player.Carrying == null
                ? "Hands free - harvest a farm plot"
                : "Carrying " + game.Player.CarryAmount + "/" + GameConfig.CarryCapacity + " " + GameConfig.ProductLabel(game.Player.Carrying.Value);
            if (carry != cachedCarry)
            {
                cachedCarry = carry;
                carryText.text = carry;
            }

            // Someone waiting at an unmanned till is the more urgent of the two, so it wins the line.
            int waiting = game.Checkout != null && !game.Checkout.IsAttended ? game.Checkout.QueueLength : 0;
            string empty = game.EmptyShelfSummary();
            string hint = waiting > 0
                ? waiting + (waiting == 1 ? " shopper is" : " shoppers are") + " waiting at the till!"
                : string.IsNullOrEmpty(empty) ? string.Empty : "Empty shelves: " + empty;
            if (hint != cachedHint)
            {
                cachedHint = hint;
                hintText.text = hint;
            }

            string prompt = game.Player == null ? string.Empty : game.Player.Prompt;
            if (prompt != cachedPrompt)
            {
                cachedPrompt = prompt;
                promptText.text = prompt;
                bool visible = !string.IsNullOrEmpty(prompt);
                if (promptBackground != null) promptBackground.enabled = visible;
            }

            if (Time.unscaledTime > notificationUntil && notificationText.text.Length > 0) notificationText.text = string.Empty;
        }

        private static string FormatClock(int totalMinutes)
        {
            int hour = totalMinutes / 60;
            int minute = totalMinutes % 60;
            return (hour < 10 ? "0" : string.Empty) + hour + ":" + (minute < 10 ? "0" : string.Empty) + minute;
        }

        public void SetNotification(string message, float duration)
        {
            if (notificationText == null) return;
            notificationText.text = message;
            notificationUntil = Time.unscaledTime + duration;
        }

        public void SetPaused(bool paused)
        {
            if (pausePanel != null) pausePanel.SetActive(paused);
            SetNotification(paused ? string.Empty : "Back to work!", paused ? 0f : 1.2f);
        }

        public void ShowDaySummary(int day, int earned, int spent, int served, int lost, int rent, int rentPaid, int balance, bool shortfall)
        {
            if (summaryPanel == null) return;
            string body = "Day " + day + " closed\n\n"
                + "Earned            $" + earned + "\n"
                + "Spent on upgrades $" + spent + "\n"
                + "Shoppers served   " + served + "\n"
                + "Walked out        " + lost + "\n"
                + "Rent              $" + rentPaid + " of $" + rent + "\n"
                + "Balance           $" + balance + "\n\n";
            body += shortfall
                ? "You could not cover rent, so your reputation took a hit."
                : "Rent paid in full. Nice work.";
            body += "\n\nDay " + (day + 1) + " opens in a moment...";
            summaryText.text = body;
            summaryPanel.SetActive(true);
        }

        public void HideDaySummary()
        {
            if (summaryPanel != null) summaryPanel.SetActive(false);
        }

        // ---------------------------------------------------------------- builders

        private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        /// <summary>Background track plus a left anchored fill, returned so callers can resize it.</summary>
        private RectTransform Track(Transform parent, Vector2 offset, Vector2 size, Color fillColor)
        {
            Panel("Track", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), offset, size, TrackColor);
            return Panel("Fill", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), offset, size, fillColor);
        }

        private Text Label(string name, Transform parent, Vector2 offset, Vector2 size, int fontSize, TextAnchor alignment, Color color, FontStyle style)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return text;
        }

        private static void CenterInParent(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
        }
    }
}
