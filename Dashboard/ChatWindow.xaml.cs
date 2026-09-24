using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GravityDashboard;

public partial class ChatWindow : Window
{
    private readonly ApiClient _api;
    private readonly ObservableCollection<ChatBubble> _chat = new();
    private readonly List<ChatMessage> _history = new();
    private bool _busy;

    public ChatWindow(ApiClient api)
    {
        InitializeComponent();
        _api = api;
        ChatList.ItemsSource = _chat;
        _chat.Add(new ChatBubble("GRAVITY à votre écoute. Comment puis-je vous aider ?", isUser: false));
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendAsync();

    private async void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await SendAsync();
    }

    private async Task SendAsync()
    {
        var msg = ChatInput.Text.Trim();
        if (string.IsNullOrEmpty(msg) || _busy) return;

        ChatInput.Clear();
        _busy = true;
        _chat.Add(new ChatBubble(msg, isUser: true));
        _history.Add(new ChatMessage { Role = "user", Content = msg });
        ScrollToEnd();

        try
        {
            var reply = await _api.ChatAsync(msg, _history);
            _history.Add(new ChatMessage { Role = "assistant", Content = reply });
            _chat.Add(new ChatBubble(reply, isUser: false));
        }
        catch (Exception ex)
        {
            _chat.Add(new ChatBubble("GRAVITY indisponible : " + ex.Message, isUser: false));
        }
        finally
        {
            _busy = false;
            ScrollToEnd();
        }
    }

    private void ScrollToEnd()
    {
        if (_chat.Count > 0) ChatList.ScrollIntoView(_chat[^1]);
    }
}

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