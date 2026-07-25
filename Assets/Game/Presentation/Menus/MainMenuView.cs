using System;
using UnityEngine;
using UnityEngine.UI;

namespace TypingDefense.Presentation.Menus
{
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _englishButton;
        [SerializeField] private Button _hebrewButton;
        [SerializeField] private Button _startButton;
        [SerializeField] private Text _selectedLanguageLabel;

        public event Action<string> StartRequested;

        public void ConfigureRuntime(
            GameObject root,
            Button englishButton,
            Button hebrewButton,
            Button startButton,
            Text selectedLanguageLabel)
        {
            _root = root;
            _englishButton = englishButton;
            _hebrewButton = hebrewButton;
            _startButton = startButton;
            _selectedLanguageLabel = selectedLanguageLabel;
            WireButtons();
        }

        private void Awake()
        {
            WireButtons();
        }

        private void WireButtons()
        {
            if (_englishButton != null)
            {
                _englishButton.onClick.RemoveAllListeners();
                _englishButton.onClick.AddListener(() => Begin("en"));
            }

            if (_hebrewButton != null)
            {
                _hebrewButton.onClick.RemoveAllListeners();
                _hebrewButton.onClick.AddListener(() => Begin("he"));
                var heLabel = _hebrewButton.GetComponentInChildren<Text>();
                if (heLabel != null)
                {
                    heLabel.font = UI.UiFonts.Resolve();
                    heLabel.text = FormatRtl("עברית");
                }
            }

            // Optional legacy start button — hide; language choice starts the run.
            if (_startButton != null)
            {
                _startButton.onClick.RemoveAllListeners();
                _startButton.gameObject.SetActive(false);
            }

            if (_selectedLanguageLabel != null)
                _selectedLanguageLabel.text = "SELECT LANGUAGE TO START";
        }

        private static string FormatRtl(string logical)
        {
            if (string.IsNullOrEmpty(logical))
                return string.Empty;
            var chars = logical.ToCharArray();
            System.Array.Reverse(chars);
            return new string(chars);
        }

        public void Show()
        {
            if (_root != null)
                _root.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void Begin(string languageId)
        {
            StartRequested?.Invoke(languageId ?? "en");
        }
    }

    public sealed class ResultsView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _summaryText;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _menuButton;

        public event Action RetryRequested;
        public event Action MenuRequested;

        public void ConfigureRuntime(GameObject root, Text summaryText, Button retryButton, Button menuButton)
        {
            _root = root;
            _summaryText = summaryText;
            _retryButton = retryButton;
            _menuButton = menuButton;
            WireButtons();
        }

        private void Awake() => WireButtons();

        private void WireButtons()
        {
            if (_retryButton != null)
            {
                _retryButton.onClick.RemoveAllListeners();
                _retryButton.onClick.AddListener(() => RetryRequested?.Invoke());
            }

            if (_menuButton != null)
            {
                _menuButton.onClick.RemoveAllListeners();
                _menuButton.onClick.AddListener(() => MenuRequested?.Invoke());
            }
        }

        public void Show(string summary)
        {
            if (_summaryText != null)
                _summaryText.text = summary;
            if (_root != null)
                _root.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
            else
                gameObject.SetActive(false);
        }
    }
}
