using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ZombieGame
{
    [RequireComponent(typeof(AudioSource))]
    public class StoryIntroManager : MonoBehaviour
    {
        [System.Serializable]
        public class StorySlide
        {
            [Tooltip("The image to display for this story beat")]
            public Texture2D image;

            [Tooltip("Voiceover narration audio clip")]
            public AudioClip voiceover;

            [TextArea(3, 6)]
            [Tooltip("Subtitles displayed at the bottom of the screen")]
            public string subtitleText;
        }

        [Header("Story Slides")]
        public List<StorySlide> slides = new List<StorySlide>();

        [Header("Scene Navigation")]
        [Tooltip("The name of the playable scene to load after the intro")]
        public string targetSceneName = "SampleScene";

        [Header("Presentation Settings")]
        [Tooltip("Time to fade between slides in seconds")]
        public float fadeDuration = 1.0f;

        [Tooltip("Extra pause after voiceover finishes before advancing to next slide")]
        public float slideHoldTime = 1.2f;

        [Tooltip("Subtle Ken Burns zoom intensity (1.0 = none, 1.06 = subtle cinematic zoom)")]
        [Range(1.0f, 1.2f)]
        public float kenBurnsZoom = 1.05f;

        [Header("Audio")]
        [Range(0f, 1f)] public float voiceoverVolume = 1.0f;
        public AudioClip buttonClickSound;

        private enum IntroState
        {
            TitleScreen,
            Transitioning,
            StoryPlaying,
            LoadingNextScene
        }

        private IntroState _currentState = IntroState.TitleScreen;
        private int _currentSlideIndex = -1;
        private float _slideTimer = 0f;
        private float _fadeAlpha = 0f; // 0 = fully transparent overlay, 1 = fully black
        private AudioSource _audioSource;
        private Texture2D _blackTexture;
        private Texture2D _panelTexture;

        // Typewriter subtitle effect
        private string _targetSubtitle = "";
        private string _displayedSubtitle = "";
        private float _typewriterTimer = 0f;
        private int _typewriterCharIndex = 0;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            CreateProceduralTextures();

#if UNITY_EDITOR
            AutoAssignStoryAssets();
#endif
        }

        private void Start()
        {
            _currentState = IntroState.TitleScreen;
            _fadeAlpha = 1.0f;
            StartCoroutine(FadeFromBlack(1.2f));
        }

        private void CreateProceduralTextures()
        {
            if (_blackTexture == null)
            {
                _blackTexture = new Texture2D(1, 1);
                _blackTexture.SetPixel(0, 0, Color.black);
                _blackTexture.Apply();
            }

            if (_panelTexture == null)
            {
                _panelTexture = new Texture2D(1, 1);
                _panelTexture.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.08f, 0.88f));
                _panelTexture.Apply();
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            AutoAssignStoryAssets();
        }

        private void OnValidate()
        {
            if (slides == null || slides.Count == 0)
            {
                AutoAssignStoryAssets();
            }
        }

        [ContextMenu("Auto-Assign Starter Assets")]
        public void AutoAssignStoryAssets()
        {
            if (slides != null && slides.Count >= 4 && slides[0].image != null) return;

            slides = new List<StorySlide>();

            // Slide 1: Tunnel entrance
            slides.Add(new StorySlide
            {
                image = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Project/Starter/Noir Tunnel to the Industrial Skyline.png"),
                voiceover = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/Starter/Audio/Story_Part1.wav"),
                subtitleText = "Four days ago, Sector 4 went completely silent. No broadcasts, no distress calls, nothing. Command declared a state of emergency and dispatched our three-man squad into the industrial city."
            });

            // Slide 2: Zombie ambush in tunnel
            slides.Add(new StorySlide
            {
                image = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Project/Starter/Zombie Chase Through Foggy Tunnel.png"),
                voiceover = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/Starter/Audio/Story_Part2.wav"),
                subtitleText = "We entered through the northern highway tunnel. Out of the heavy fog, dozens of infected figures sprinted directly into our headlights. They were not human anymore."
            });

            // Slide 3: Armoured vehicle crash
            slides.Add(new StorySlide
            {
                image = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Project/Starter/Armoured Vehicle Crash in Tunnel.png"),
                voiceover = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/Starter/Audio/Story_Part3.wav"),
                subtitleText = "Our driver lost control. The armored vehicle swerved, flipped, and smashed into the concrete barrier with violent force. Metal tore apart. Everything went black."
            });

            // Slide 4: Lone survivor crawling to rifle
            slides.Add(new StorySlide
            {
                image = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Project/Starter/Desperate Reach Amid Ruins.png"),
                voiceover = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/Starter/Audio/Story_Part4.wav"),
                subtitleText = "I woke up to burning smoke and shattered glass. My squad did not make it. Outside, the horde is closing in. I must grab that rifle, find a way out, and survive."
            });
        }
#endif

        private void Update()
        {
            // Title screen start input
            if (_currentState == IntroState.TitleScreen)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                {
                    StartStory();
                }
            }
            // During story
            else if (_currentState == IntroState.StoryPlaying)
            {
                _slideTimer += Time.deltaTime;

                // Typewriter text update
                UpdateTypewriter();

                // Skip entire story with Escape or S
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    SkipToGame();
                    return;
                }

                // Advance to next slide with Space, Enter, or Left Mouse Click
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                {
                    // If subtitle is still typing, finish typing immediately first
                    if (_typewriterCharIndex < _targetSubtitle.Length)
                    {
                        _typewriterCharIndex = _targetSubtitle.Length;
                        _displayedSubtitle = _targetSubtitle;
                    }
                    else
                    {
                        AdvanceSlide();
                    }
                    return;
                }

                // Auto-advance when voiceover is done
                float clipDuration = 4.0f;
                if (_currentSlideIndex >= 0 && _currentSlideIndex < slides.Count)
                {
                    var cur = slides[_currentSlideIndex];
                    if (cur.voiceover != null) clipDuration = cur.voiceover.length;
                }

                if (_slideTimer >= (clipDuration + slideHoldTime))
                {
                    AdvanceSlide();
                }
            }
        }

        public void StartStory()
        {
            if (_currentState != IntroState.TitleScreen) return;
            if (buttonClickSound != null) _audioSource.PlayOneShot(buttonClickSound);
            StartCoroutine(StartStoryRoutine());
        }

        private IEnumerator StartStoryRoutine()
        {
            _currentState = IntroState.Transitioning;
            yield return StartCoroutine(FadeToBlack(0.6f));

            _currentSlideIndex = 0;
            LoadSlideContent(_currentSlideIndex);

            yield return StartCoroutine(FadeFromBlack(0.8f));
            _currentState = IntroState.StoryPlaying;
        }

        private void AdvanceSlide()
        {
            if (_currentState != IntroState.StoryPlaying) return;

            int nextIndex = _currentSlideIndex + 1;
            if (nextIndex < slides.Count)
            {
                StartCoroutine(TransitionToSlide(nextIndex));
            }
            else
            {
                // Completed all 4 slides -> Load gameplay scene
                StartCoroutine(FinishIntroAndLoadGame());
            }
        }

        public void SkipToGame()
        {
            if (_currentState == IntroState.LoadingNextScene) return;
            StartCoroutine(FinishIntroAndLoadGame());
        }

        private IEnumerator TransitionToSlide(int targetIndex)
        {
            _currentState = IntroState.Transitioning;
            yield return StartCoroutine(FadeToBlack(fadeDuration * 0.5f));

            _currentSlideIndex = targetIndex;
            LoadSlideContent(_currentSlideIndex);

            yield return StartCoroutine(FadeFromBlack(fadeDuration * 0.5f));
            _currentState = IntroState.StoryPlaying;
        }

        private void LoadSlideContent(int index)
        {
            _slideTimer = 0f;

            if (index < 0 || index >= slides.Count) return;

            var slide = slides[index];

            // Play voice narration
            if (slide.voiceover != null && _audioSource != null)
            {
                _audioSource.Stop();
                _audioSource.clip = slide.voiceover;
                _audioSource.volume = voiceoverVolume;
                _audioSource.Play();
            }

            // Setup typewriter subtitles
            _targetSubtitle = slide.subtitleText ?? "";
            _displayedSubtitle = "";
            _typewriterCharIndex = 0;
            _typewriterTimer = 0f;
        }

        private void UpdateTypewriter()
        {
            if (_typewriterCharIndex < _targetSubtitle.Length)
            {
                _typewriterTimer += Time.deltaTime;
                // ~35 characters per second
                if (_typewriterTimer >= 0.028f)
                {
                    _typewriterTimer = 0f;
                    _typewriterCharIndex = Mathf.Min(_typewriterCharIndex + 1, _targetSubtitle.Length);
                    _displayedSubtitle = _targetSubtitle.Substring(0, _typewriterCharIndex);
                }
            }
        }

        private IEnumerator FinishIntroAndLoadGame()
        {
            _currentState = IntroState.LoadingNextScene;
            if (_audioSource != null) _audioSource.Stop();

            yield return StartCoroutine(FadeToBlack(0.8f));

            // Wait brief moment on black
            yield return new WaitForSeconds(0.4f);

            if (!string.IsNullOrEmpty(targetSceneName))
            {
                SceneManager.LoadScene(targetSceneName);
            }
        }

        private IEnumerator FadeToBlack(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _fadeAlpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            _fadeAlpha = 1.0f;
        }

        private IEnumerator FadeFromBlack(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _fadeAlpha = 1.0f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            _fadeAlpha = 0f;
        }

        private void OnGUI()
        {
            CreateProceduralTextures();

            float screenW = Screen.width;
            float screenH = Screen.height;

            if (_currentState == IntroState.TitleScreen)
            {
                DrawTitleScreen(screenW, screenH);
            }
            else if (_currentState == IntroState.StoryPlaying || _currentState == IntroState.Transitioning)
            {
                DrawStorySlide(screenW, screenH);
            }
            else if (_currentState == IntroState.LoadingNextScene)
            {
                DrawLoadingScreen(screenW, screenH);
            }

            // Black fade overlay
            if (_fadeAlpha > 0.001f)
            {
                GUI.color = new Color(0, 0, 0, _fadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _blackTexture);
                GUI.color = Color.white;
            }
        }

        private void DrawTitleScreen(float sw, float sh)
        {
            // Dimmed background using first slide image if available
            if (slides.Count > 0 && slides[0].image != null)
            {
                GUI.color = new Color(0.45f, 0.45f, 0.5f, 1f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), slides[0].image, ScaleMode.ScaleAndCrop);
            }
            else
            {
                GUI.color = new Color(0.08f, 0.09f, 0.12f, 1f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _blackTexture);
            }

            // Dark vignette gradient overlay
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _blackTexture);
            GUI.color = Color.white;

            // Main Title
            float titleY = sh * 0.28f;
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Mathf.RoundToInt(sw * 0.048f), 32, 68),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;

            GUIStyle subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Mathf.RoundToInt(sw * 0.016f), 13, 22),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            subtitleStyle.normal.textColor = new Color(1f, 0.35f, 0.2f, 0.95f); // Reddish hazard accent

            // Shadow
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(2, titleY + 2, sw, 80), "OPERATION: DEAD SECTOR", titleStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, titleY, sw, 80), "OPERATION: DEAD SECTOR", titleStyle);

            GUI.Label(new Rect(0, titleY + 75, sw, 30), "-- SECTOR 4 QUARANTINE ZONE --", subtitleStyle);

            // Pulsing "CLICK TO START" Button
            float pulse = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
            float btnW = Mathf.Clamp(sw * 0.32f, 260f, 420f);
            float btnH = 60f;
            float btnX = (sw - btnW) * 0.5f;
            float btnY = sh * 0.65f;

            Rect btnRect = new Rect(btnX, btnY, btnW, btnH);

            // Button glow background
            GUI.color = new Color(0.08f, 0.12f, 0.18f, 0.85f);
            GUI.DrawTexture(btnRect, _blackTexture);

            // Left & Right accent bars
            Color accentCol = Color.Lerp(new Color(1f, 0.65f, 0.1f, 0.7f), new Color(1f, 0.85f, 0.3f, 1f), pulse);
            GUI.color = accentCol;
            GUI.DrawTexture(new Rect(btnX, btnY, 4f, btnH), _blackTexture);
            GUI.DrawTexture(new Rect(btnX + btnW - 4f, btnY, 4f, btnH), _blackTexture);

            GUIStyle btnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Mathf.RoundToInt(sw * 0.017f), 14, 24),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            btnStyle.normal.textColor = Color.Lerp(new Color(0.85f, 0.9f, 1f, 0.75f), Color.white, pulse);

            GUI.Label(btnRect, "[ CLICK TO START ]", btnStyle);

            // Bottom tip
            GUIStyle tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            tipStyle.normal.textColor = new Color(0.6f, 0.65f, 0.75f, 0.8f);
            GUI.Label(new Rect(0, sh - 45, sw, 30), "Press SPACE, ENTER or Click to begin", tipStyle);

            // Click detection on button or screen
            if (Event.current.type == EventType.MouseDown && btnRect.Contains(Event.current.mousePosition))
            {
                StartStory();
            }
        }

        private void DrawStorySlide(float sw, float sh)
        {
            if (_currentSlideIndex < 0 || _currentSlideIndex >= slides.Count) return;

            var currentSlide = slides[_currentSlideIndex];

            // 1. Draw Image with subtle Ken Burns zoom
            if (currentSlide.image != null)
            {
                float zoom = Mathf.Lerp(1.0f, kenBurnsZoom, Mathf.Clamp01(_slideTimer / 8.0f));
                float imgW = sw * zoom;
                float imgH = sh * zoom;
                float imgX = (sw - imgW) * 0.5f;
                float imgY = (sh - imgH) * 0.5f;

                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(imgX, imgY, imgW, imgH), currentSlide.image, ScaleMode.ScaleAndCrop);
            }

            // 2. Cinematic top & bottom dark letterbox gradient
            float barH = sh * 0.14f;
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, sw, barH), _blackTexture); // Top bar
            GUI.DrawTexture(new Rect(0, sh - barH - 40, sw, barH + 40), _blackTexture); // Bottom bar

            // 3. Top Slide Indicator & Skip Button
            GUI.color = Color.white;
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = new Color(1f, 0.75f, 0.2f, 0.95f);
            GUI.Label(new Rect(35, 18, 300, 25), $"TRANSMISSION [{_currentSlideIndex + 1} / {slides.Count}]", headerStyle);

            // Skip button
            float skipW = 120f;
            float skipH = 34f;
            Rect skipRect = new Rect(sw - skipW - 30, 16, skipW, skipH);

            GUI.color = new Color(0.12f, 0.14f, 0.18f, 0.8f);
            GUI.DrawTexture(skipRect, _blackTexture);
            GUI.color = Color.white;

            GUIStyle skipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            skipStyle.normal.textColor = new Color(0.8f, 0.85f, 0.95f, 0.9f);
            GUI.Label(skipRect, "SKIP [ESC]", skipStyle);

            if (Event.current.type == EventType.MouseDown && skipRect.Contains(Event.current.mousePosition))
            {
                SkipToGame();
                return;
            }

            // 4. Subtitle Glass Plate & Text
            float subBoxW = Mathf.Clamp(sw * 0.84f, 500f, 1100f);
            float subBoxH = Mathf.Clamp(sh * 0.16f, 80f, 130f);
            float subBoxX = (sw - subBoxW) * 0.5f;
            float subBoxY = sh - subBoxH - 30f;

            Rect subBoxRect = new Rect(subBoxX, subBoxY, subBoxW, subBoxH);

            // Background glass plate
            GUI.color = new Color(0.04f, 0.06f, 0.09f, 0.88f);
            GUI.DrawTexture(subBoxRect, _panelTexture);

            // Left hazard accent stripe
            GUI.color = new Color(1f, 0.45f, 0.15f, 0.95f);
            GUI.DrawTexture(new Rect(subBoxX, subBoxY, 4f, subBoxH), _blackTexture);
            GUI.color = Color.white;

            // Subtitle Text
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Mathf.RoundToInt(sw * 0.0145f), 15, 23),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontStyle = FontStyle.Normal
            };
            subStyle.normal.textColor = Color.white;

            Rect textRect = new Rect(subBoxX + 25, subBoxY + 12, subBoxW - 50, subBoxH - 24);
            GUI.Label(textRect, _displayedSubtitle, subStyle);

            // Bottom Right "NEXT" prompt
            GUIStyle nextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            nextStyle.normal.textColor = new Color(0.6f, 0.7f, 0.85f, 0.8f);
            GUI.Label(new Rect(subBoxX, subBoxY + subBoxH + 4, subBoxW, 20), "CLICK OR PRESS SPACE TO CONTINUE >", nextStyle);
        }

        private void DrawLoadingScreen(float sw, float sh)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _blackTexture);
            GUI.color = Color.white;

            GUIStyle loadStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            loadStyle.normal.textColor = new Color(1f, 0.75f, 0.2f, 0.95f);
            GUI.Label(new Rect(0, sh * 0.45f, sw, 50), "ENTERING INDUSTRIAL SECTOR 4...", loadStyle);

            GUIStyle tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            tipStyle.normal.textColor = new Color(0.6f, 0.65f, 0.7f, 0.8f);
            GUI.Label(new Rect(0, sh * 0.52f, sw, 30), "Preparing weapons and environment...", tipStyle);
        }
    }
}
