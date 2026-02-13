using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Themes.Fluent;

namespace Shelly_UI.CustomControls.Primitives;

public partial class MarkdownTextBlock : StackPanel
{
    private static readonly StyledProperty<string> MarkdownTextProperty =
        AvaloniaProperty.Register<MarkdownTextBlock, string>(nameof(MarkdownText), string.Empty);

    public string MarkdownText
    {
        get => GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    static MarkdownTextBlock()
    {
        MarkdownTextProperty.Changed.AddClassHandler<MarkdownTextBlock>((x, e) => x.OnMarkdownTextChanged(e));
    }

    public MarkdownTextBlock()
    {
        Spacing = 8;
    }

    private void OnMarkdownTextChanged(AvaloniaPropertyChangedEventArgs e)
    {
        Children.Clear();

        if (string.IsNullOrWhiteSpace(MarkdownText))
            return;

        var lines = MarkdownText.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                Children.Add(new TextBlock { Height = 4 });
                continue;
            }

            // Check for header ##
            if (line.TrimStart().StartsWith("## "))
            {
                var headerText = line.TrimStart()[3..];
                var textBlock = new TextBlock
                {
                    Text = headerText,
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(0, 8, 0, 4),
                };
                Children.Add(textBlock);
            }

            // Check for bullet point *
            else if (line.TrimStart().StartsWith("* "))
            {
                var bulletText = line.TrimStart()[2..];
                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(16, 2, 0, 2)
                };

                panel.Children.Add(new TextBlock
                {
                    Text = "• ",
                    VerticalAlignment = VerticalAlignment.Top,
                });

                var contentPanel = CreateFormattedContent(bulletText);
                contentPanel.VerticalAlignment = VerticalAlignment.Top;
                panel.Children.Add(contentPanel);

                Children.Add(panel);
            }
            else
            {
                var contentPanel = CreateFormattedContent(line);
                Children.Add(contentPanel);
            }
        }
    }

    private static Panel CreateFormattedContent(string text)
    {
        var wrapPanel = new WrapPanel();

        //Regex to match bold and links
        var matches = MyRegex().Matches(text);

        if (matches.Count == 0)
        {
            wrapPanel.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
            });
            return wrapPanel;
        }

        var lastIndex = 0;

        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
            {
                var normalText = text.Substring(lastIndex, match.Index - lastIndex);
                wrapPanel.Children.Add(new TextBlock
                {
                    Text = normalText,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            //Bold
            if (!string.IsNullOrEmpty(match.Groups[2].Value))
            {
                var boldText = match.Groups[2].Value;
                wrapPanel.Children.Add(new TextBlock
                {
                    Text = boldText,
                    FontWeight = FontWeight.Bold,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            //URL
            else if (!string.IsNullOrEmpty(match.Groups[3].Value))
            {
                var accentColor = Colors.DodgerBlue;
                if (Application.Current?.TryFindResource("SystemAccentColorLight1", out var resource) == true &&
                    resource is Color col)
                {
                    accentColor = col;
                }

                var foreground = new SolidColorBrush(accentColor, 1);
                var url = match.Groups[3].Value;
                var linkTextBlock = new TextBlock
                {
                    Text = url,
                    Foreground = foreground,
                    TextDecorations = TextDecorations.Underline,
                    Cursor = new Cursor(StandardCursorType.Hand),
                };

                linkTextBlock.PointerPressed += (s, e) =>
                {
                    if (e.GetCurrentPoint(linkTextBlock).Properties.IsLeftButtonPressed)
                    {
                        OpenUrl(url);
                    }
                };

                wrapPanel.Children.Add(linkTextBlock);
            }

            lastIndex = match.Index + match.Length;
        }

        // Add remaining text after last match
        if (lastIndex >= text.Length) return wrapPanel;
        var remainingText = text[lastIndex..];
        wrapPanel.Children.Add(new TextBlock
        {
            Text = remainingText,
            TextWrapping = TextWrapping.Wrap,
        });

        return wrapPanel;
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = url,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to open URL: {ex.Message}");
        }
    }

    [GeneratedRegex(@"(\*\*(.+?)\*\*)|((https?://[^\s\)]+))")]
    private static partial Regex MyRegex();
}