using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GravityDashboard;

public partial class MainWindow : Window
{
    private ApiClient? _api;
    private Crew? _selected;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly ObservableCollection<ChatBubble> _chat = new();
    private readonly List<ChatMessage> _history = new();
    private bool _chatBusy;

    public MainWindow()
    {
        InitializeComponent();
        ChatList.ItemsSource = _chat;
        // Retient l'URL entre deux lancements
        if (File.Exists("gravity_config.txt"))
            ApiUrlBox.Text = File.ReadAllText("gravity_config.txt");
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    // Connexion

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var url = ApiUrlBox.Text.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url)) return;

        _api = new ApiClient(url);
        File.WriteAllText("gravity_config.txt", url);
        try
        {
            var crews = await _api.GetCrewsAsync();
            CrewList.ItemsSource = crews;
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
            StatusText.Text = $"En ligne — {crews.Count} membre(s)";
            _timer.Start();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            StatusText.Text = "Connexion impossible : " + ex.Message;
        }
    }

    private async void CrewList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = CrewList.SelectedItem as Crew;
        await RefreshAsync();
    }

    // Rafraichissement automatique

    private async Task RefreshAsync()
    {
        if (_api == null) return;
        try
        {
            if (_selected != null)
            {
                var st = await _api.GetStatusAsync(_selected.Id);
                if (st != null)
                {
                    CrewTitle.Text = $"{st.Name} — {_selected.Role}";
                    ScoreText.Text = (st.ScoreGlobal?.ToString("0.0") ?? "—") + " %";
                    IndicatorsGrid.ItemsSource = st.Indicateurs
                        .Select(kv => new IndicatorRow(kv))
                        .ToList();
                }
            }
            AlertsGrid.ItemsSource = await _api.GetAlertsAsync();
        }
        catch
        {
            
        }
    }

    // Chat GRAVITY AI

    private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendAsync();

    private async void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await SendAsync();
    }

    private async Task SendAsync()
    {
        var msg = ChatInput.Text.Trim();
        if (string.IsNullOrEmpty(msg) || _api == null || _chatBusy) return;

        ChatInput.Clear();
        _chatBusy = true;
        _chat.Add(new ChatBubble(msg, isUser: true));
        _history.Add(new ChatMessage { Role = "user", Content = msg });
        ScrollChatToEnd();

        try
        {
            var reply = await _api.ChatAsync(msg, _history);
            _history.Add(new ChatMessage { Role = "assistant", Content = reply });
            _chat.Add(new ChatBubble(reply, isUser: false));
        }
        catch (Exception ex)
        {
            // Cas demo : LLM coupe -> le reste du systeme continue
            _chat.Add(new ChatBubble("GRAVITY indisponible : " + ex.Message, isUser: false));
        }
        finally
        {
            _chatBusy = false;
            ScrollChatToEnd();
        }
    }

    private void ScrollChatToEnd()
    {
        if (_chat.Count > 0) ChatList.ScrollIntoView(_chat[^1]);
    }
}

// Bulle de message : alignee a droite si c'est l'utilisateur
public class ChatBubble
{
    public string Text { get; }
    public HorizontalAlignment Align { get; }

    public ChatBubble(string text, bool isUser)
    {
        Text = text;
        Align = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }
}
