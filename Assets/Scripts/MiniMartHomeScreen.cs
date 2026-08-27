using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MiniMart
{
    /// <summary>
    /// Small runtime title screen that sits over the built world. It keeps the world visible as a
    /// warm background, pauses gameplay until the player chooses Continue or Start Farm, and uses
    /// the same saved state as the game manager.
    /// </summary>
    public sealed class MiniMartHomeScreen : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.18f, 0.10f, 0.06f);
        private static readonly Color Cream = new Color(1f, 0.94f, 0.76f);
        private static readonly Color Wood = new Color(0.45f, 0.23f, 0.12f);
        private static readonly Color Leaf = new Color(0.27f, 0.58f, 0.30f);
        private static readonly Color Gold = new Color(1f, 0.68f, 0.18f);

        private MiniMartGameManager game;
        private GameObject mainCard;
        private GameObject settingsCard;
        private GameObject confirmCard;
        private Text soundState;

        public static MiniMartHomeScreen Create(MiniMartGameManager manager)
        {
            EnsureEventSystem();

            GameObject root = new GameObject("MiniMart_HomeScreen");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            root.AddComponent<GraphicRaycaster>();

            MiniMartHomeScreen screen = root.AddComponent<MiniMartHomeScreen>();
            screen.game = manager;
            screen.Build();
            return screen;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            GameObject eventRoot = new GameObject("MiniMart_UI_EventSystem");
            eventRoot.AddComponent<EventSystem>();
            eventRoot.AddComponent<InputSystemUIInputModule>();
        }

        private void Build()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Dim the live world, rather than using a separate loading scene.
            RectTransform shade = Panel("Home_Shade", transform, Vector2.zero, Vector2.one, Vector2.zero,
                new Color(0.03f, 0.07f, 0.08f, 0.66f));
            Stretch(shade);

            mainCard = BuildMainCard(font);
            settingsCard = BuildSettingsCard(font);
            confirmCard = BuildConfirmCard(font);
            settingsCard.SetActive(false);
            confirmCard.SetActive(false);
        }

        private GameObject BuildMainCard(Font font)
        {
            RectTransform card = Panel("Home_Card", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(460f, 455f), Cream);
            AddOutline(card.gameObject, Wood, 6);

            Text title = Label("Title", card, new Vector2(0f, -46f), new Vector2(450f, 105f), 45,
                TextAnchor.MiddleCenter, Ink, FontStyle.Bold, font);
            title.text = "TINY TOWN\nMINI MART";
            CenterTop(title.rectTransform, 0f, -28f);

            Text subtitle = Label("Subtitle", card, new Vector2(0f, -155f), new Vector2(430f, 50f), 18,
                TextAnchor.MiddleCenter, new Color(0.35f, 0.22f, 0.14f), FontStyle.Normal, font);
            subtitle.text = "Harvest  •  Stock  •  Serve\nBuild your little farm market";
            CenterTop(subtitle.rectTransform, 0f, -128f);

            bool saved = game.HasSavedGame;
            Text saveLine = Label("Save_Information", card, new Vector2(0f, -213f), new Vector2(430f, 30f), 16,
                TextAnchor.MiddleCenter, new Color(0.38f, 0.28f, 0.18f), FontStyle.Normal, font);
            saveLine.text = saved
                ? "Saved farm: Day " + game.Day + "  •  $" + game.Money
                : "Start your first small farm";
            CenterTop(saveLine.rectTransform, 0f, -185f);

            Button primary = Button("Continue_Button", card, new Vector2(0f, -280f), new Vector2(350f, 58f),
                saved ? "CONTINUE" : "START FARM", Leaf, Color.white, font);
            CenterTop(primary.GetComponent<RectTransform>(), 0f, -226f);
            primary.onClick.AddListener(() => game.StartFromHome(false));

            Button fresh = Button("New_Game_Button", card, new Vector2(0f, -348f), new Vector2(350f, 48f),
                "NEW GAME", new Color(0.86f, 0.52f, 0.24f), Color.white, font);
            CenterTop(fresh.GetComponent<RectTransform>(), 0f, -293f);
            fresh.onClick.AddListener(ShowNewGameConfirm);

            Button settings = Button("Settings_Button", card, new Vector2(0f, -408f), new Vector2(350f, 43f),
                "SETTINGS", new Color(0.62f, 0.41f, 0.24f), Color.white, font);
            CenterTop(settings.GetComponent<RectTransform>(), 0f, -348f);
            settings.onClick.AddListener(ShowSettings);

            Text footer = Label("Footer", card, Vector2.zero, new Vector2(440f, 26f), 14,
                TextAnchor.MiddleCenter, new Color(0.40f, 0.29f, 0.19f), FontStyle.Italic, font);
            footer.text = "A cozy farm-to-market game";
            CenterBottom(footer.rectTransform, 0f, 24f);
            return card.gameObject;
        }

        private GameObject BuildSettingsCard(Font font)
        {
            RectTransform card = Panel("Settings_Card", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(420f, 300f), Cream);
            AddOutline(card.gameObject, Wood, 6);

            Text title = Label("Settings_Title", card, Vector2.zero, new Vector2(360f, 48f), 31,
                TextAnchor.MiddleCenter, Ink, FontStyle.Bold, font);
            title.text = "SETTINGS";
            CenterTop(title.rectTransform, 0f, -35f);

            soundState = Label("Sound_State", card, Vector2.zero, new Vector2(350f, 34f), 20,
                TextAnchor.MiddleCenter, new Color(0.33f, 0.22f, 0.14f), FontStyle.Bold, font);
            CenterTop(soundState.rectTransform, 0f, -108f);
            RefreshSoundState();

            Button toggle = Button("Sound_Toggle", card, Vector2.zero, new Vector2(300f, 52f), "TOGGLE SOUND",
                Gold, Ink, font);
            CenterTop(toggle.GetComponent<RectTransform>(), 0f, -153f);
            toggle.onClick.AddListener(ToggleSound);

            Button back = Button("Settings_Back", card, Vector2.zero, new Vector2(300f, 46f), "BACK",
                new Color(0.62f, 0.41f, 0.24f), Color.white, font);
            CenterTop(back.GetComponent<RectTransform>(), 0f, -215f);
            back.onClick.AddListener(HideSettings);
            return card.gameObject;
        }

        private GameObject BuildConfirmCard(Font font)
        {
            RectTransform card = Panel("New_Game_Confirm", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(460f, 330f), Cream);
            AddOutline(card.gameObject, Wood, 6);

            Text title = Label("Confirm_Title", card, Vector2.zero, new Vector2(400f, 44f), 28,
                TextAnchor.MiddleCenter, Ink, FontStyle.Bold, font);
            title.text = "START A FRESH FARM?";
            CenterTop(title.rectTransform, 0f, -40f);

            Text warning = Label("Confirm_Warning", card, Vector2.zero, new Vector2(385f, 84f), 18,
                TextAnchor.MiddleCenter, new Color(0.40f, 0.24f, 0.15f), FontStyle.Normal, font);
            warning.text = game.HasSavedGame
                ? "This removes your saved money,\nday, and egg-table upgrade."
                : "Your new farm will begin on Day 1.";
            CenterTop(warning.rectTransform, 0f, -102f);

            Button confirm = Button("Fresh_Farm_Confirm", card, Vector2.zero, new Vector2(320f, 52f), "START NEW FARM",
                new Color(0.82f, 0.34f, 0.22f), Color.white, font);
            CenterTop(confirm.GetComponent<RectTransform>(), 0f, -202f);
            confirm.onClick.AddListener(() => game.StartFromHome(true));

            Button cancel = Button("Fresh_Farm_Cancel", card, Vector2.zero, new Vector2(320f, 45f), "CANCEL",
                new Color(0.62f, 0.41f, 0.24f), Color.white, font);
            CenterTop(cancel.GetComponent<RectTransform>(), 0f, -262f);
            cancel.onClick.AddListener(HideNewGameConfirm);
            return card.gameObject;
        }

        private void ShowSettings()
        {
            mainCard.SetActive(false);
            settingsCard.SetActive(true);
        }

        private void HideSettings()
        {
            settingsCard.SetActive(false);
            mainCard.SetActive(true);
        }

        private void ShowNewGameConfirm()
        {
            mainCard.SetActive(false);
            confirmCard.SetActive(true);
        }

        private void HideNewGameConfirm()
        {
            confirmCard.SetActive(false);
            mainCard.SetActive(true);
        }

        private void ToggleSound()
        {
            AudioListener.volume = AudioListener.volume > 0.05f ? 0f : 1f;
            PlayerPrefs.SetInt("MiniMart.SoundEnabled", AudioListener.volume > 0.05f ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSoundState();
        }

        private void RefreshSoundState()
        {
            if (soundState != null) soundState.text = AudioListener.volume > 0.05f ? "Sound: ON" : "Sound: OFF";
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Button Button(string name, Transform parent, Vector2 position, Vector2 size, string label, Color background, Color foreground, Font font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = background;
            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            button.colors = colors;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            AddOutline(go, new Color(0.28f, 0.15f, 0.09f), 3);

            Text text = Label("Label", go.transform, Vector2.zero, size - new Vector2(16f, 8f), 19,
                TextAnchor.MiddleCenter, foreground, FontStyle.Bold, font);
            text.text = label;
            text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.transform.SetAsLastSibling();
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return button;
        }

        private static Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color, FontStyle style, Font font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 13;
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        private static void AddOutline(GameObject target, Color color, int thickness)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, -thickness);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CenterTop(RectTransform rect, float x, float y)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void CenterBottom(RectTransform rect, float x, float y)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
        }
    }
}
