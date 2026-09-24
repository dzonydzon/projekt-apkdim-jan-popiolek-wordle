namespace WordlePL;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

public partial class MainWindow : Window
{
    // ── Kolory (Wordle palette) ──
    private static readonly SolidColorBrush CorrectBrush  = new(Color.FromRgb(0x53, 0x8d, 0x4e));
    private static readonly SolidColorBrush PresentBrush  = new(Color.FromRgb(0xb5, 0x9f, 0x3b));
    private static readonly SolidColorBrush AbsentBrush   = new(Color.FromRgb(0x3a, 0x3a, 0x3c));
    private static readonly SolidColorBrush EmptyBorder   = new(Color.FromRgb(0x3a, 0x3a, 0x3c));
    private static readonly SolidColorBrush FilledBorder  = new(Color.FromRgb(0x56, 0x57, 0x58));
    private static readonly SolidColorBrush KeyDefault    = new(Color.FromRgb(0x81, 0x83, 0x84));
    private static readonly SolidColorBrush TextWhite     = new(Color.FromRgb(0xf8, 0xf9, 0xfa));
    private static readonly SolidColorBrush TransparentBg = Brushes.Transparent;

    // ── Układ klawiatury ──
    private static readonly string[][] KeyboardLayout =
    {
        new[] { "Q","W","E","R","T","Y","U","I","O","P" },
        new[] { "A","S","D","F","G","H","J","K","L" },
        new[] { "ENTER","Z","X","C","V","B","N","M","⌫" },
        new[] { "Ą","Ć","Ę","Ł","Ń","Ó","Ś","Ź","Ż" },
    };

    // Zbiór dozwolonych liter (dla walidacji klawiatury fizycznej)
    private static readonly HashSet<char> ValidLetters = new(
        "AĄBCĆDEĘFGHIJKLŁMNŃOÓPQRSŚTUVWXYZŹŻ".ToCharArray());

    // ── Stan gry ──
    private string _secretWord = "";
    private int _currentRow;
    private int _currentCol;
    private bool _isGameOver;

    // Siatka kafelków: [row, col]
    private readonly Border[,] _tiles = new Border[GameLogic.MaxAttempts, GameLogic.WordLength];
    private readonly TextBlock[,] _tileTexts = new TextBlock[GameLogic.MaxAttempts, GameLogic.WordLength];

    // Przyciski klawiatury: litera → przycisk
    private readonly Dictionary<string, Button> _keyButtons = new();

    // Stan klawiatury (najlepszy status każdej litery)
    private readonly Dictionary<char, LetterStatus> _keyboardState = new();

    // Statystyki
    private int _gamesPlayed;
    private int _gamesWon;
    private int _streak;

    public MainWindow()
    {
        InitializeComponent();
        BuildBoard();
        BuildKeyboard();
        StartNewRound();
        Loaded += (_, _) => Focus();
    }

    // ══════════════════════════════════════════
    //  BUDOWA UI
    // ══════════════════════════════════════════

    private void BuildBoard()
    {
        for (int r = 0; r < GameLogic.MaxAttempts; r++)
        {
            for (int c = 0; c < GameLogic.WordLength; c++)
            {
                var textBlock = new TextBlock
                {
                    Text = "",
                    FontSize = 26,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextWhite,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                var border = new Border
                {
                    BorderBrush = EmptyBorder,
                    BorderThickness = new Thickness(2),
                    Background = TransparentBg,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(3),
                    Child = textBlock,
                };

                Grid.SetRow(border, r);
                Grid.SetColumn(border, c);
                BoardGrid.Children.Add(border);

                _tiles[r, c] = border;
                _tileTexts[r, c] = textBlock;
            }
        }
    }

    private void BuildKeyboard()
    {
        KeyboardPanel.Children.Clear();

        foreach (var row in KeyboardLayout)
        {
            var panel = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2),
            };

            foreach (var key in row)
            {
                bool isWide = key == "ENTER" || key == "⌫";

                var btn = new Button
                {
                    Content = key,
                    Width = isWide ? 62 : 36,
                    Height = 44,
                    Margin = new Thickness(2),
                    FontSize = isWide ? 12 : 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    Background = KeyDefault,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Focusable = false,  // aby nie kraść focusu od okna
                    Tag = key,
                };

                // Zaokrąglone rogi przez ControlTemplate
                btn.Template = CreateRoundedButtonTemplate();

                btn.Click += KeyButton_Click;
                panel.Children.Add(btn);
                _keyButtons[key] = btn;
            }

            KeyboardPanel.Children.Add(panel);
        }
    }

    private static ControlTemplate CreateRoundedButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetBinding(Border.BackgroundProperty,
            new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(2));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

        borderFactory.AppendChild(contentFactory);
        template.VisualTree = borderFactory;
        return template;
    }

    // ══════════════════════════════════════════
    //  NOWA RUNDA
    // ══════════════════════════════════════════

    private void StartNewRound()
    {
        _secretWord = WordBank.GetRandomWord();
        _currentRow = 0;
        _currentCol = 0;
        _isGameOver = false;

        // Wyczyść planszę
        for (int r = 0; r < GameLogic.MaxAttempts; r++)
        {
            for (int c = 0; c < GameLogic.WordLength; c++)
            {
                _tileTexts[r, c].Text = "";
                _tiles[r, c].Background = TransparentBg;
                _tiles[r, c].BorderBrush = EmptyBorder;
            }
        }

        // Wyczyść klawiaturę
        _keyboardState.Clear();
        foreach (var kvp in _keyButtons)
        {
            kvp.Value.Background = KeyDefault;
            kvp.Value.Opacity = 1.0;
        }

        StreakText.Text = _streak.ToString();
        ToastBorder.BeginAnimation(OpacityProperty, null);
        ToastBorder.Opacity = 0;
        Focus();
    }

    // ══════════════════════════════════════════
    //  OBSŁUGA KLAWISZY
    // ══════════════════════════════════════════

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_isGameOver) return;

        if (e.Key == Key.Enter)
        {
            SubmitRow();
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            DeleteLetter();
            e.Handled = true;
        }
        else
        {
            // Konwersja klawisza do znaku z uwzględnieniem polskich liter
            string text = KeyToPolishChar(e);
            if (!string.IsNullOrEmpty(text))
            {
                char ch = char.ToUpper(text[0]);
                if (ValidLetters.Contains(ch))
                {
                    AddLetter(ch);
                    e.Handled = true;
                }
            }
        }
    }

    /// <summary>
    /// Konwertuje KeyEventArgs na polską literę, wspierając Alt+klawisz i Oem klawisze.
    /// </summary>
    private static string KeyToPolishChar(KeyEventArgs e)
    {
        // Próba uzyskania wpisanego znaku przez TextComposition
        // Dla standardowych liter (Key.A-Key.Z)
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key >= Key.A && key <= Key.Z)
        {
            // Polskie znaki z AltGr (prawy Alt) – Windows wysyła je jako Ctrl+Alt+klawisz
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
                Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                return key switch
                {
                    Key.A => "Ą",
                    Key.C => "Ć",
                    Key.E => "Ę",
                    Key.L => "Ł",
                    Key.N => "Ń",
                    Key.O => "Ó",
                    Key.S => "Ś",
                    Key.X => "Ź",
                    Key.Z => "Ż",
                    _ => null!
                };
            }

            // Normalny klawisz (bez modyfikatorów lub z samym Shift)
            if (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift)
            {
                return key.ToString();
            }
        }

        return null!;
    }

    private void KeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGameOver) return;

        if (sender is Button btn && btn.Tag is string key)
        {
            if (key == "ENTER")
                SubmitRow();
            else if (key == "⌫")
                DeleteLetter();
            else if (key.Length == 1)
                AddLetter(char.ToUpper(key[0]));
        }

        Focus(); // Oddaj focus oknu, żeby klawiatura fizyczna dalej działała
    }

    // ══════════════════════════════════════════
    //  LOGIKA GRY
    // ══════════════════════════════════════════

    private void AddLetter(char letter)
    {
        if (_currentCol >= GameLogic.WordLength) return;

        _tileTexts[_currentRow, _currentCol].Text = letter.ToString();
        _tiles[_currentRow, _currentCol].BorderBrush = FilledBorder;

        // Drobna animacja „pop"
        var tile = _tiles[_currentRow, _currentCol];
        AnimateScale(tile, 1.0, 1.1, 60);
        AnimateScale(tile, 1.1, 1.0, 60, 60);

        _currentCol++;
    }

    private void DeleteLetter()
    {
        if (_currentCol <= 0) return;

        _currentCol--;
        _tileTexts[_currentRow, _currentCol].Text = "";
        _tiles[_currentRow, _currentCol].BorderBrush = EmptyBorder;
    }

    private void SubmitRow()
    {
        if (_currentCol < GameLogic.WordLength)
        {
            ShowToast("Za mało liter");
            ShakeRow(_currentRow);
            return;
        }

        // Zbuduj zgadywane słowo
        string guess = "";
        for (int c = 0; c < GameLogic.WordLength; c++)
            guess += _tileTexts[_currentRow, c].Text;

        // Walidacja obecności w bazie polskich słów
        if (!WordBank.IsValidWord(guess))
        {
            ShowToast("Nie ma takiego słowa");
            ShakeRow(_currentRow);
            return;
        }

        // Oceń
        var evaluation = GameLogic.Evaluate(guess, _secretWord);

        // Koloruj kafelki z opóźnieniem (animacja flip)
        for (int i = 0; i < GameLogic.WordLength; i++)
        {
            int col = i; // przechwycenie zmiennej
            int delayMs = i * 250;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(delayMs)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();

                var tile = _tiles[_currentRow, col];
                var brush = evaluation[col] switch
                {
                    LetterStatus.Correct => CorrectBrush,
                    LetterStatus.Present => PresentBrush,
                    _ => AbsentBrush
                };

                // Animacja: zmniejsz do 0 (oś Y), zmień kolor, rozwiń z powrotem
                AnimateScaleY(tile, 1, 0, 120, onComplete: () =>
                {
                    tile.Background = brush;
                    tile.BorderBrush = brush;
                    AnimateScaleY(tile, 0, 1, 120);
                });
            };
            timer.Start();
        }

        // Po zakończeniu animacji – zaktualizuj klawiaturę i sprawdź wynik
        int totalDelay = GameLogic.WordLength * 250 + 300;
        var finishTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(totalDelay)
        };
        finishTimer.Tick += (_, _) =>
        {
            finishTimer.Stop();

            // Aktualizuj klawiaturę
            GameLogic.UpdateKeyboardState(_keyboardState, guess, evaluation);
            RefreshKeyboardColors();

            // Sprawdź wynik
            if (guess == _secretWord)
            {
                _isGameOver = true;
                _gamesPlayed++;
                _gamesWon++;
                _streak++;

                // Bounce animacja
                for (int j = 0; j < GameLogic.WordLength; j++)
                {
                    int jj = j;
                    var bounceTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(j * 80)
                    };
                    bounceTimer.Tick += (_, _) =>
                    {
                        bounceTimer.Stop();
                        AnimateBounce(_tiles[_currentRow, jj]);
                    };
                    bounceTimer.Start();
                }

                // Pokaż okno podsumowania
                var resultTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(1000)
                };
                resultTimer.Tick += (_, _) =>
                {
                    resultTimer.Stop();
                    ShowResultDialog(won: true);
                };
                resultTimer.Start();
            }
            else if (_currentRow >= GameLogic.MaxAttempts - 1)
            {
                _isGameOver = true;
                _gamesPlayed++;
                _streak = 0;

                var resultTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                resultTimer.Tick += (_, _) =>
                {
                    resultTimer.Stop();
                    ShowResultDialog(won: false);
                };
                resultTimer.Start();
            }
            else
            {
                _currentRow++;
                _currentCol = 0;
            }
        };
        finishTimer.Start();
    }

    // ══════════════════════════════════════════
    //  KLAWIATURA – KOLOROWANIE
    // ══════════════════════════════════════════

    private void RefreshKeyboardColors()
    {
        foreach (var kvp in _keyboardState)
        {
            string keyStr = kvp.Key.ToString();
            if (_keyButtons.TryGetValue(keyStr, out var btn))
            {
                btn.Background = kvp.Value switch
                {
                    LetterStatus.Correct => CorrectBrush,
                    LetterStatus.Present => PresentBrush,
                    LetterStatus.Absent => AbsentBrush,
                    _ => KeyDefault
                };

                if (kvp.Value == LetterStatus.Absent)
                    btn.Opacity = 0.45;
            }
        }
    }

    // ══════════════════════════════════════════
    //  OKNO WYNIKU
    // ══════════════════════════════════════════

    private void ShowResultDialog(bool won)
    {
        string title = won ? "Wspaniale! 🎉" : "Koniec prób 😔";
        string attemptsText = won
            ? $"Odgadłeś słowo w {_currentRow + 1} {Grammar(_currentRow + 1)}!"
            : "Nie udało się tym razem.";

        string stats = $"Rozegrane: {_gamesPlayed}   |   Wygrane: {_gamesWon}   |   Seria: {_streak}";

        var result = MessageBox.Show(
            $"{title}\n\n{attemptsText}\n\nSzukane hasło: {_secretWord}\n\n{stats}\n\nCzy chcesz zagrać ponownie?",
            "Wordle PL – Wynik",
            MessageBoxButton.YesNo,
            MessageBoxImage.None);

        if (result == MessageBoxResult.Yes)
        {
            StartNewRound();
        }
    }

    private static string Grammar(int n) =>
        n == 1 ? "próbie" : "próbach";

    // ══════════════════════════════════════════
    //  POMOC
    // ══════════════════════════════════════════

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "JAK GRAĆ?\n\n" +
            "Odgadnij 5-literowe słowo w 6 próbach.\n\n" +
            "🟩 ZIELONY – litera jest na właściwym miejscu\n" +
            "🟨 ŻÓŁTY – litera jest w słowie, ale na innej pozycji\n" +
            "⬛ SZARY – litery nie ma w słowie\n\n" +
            "Wpisuj litery klawiaturą fizyczną lub klikaj klawiaturę ekranową.\n" +
            "Polskie znaki: użyj prawego Alt (AltGr) lub kliknij w dolny rząd klawiatury.",
            "Wordle PL – Zasady",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // ══════════════════════════════════════════
    //  ANIMACJE
    // ══════════════════════════════════════════

    private static void AnimateScale(Border border, double from, double to, int durationMs, int delayMs = 0)
    {
        var transform = border.RenderTransform as ScaleTransform;
        if (transform == null)
        {
            transform = new ScaleTransform(1, 1);
            border.RenderTransform = transform;
            border.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs)
        };

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    private static void AnimateScaleY(Border border, double from, double to, int durationMs,
                                       int delayMs = 0, Action? onComplete = null)
    {
        var transform = border.RenderTransform as ScaleTransform;
        if (transform == null)
        {
            transform = new ScaleTransform(1, 1);
            border.RenderTransform = transform;
            border.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs)
        };

        if (onComplete != null)
        {
            anim.Completed += (_, _) => onComplete();
        }

        transform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    private static void AnimateBounce(Border border)
    {
        var transform = border.RenderTransform as TranslateTransform;
        if (transform == null)
        {
            // Potrzebujemy TransformGroup, bo mogliśmy wcześniej ustawić ScaleTransform
            var group = new TransformGroup();
            if (border.RenderTransform is ScaleTransform existingScale)
            {
                group.Children.Add(existingScale);
            }
            else
            {
                group.Children.Add(new ScaleTransform(1, 1));
            }
            transform = new TranslateTransform(0, 0);
            group.Children.Add(transform);
            border.RenderTransform = group;
            border.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        var bounceUp = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(500)
        };
        bounceUp.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        bounceUp.KeyFrames.Add(new LinearDoubleKeyFrame(-18, KeyTime.FromPercent(0.3)));
        bounceUp.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0.5)));
        bounceUp.KeyFrames.Add(new LinearDoubleKeyFrame(-8, KeyTime.FromPercent(0.7)));
        bounceUp.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1.0)));

        transform.BeginAnimation(TranslateTransform.YProperty, bounceUp);
    }

    private void ShakeRow(int row)
    {
        for (int c = 0; c < GameLogic.WordLength; c++)
        {
            var tile = _tiles[row, c];

            // Znajdź lub stwórz TranslateTransform
            TranslateTransform? translate = null;
            if (tile.RenderTransform is TranslateTransform tt)
            {
                translate = tt;
            }
            else if (tile.RenderTransform is TransformGroup tg)
            {
                translate = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
            }

            if (translate == null)
            {
                translate = new TranslateTransform(0, 0);
                tile.RenderTransform = translate;
            }

            var shake = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(400)
            };
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(-5, KeyTime.FromPercent(0.2)));
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(5, KeyTime.FromPercent(0.4)));
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(-5, KeyTime.FromPercent(0.6)));
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(5, KeyTime.FromPercent(0.8)));
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1.0)));

            translate.BeginAnimation(TranslateTransform.XProperty, shake);
        }
    }

    private System.Windows.Threading.DispatcherTimer? _toastTimer;

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        _toastTimer?.Stop();

        // Płynne pojawienie się powiadomienia
        var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120));
        ToastBorder.BeginAnimation(OpacityProperty, fadeIn);

        _toastTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(300));
            ToastBorder.BeginAnimation(OpacityProperty, fadeOut);
        };
        _toastTimer.Start();
    }
}