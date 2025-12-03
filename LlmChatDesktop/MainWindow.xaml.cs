using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LlmChatDesktop
{
    public partial class MainWindow : Window
    {
        private readonly LlmService _llm;
        private readonly List<ChatMessage> _conversationHistory;

        public MainWindow()
        {
            InitializeComponent();

            // Karanlık mod teması
            this.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            ConversationScroll.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            InputBox.Background = new SolidColorBrush(Color.FromRgb(50, 50, 50));
            InputBox.Foreground = Brushes.White;
            InputBox.BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70));

            // API anahtarını buraya ekle
            // GROQ: https://console.groq.com/keys (ÜCRETSIZ!)
            // CLAUDE: https://console.anthropic.com/settings/keys
            var apiKey = ""; // Buraya API key'ini yapıştır
            var provider = ApiProvider.Groq; // Groq, Claude veya Mock seçebilirsin

            _llm = new LlmService(apiKey, provider);
            _conversationHistory = new List<ChatMessage>();

            // Enter tuşu ile gönder
            InputBox.PreviewKeyDown += InputBox_PreviewKeyDown;
        }

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                SendBtn_Click(sender, e);
            }
        }

        private async void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            var userText = InputBox.Text?.Trim();
            if (string.IsNullOrEmpty(userText)) return;

            // Kullanıcı mesajını ekle
            _conversationHistory.Add(new ChatMessage { Role = "user", Content = userText });
            AddMessageToConversation("Sen", userText, Color.FromRgb(37, 99, 235));
            InputBox.Clear();
            SendBtn.IsEnabled = false;

            // "Yazıyor..." göstergesi
            var typingIndicator = AddTypingIndicator();

            try
            {
                var response = await _llm.GetChatCompletionStreamAsync(
                    _conversationHistory,
                    (chunk) => UpdateTypingMessage(typingIndicator, chunk)
                );

                // Yazıyor göstergesini kaldır ve gerçek mesajı ekle
                ConversationPanel.Children.Remove(typingIndicator);
                _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = response });
                AddMessageToConversation("Asistan", response, Color.FromRgb(34, 197, 94));
            }
            catch (Exception ex)
            {
                ConversationPanel.Children.Remove(typingIndicator);
                AddMessageToConversation("Sistem", $"❌ Hata: {ex.Message}", Color.FromRgb(220, 38, 38));
            }

            ConversationScroll.ScrollToEnd();
            SendBtn.IsEnabled = true;
        }

        private Border AddTypingIndicator()
        {
            var dots = new TextBlock
            {
                Text = "●●●",
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Margin = new Thickness(0, 5, 0, 0)
            };

            var border = CreateMessageBorder("Asistan", Color.FromRgb(60, 60, 60), dots);
            ConversationPanel.Children.Add(border);
            ConversationScroll.ScrollToEnd();
            return border;
        }

        private void UpdateTypingMessage(Border border, string text)
        {
            Dispatcher.Invoke(() =>
            {
                if (border.Child is StackPanel panel && panel.Children.Count > 1)
                {
                    if (panel.Children[1] is TextBlock tb)
                    {
                        tb.Text = text;
                    }
                }
                else if (border.Child is StackPanel panel2 && panel2.Children.Count == 1)
                {
                    // İlk chunk geldiğinde TextBlock ekle
                    panel2.Children.Add(new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White,
                        FontSize = 13,
                        Margin = new Thickness(0, 5, 0, 0)
                    });
                }
                ConversationScroll.ScrollToEnd();
            });
        }

        private Border AddMessageToConversation(string who, string text, Color bgColor)
        {
            var tb = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var border = CreateMessageBorder(who, bgColor, tb);
            ConversationPanel.Children.Add(border);
            ConversationScroll.ScrollToEnd();
            return border;
        }

        private Border CreateMessageBorder(string who, Color bgColor, UIElement content)
        {
            var header = new TextBlock
            {
                Text = who,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200))
            };

            var panel = new StackPanel();
            panel.Children.Add(header);
            panel.Children.Add(content);

            return new Border
            {
                Background = new SolidColorBrush(bgColor),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(8, 4, 8, 4),
                Padding = new Thickness(12),
                Child = panel,
                MaxWidth = 600,
                HorizontalAlignment = who == "Sen" ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public enum ApiProvider
    {
        Groq,
        Claude,
        Mock
    }

    public class LlmService
    {
        private readonly string? _apiKey;
        private readonly HttpClient? _http;
        private readonly ApiProvider _provider;

        public LlmService(string? apiKey, ApiProvider provider)
        {
            _provider = string.IsNullOrWhiteSpace(apiKey) ? ApiProvider.Mock : provider;

            if (_provider != ApiProvider.Mock)
            {
                _apiKey = apiKey;
                _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

                switch (_provider)
                {
                    case ApiProvider.Groq:
                        _http.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
                        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                        break;
                    case ApiProvider.Claude:
                        _http.BaseAddress = new Uri("https://api.anthropic.com/v1/");
                        _http.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                        break;
                }
            }
        }

        public async Task<string> GetChatCompletionStreamAsync(List<ChatMessage> history, Action<string> onChunk)
        {
            if (_provider == ApiProvider.Mock || _http == null)
                return await GetMockResponseAsync(history, onChunk);

            try
            {
                switch (_provider)
                {
                    case ApiProvider.Groq:
                        return await GetGroqResponseAsync(history, onChunk);
                    case ApiProvider.Claude:
                        return await GetClaudeResponseAsync(history, onChunk);
                    default:
                        throw new Exception("Bilinmeyen provider");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"API Hatası: {ex.Message}");
            }
        }

        private async Task<string> GetGroqResponseAsync(List<ChatMessage> history, Action<string> onChunk)
        {
            var messages = new List<object>
            {
                new { role = "system", content = "Sen yardımcı, bilgili ve samimi bir Türkçe asistansın. Detaylı ve anlaşılır açıklamalar yaparsın. Doğal ve arkadaşça konuşursun." }
            };

            foreach (var msg in history)
                messages.Add(new { role = msg.Role, content = msg.Content });

            var request = new
            {
                model = "llama-3.3-70b-versatile", // Groq'un en iyi modeli
                messages = messages,
                max_tokens = 2048,
                temperature = 0.7,
                stream = true
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http!.PostAsync("chat/completions", content);


            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"API Hatası ({response.StatusCode}): {error}");
            }

            var fullResponse = new StringBuilder();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                var data = line.Substring(6);
                if (data == "[DONE]") break;

                try
                {
                    using var doc = JsonDocument.Parse(data);
                    if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var delta = choices[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentProp))
                        {
                            var chunk = contentProp.GetString();
                            if (!string.IsNullOrEmpty(chunk))
                            {
                                fullResponse.Append(chunk);
                                onChunk?.Invoke(fullResponse.ToString());
                                await Task.Delay(10); // Smooth animasyon
                            }
                        }
                    }
                }
                catch { }
            }

            return fullResponse.ToString();
        }

        private async Task<string> GetClaudeResponseAsync(List<ChatMessage> history, Action<string> onChunk)
        {
            var messages = history.Select(m => new { role = m.Role, content = m.Content }).ToList();

            var request = new
            {
                model = "claude-sonnet-4-20250514",
                max_tokens = 2048,
                messages = messages,
                system = "Sen yardımcı, bilgili ve samimi bir Türkçe asistansın. Detaylı ve anlaşılır açıklamalar yaparsın.",
                stream = true
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http!.PostAsync("messages", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Claude API Hatası: {error}");
            }

            var fullResponse = new StringBuilder();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                var data = line.Substring(6);
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    if (doc.RootElement.TryGetProperty("type", out var type) && type.GetString() == "content_block_delta")
                    {
                        if (doc.RootElement.TryGetProperty("delta", out var delta) &&
                            delta.TryGetProperty("text", out var text))
                        {
                            var chunk = text.GetString();
                            if (!string.IsNullOrEmpty(chunk))
                            {
                                fullResponse.Append(chunk);
                                onChunk?.Invoke(fullResponse.ToString());
                                await Task.Delay(10);
                            }
                        }
                    }
                }
                catch { }
            }

            return fullResponse.ToString();
        }

        private async Task<string> GetMockResponseAsync(List<ChatMessage> history, Action<string> onChunk)
        {
            var response = "🤖 MOCK MODE: API key yok, demo modda çalışıyorum!\n\n" +
                          "Gerçek AI kullanmak için:\n" +
                          "1. Groq API key al (ÜCRETSİZ): https://console.groq.com/keys\n" +
                          "2. Key'i MainWindow.cs'deki 'apiKey' değişkenine yapıştır\n" +
                          "3. Uygulamayı yeniden başlat\n\n" +
                          $"Senin mesajın: {history.LastOrDefault()?.Content ?? "Boş"}";

            // Kelime kelime yazdırma efekti
            var words = response.Split(' ');
            var accumulated = new StringBuilder();

            foreach (var word in words)
            {
                accumulated.Append(word + " ");
                onChunk?.Invoke(accumulated.ToString().Trim());
                await Task.Delay(50);
            }

            return response;
        }
    }
}