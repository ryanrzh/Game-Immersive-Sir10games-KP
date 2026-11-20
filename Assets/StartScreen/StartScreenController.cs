
using UnityEngine;
using UnityEngine.UIElements;

public class StartScreenController : MonoBehaviour
{
    [Header("UI Documents")]
    private UIDocument startScreenDocument;
    [SerializeField] private UIDocument pilihAturanDocument;

    [Header("Background")]
    [SerializeField] private Sprite factoryBackgroundSprite;

    [Header("Animation")]
    [SerializeField] private bool animatePlayButton = true;

    private VisualElement root;
    private VisualElement factoryBackground;

    private VisualElement aboutOverlay;
    private VisualElement settingsOverlay;

    private Button btnPlay;
    private Button btnAbout;
    private Button btnSettings;
    private Button btnSettingsTop;

    private Button btnCloseAbout;
    private Button btnAboutPlay;
    private Button btnScrollUp;
    private Button btnScrollDown;

    private Button btnCloseSettings;
    private Button btnCancelSettings;
    private Button btnSaveSettings;
    private Button btnResetSettings;

    private ScrollView aboutScroll;
    private ScrollView settingsScroll;

    private Toggle toggleSound;
    private Toggle toggleFullscreen;
    private Slider sliderVolume;
    private Label volumeValue;

    private IVisualElementScheduledItem playAnimation;

    private bool initialized;

    private bool savedSoundEnabled;
    private float savedVolume;

    private bool pendingSoundEnabled;
    private float pendingVolume;

    private const string SoundKey =
        "SIRVIVE_SOUND_ENABLED";

    private const string VolumeKey =
        "SIRVIVE_MASTER_VOLUME";

    private const float DefaultVolume = 0.8f;

    private const float CompactWidth = 1200f;
    private const float CompactHeight = 720f;

    // ==========================================
    // INITIALIZATION
    // ==========================================

    private void OnEnable()
    {
        if (startScreenDocument == null)
            startScreenDocument = GetComponent<UIDocument>();

        if (startScreenDocument == null)
        {
            Debug.LogError(
                "StartScreenController: UIDocument tidak ditemukan.");
            return;
        }

        root = startScreenDocument.rootVisualElement
            .Q<VisualElement>("start-root");

        if (root == null)
        {
            Debug.LogError(
                "StartScreenController: Elemen start-root tidak ditemukan.");
            return;
        }

        factoryBackground =
            root.Q<VisualElement>("factory-bg");

        aboutOverlay =
            root.Q<VisualElement>("about-overlay");

        settingsOverlay =
            root.Q<VisualElement>("settings-overlay");

        btnPlay = root.Q<Button>("btn-play");
        btnAbout = root.Q<Button>("btn-about");
        btnSettings = root.Q<Button>("btn-settings");
        btnSettingsTop = root.Q<Button>("btn-settings-top");

        btnCloseAbout = root.Q<Button>("btn-close-about");
        btnAboutPlay = root.Q<Button>("btn-about-play");

        btnScrollUp = root.Q<Button>("btn-scroll-up");
        btnScrollDown = root.Q<Button>("btn-scroll-down");

        btnCloseSettings =
            root.Q<Button>("btn-close-settings");

        btnCancelSettings =
            root.Q<Button>("btn-cancel-settings");

        btnSaveSettings =
            root.Q<Button>("btn-save-settings");

        btnResetSettings =
            root.Q<Button>("btn-reset-settings");

        aboutScroll =
            root.Q<ScrollView>("about-scroll");

        settingsScroll =
            root.Q<ScrollView>("settings-scroll");

        toggleSound =
            root.Q<Toggle>("toggle-sound");

        toggleFullscreen =
            root.Q<Toggle>("toggle-fullscreen");

        sliderVolume =
            root.Q<Slider>("slider-volume");

        volumeValue =
            root.Q<Label>("volume-value");

        if (!ValidateElements())
        {
            Debug.LogError(
                "StartScreenController: Ada elemen UXML yang tidak ditemukan. " +
                "Pastikan UXML versi terbaru digunakan.");
            return;
        }

        initialized = true;

        ApplyBackground();
        LoadSavedSettings();

        aboutOverlay.style.display = DisplayStyle.None;
        settingsOverlay.style.display = DisplayStyle.None;

        RegisterEvents();

        root.RegisterCallback<GeometryChangedEvent>(
            OnRootGeometryChanged);

        if (animatePlayButton)
        {
            playAnimation = root.schedule
                .Execute(AnimatePlayButton)
                .Every(50);
        }
    }

    // ==========================================
    // VALIDATION
    // ==========================================

    private bool ValidateElements()
    {
        return
            factoryBackground != null &&
            aboutOverlay != null &&
            settingsOverlay != null &&

            btnPlay != null &&
            btnAbout != null &&
            btnSettings != null &&
            btnSettingsTop != null &&

            btnCloseAbout != null &&
            btnAboutPlay != null &&
            btnScrollUp != null &&
            btnScrollDown != null &&

            btnCloseSettings != null &&
            btnCancelSettings != null &&
            btnSaveSettings != null &&
            btnResetSettings != null &&

            aboutScroll != null &&
            settingsScroll != null &&

            toggleSound != null &&
            toggleFullscreen != null &&
            sliderVolume != null &&
            volumeValue != null;
    }

    // ==========================================
    // BACKGROUND
    // ==========================================

    private void ApplyBackground()
    {
        if (factoryBackground == null)
            return;

        factoryBackground.pickingMode =
            PickingMode.Ignore;

        if (factoryBackgroundSprite != null)
        {
            factoryBackground.style.backgroundImage =
                new StyleBackground(factoryBackgroundSprite);
        }
        else
        {
            Debug.LogWarning(
                "StartScreenController: Background Sprite belum dipasang.");
        }
    }

    // ==========================================
    // REGISTER EVENTS
    // ==========================================

    private void RegisterEvents()
    {
        btnPlay.clicked += OnPlayClicked;
        btnAbout.clicked += OpenAbout;

        btnSettings.clicked += OpenSettings;
        btnSettingsTop.clicked += OpenSettings;

        btnCloseAbout.clicked += CloseAbout;
        btnAboutPlay.clicked += OnPlayClicked;

        btnScrollUp.clicked += ScrollAboutUp;
        btnScrollDown.clicked += ScrollAboutDown;

        btnCloseSettings.clicked += CancelSettings;
        btnCancelSettings.clicked += CancelSettings;

        btnSaveSettings.clicked += SaveSettings;
        btnResetSettings.clicked += ResetSettings;

        toggleSound.RegisterValueChangedCallback(
            OnSoundChanged);

        sliderVolume.RegisterValueChangedCallback(
            OnVolumeChanged);

        aboutScroll.verticalScroller.valueChanged +=
            OnAboutScrollChanged;

        aboutScroll.contentViewport
            .RegisterCallback<GeometryChangedEvent>(
                OnAboutGeometryChanged);

        aboutScroll.contentContainer
            .RegisterCallback<GeometryChangedEvent>(
                OnAboutGeometryChanged);
    }

    // ==========================================
    // START GAME
    // ==========================================

    private void OnPlayClicked()
    {
        if (pilihAturanDocument == null)
        {
            Debug.LogWarning(
                "StartScreenController: Pilih Aturan Document belum diisi.");
            return;
        }

        if (!pilihAturanDocument.gameObject.activeInHierarchy)
        {
            Debug.LogWarning(
                "StartScreenController: GameObject Pilih Aturan belum aktif.");
            return;
        }

        VisualElement targetRoot =
            pilihAturanDocument.rootVisualElement;

        if (targetRoot == null)
        {
            Debug.LogWarning(
                "StartScreenController: Root Pilih Aturan tidak ditemukan.");
            return;
        }

        // Open the target screen.
        targetRoot.style.display =
            DisplayStyle.Flex;

        // Close both modal overlays.
        CloseAbout();

        settingsOverlay.style.display =
            DisplayStyle.None;

        // Hide start screen.
        root.style.display =
            DisplayStyle.None;
    }

    // ==========================================
    // ABOUT GAME MODAL
    // ==========================================

    private void OpenAbout()
    {
        settingsOverlay.style.display =
            DisplayStyle.None;

        aboutOverlay.style.display =
            DisplayStyle.Flex;

        aboutOverlay.BringToFront();

        aboutScroll.scrollOffset =
            Vector2.zero;

        // Wait until layout has been calculated.
        aboutScroll.schedule
            .Execute(UpdateAboutScrollButtons)
            .ExecuteLater(50);
    }

    private void CloseAbout()
    {
        if (aboutOverlay != null)
        {
            aboutOverlay.style.display =
                DisplayStyle.None;
        }
    }

    private void ScrollAboutUp()
    {
        ScrollAboutBy(-250f);
    }

    private void ScrollAboutDown()
    {
        ScrollAboutBy(250f);
    }

    private void ScrollAboutBy(float amount)
    {
        if (aboutScroll == null)
            return;

        float maxY =
            Mathf.Max(
                0f,
                aboutScroll.verticalScroller.highValue);

        float targetY =
            Mathf.Clamp(
                aboutScroll.scrollOffset.y + amount,
                0f,
                maxY);

        Vector2 offset =
            aboutScroll.scrollOffset;

        offset.y = targetY;

        aboutScroll.scrollOffset = offset;

        UpdateAboutScrollButtons();
    }

    private void OnAboutScrollChanged(float value)
    {
        UpdateAboutScrollButtons();
    }

    private void OnAboutGeometryChanged(
        GeometryChangedEvent evt)
    {
        UpdateAboutScrollButtons();
    }

    private void UpdateAboutScrollButtons()
    {
        if (aboutScroll == null ||
            btnScrollUp == null ||
            btnScrollDown == null)
            return;

        float y =
            aboutScroll.scrollOffset.y;

        float maxY =
            aboutScroll.verticalScroller.highValue;

        if (float.IsNaN(maxY) ||
            float.IsInfinity(maxY))
            return;

        maxY = Mathf.Max(0f, maxY);

        btnScrollUp.SetEnabled(y > 2f);
        btnScrollDown.SetEnabled(y < maxY - 2f);
    }

    // ==========================================
    // SETTINGS MODAL
    // ==========================================

    private void OpenSettings()
    {
        CloseAbout();

        pendingSoundEnabled = savedSoundEnabled;
        pendingVolume = savedVolume;

        toggleSound.SetValueWithoutNotify(
            pendingSoundEnabled);

        sliderVolume.SetValueWithoutNotify(
            pendingVolume * 100f);

        toggleFullscreen.SetValueWithoutNotify(
            Screen.fullScreen);

        UpdateVolumeLabel();

        settingsOverlay.style.display =
            DisplayStyle.Flex;

        settingsOverlay.BringToFront();

        settingsScroll.scrollOffset =
            Vector2.zero;
    }

    // ==========================================
    // CANCEL SETTINGS
    // ==========================================

    private void CancelSettings()
    {
        pendingSoundEnabled = savedSoundEnabled;
        pendingVolume = savedVolume;

        // Restore saved audio values.
        ApplyAudio(
            savedSoundEnabled,
            savedVolume);

        toggleSound.SetValueWithoutNotify(
            savedSoundEnabled);

        sliderVolume.SetValueWithoutNotify(
            savedVolume * 100f);

        UpdateVolumeLabel();

        settingsOverlay.style.display =
            DisplayStyle.None;
    }

    // ==========================================
    // SAVE SETTINGS
    // ==========================================

    private void SaveSettings()
    {
        savedSoundEnabled = pendingSoundEnabled;
        savedVolume = pendingVolume;

        PlayerPrefs.SetInt(
            SoundKey,
            savedSoundEnabled ? 1 : 0);

        PlayerPrefs.SetFloat(
            VolumeKey,
            savedVolume);

        PlayerPrefs.Save();

        ApplyAudio(
            savedSoundEnabled,
            savedVolume);

        bool requestedFullscreen =
            toggleFullscreen.value;

#if !UNITY_WEBGL
        if (Screen.fullScreen != requestedFullscreen)
        {
            Screen.fullScreen =
                requestedFullscreen;
        }
#else
        // WebGL fullscreen is controlled by
        // the browser's fullscreen permissions.
        Debug.Log(
            "SIRVIVE: Fullscreen WebGL requires " +
            "browser-supported fullscreen handling.");
#endif

        settingsOverlay.style.display =
            DisplayStyle.None;
    }

    // ==========================================
    // RESET SETTINGS (PENDING ONLY)
    // ==========================================

    private void ResetSettings()
    {
        pendingSoundEnabled = true;
        pendingVolume = DefaultVolume;

        toggleSound.SetValueWithoutNotify(true);

        sliderVolume.SetValueWithoutNotify(
            DefaultVolume * 100f);

        toggleFullscreen.SetValueWithoutNotify(false);

        UpdateVolumeLabel();

        ApplyAudio(
            pendingSoundEnabled,
            pendingVolume);
    }

    // ==========================================
    // AUDIO CALLBACKS
    // ==========================================

    private void OnSoundChanged(
        ChangeEvent<bool> evt)
    {
        pendingSoundEnabled = evt.newValue;

        ApplyAudio(
            pendingSoundEnabled,
            pendingVolume);
    }

    private void OnVolumeChanged(
        ChangeEvent<float> evt)
    {
        pendingVolume =
            Mathf.Clamp01(
                evt.newValue / 100f);

        UpdateVolumeLabel();

        ApplyAudio(
            pendingSoundEnabled,
            pendingVolume);
    }

    private void UpdateVolumeLabel()
    {
        if (volumeValue == null)
            return;

        volumeValue.text =
            Mathf.RoundToInt(
                pendingVolume * 100f) + "%";
    }

    private void LoadSavedSettings()
    {
        savedSoundEnabled =
            PlayerPrefs.GetInt(
                SoundKey,
                1) == 1;

        savedVolume =
            Mathf.Clamp01(
                PlayerPrefs.GetFloat(
                    VolumeKey,
                    DefaultVolume));

        pendingSoundEnabled =
            savedSoundEnabled;

        pendingVolume =
            savedVolume;

        ApplyAudio(
            savedSoundEnabled,
            savedVolume);
    }

    private static void ApplyAudio(
        bool enabled,
        float volume)
    {
        AudioListener.volume =
            enabled
                ? Mathf.Clamp01(volume)
                : 0f;
    }

    // ==========================================
    // RESPONSIVE UI
    // ==========================================

    private void OnRootGeometryChanged(
        GeometryChangedEvent evt)
    {
        float width =
            evt.newRect.width;

        float height =
            evt.newRect.height;

        if (width <= 0f || height <= 0f)
            return;

        bool compact =
            width < CompactWidth ||
            height < CompactHeight;

        root.EnableInClassList(
            "start-compact",
            compact);
    }

    // ==========================================
    // START BUTTON ANIMATION
    // NO OBSOLETE TRANSFORM API
    // ==========================================

    private void AnimatePlayButton()
    {
        if (!animatePlayButton ||
            btnPlay == null)
            return;

        bool modalOpen =
            aboutOverlay.style.display.value ==
                DisplayStyle.Flex ||
            settingsOverlay.style.display.value ==
                DisplayStyle.Flex;

        if (modalOpen)
        {
            btnPlay.style.scale =
                new Scale(Vector3.one);

            return;
        }

        float wave =
            Mathf.Sin(
                Time.realtimeSinceStartup * 2.4f);

        float scale =
            1f + wave * 0.008f;

        // Modern Unity UI Toolkit API.
        btnPlay.style.scale =
            new Scale(
                new Vector3(
                    scale,
                    scale,
                    1f));
    }

    // ==========================================
    // CLEANUP
    // ==========================================

    private void OnDisable()
    {
        if (!initialized)
            return;

        btnPlay.clicked -= OnPlayClicked;
        btnAbout.clicked -= OpenAbout;

        btnSettings.clicked -= OpenSettings;
        btnSettingsTop.clicked -= OpenSettings;

        btnCloseAbout.clicked -= CloseAbout;
        btnAboutPlay.clicked -= OnPlayClicked;

        btnScrollUp.clicked -= ScrollAboutUp;
        btnScrollDown.clicked -= ScrollAboutDown;

        btnCloseSettings.clicked -= CancelSettings;
        btnCancelSettings.clicked -= CancelSettings;

        btnSaveSettings.clicked -= SaveSettings;
        btnResetSettings.clicked -= ResetSettings;

        toggleSound.UnregisterValueChangedCallback(
            OnSoundChanged);

        sliderVolume.UnregisterValueChangedCallback(
            OnVolumeChanged);

        aboutScroll.verticalScroller.valueChanged -=
            OnAboutScrollChanged;

        aboutScroll.contentViewport
            .UnregisterCallback<GeometryChangedEvent>(
                OnAboutGeometryChanged);

        aboutScroll.contentContainer
            .UnregisterCallback<GeometryChangedEvent>(
                OnAboutGeometryChanged);

        root.UnregisterCallback<GeometryChangedEvent>(
            OnRootGeometryChanged);

        if (playAnimation != null)
        {
            playAnimation.Pause();
            playAnimation = null;
        }

        initialized = false;
    }
}
