using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Path = Avalonia.Controls.Shapes.Path;

namespace Notes.Services;

/// <summary>
/// Renders a markdown document as Avalonia controls (macOS-Notes-like typography:
/// bold headings, quiet body, accent links, soft code and quote surfaces).
/// Covers the widget-relevant subset: headings, paragraphs, inline styling,
/// links, code, lists (incl. task lists), quotes, rules and simple tables.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private static readonly FontFamily MonoFamily = new("Cascadia Mono, Consolas, Courier New");

    /// <summary>
    /// Build the visual tree for a markdown string.
    /// </summary>
    /// <param name="scope">Element used to resolve theme resources (accent, base colors).</param>
    /// <param name="markdown">The markdown text.</param>
    /// <param name="fontSize">Body font size; headings scale around it.</param>
    public static Control Render(StyledElement scope, string? markdown, double fontSize = 14)
    {
        var panel = new StackPanel { Spacing = Math.Round(fontSize * 0.38) };

        if (string.IsNullOrWhiteSpace(markdown))
            return panel;

        foreach (var block in Markdown.Parse(markdown, Pipeline))
            AppendBlock(scope, panel, block, fontSize);

        return panel;
    }

    private static void AppendBlock(StyledElement scope, Panel parent, Block block, double fontSize)
    {
        switch (block)
        {
            case HeadingBlock heading:
            {
                var host = NewTextBlock(fontSize * heading.Level switch
                {
                    1 => 1.5,
                    2 => 1.3,
                    3 => 1.15,
                    4 => 1.05,
                    _ => 0.95,
                });
                host.FontWeight = heading.Level <= 2 ? FontWeight.Bold : FontWeight.SemiBold;
                AppendInline(host, heading.Inline, InlineStyle.Plain);
                parent.Children.Add(host);
                break;
            }
            case ParagraphBlock paragraph:
            {
                var host = NewTextBlock(fontSize);
                AppendInline(host, paragraph.Inline, InlineStyle.Plain);
                parent.Children.Add(host);
                break;
            }
            case ListBlock list:
            {
                parent.Children.Add(BuildList(scope, list, fontSize));
                break;
            }
            case QuoteBlock quote:
            {
                var inner = new StackPanel { Spacing = Math.Round(fontSize * 0.3) };
                foreach (var child in quote)
                    AppendBlock(scope, inner, child, fontSize);
                inner.Opacity = 0.78;
                parent.Children.Add(new Border
                {
                    Margin = new Thickness(0, 0, 0, 2),
                    Padding = new Thickness(10, 2, 2, 2),
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    BorderBrush = AccentBrush(scope, 0.9),
                    Child = inner,
                });
                break;
            }
            case ThematicBreakBlock:
            {
                parent.Children.Add(BuildRule());
                break;
            }
            case CodeBlock code:
            {
                parent.Children.Add(BuildCode(scope, code.Lines.ToString(), fontSize));
                break;
            }
            case Table table:
            {
                parent.Children.Add(BuildTable(scope, table, fontSize));
                break;
            }
            case ContainerBlock container:
            {
                foreach (var child in container)
                    AppendBlock(scope, parent, child, fontSize);
                break;
            }
        }
    }

    private static Control BuildList(StyledElement scope, ListBlock list, double fontSize)
    {
        var panel = new StackPanel { Spacing = Math.Round(fontSize * 0.18) };
        var number = int.TryParse(list.OrderedStart, out var start) ? start : 1;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            var (marker, body) = BuildItem(scope, item, list, number, fontSize);
            if (list.IsOrdered) number++;

            var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto, *") };
            Grid.SetColumn(marker, 0);
            Grid.SetColumn(body, 1);
            columns.Children.Add(marker);
            columns.Children.Add(body);
            panel.Children.Add(columns);
        }

        return panel;
    }

    private static (TextBlock Marker, StackPanel Body) BuildItem(
        StyledElement scope, ListItemBlock item, ListBlock list, int number, double fontSize)
    {
        var marker = new TextBlock
        {
            Text = list.IsOrdered ? $"{number}." : "•",
            FontSize = fontSize,
            Opacity = 0.65,
            Margin = new Thickness(0, 0, 6, 0),
            MinWidth = 12,
        };

        var body = new StackPanel { Spacing = list.IsLoose ? 4 : 2 };
        var first = true;
        var isTask = false;
        var taskChecked = false;

        foreach (var block in item)
        {
            if (first && block is ParagraphBlock paragraph && TryExtractTask(paragraph, out var checkedState))
            {
                isTask = true;
                taskChecked = checkedState;
                var host = NewTextBlock(fontSize);
                AppendInline(host, paragraph.Inline, InlineStyle.Plain);
                body.Children.Add(host);
                first = false;
                continue;
            }

            first = false;
            AppendBlock(scope, body, block, fontSize);
        }

        if (isTask)
        {
            marker.Text = taskChecked ? "✓" : "○";
            if (taskChecked)
            {
                marker.Foreground = AccentBrush(scope, 1);
                marker.Opacity = 1;
                body.Opacity = 0.55;
            }
        }

        return (marker, body);
    }

    /// <summary>
    /// Detect Markdig's task-list marker at the start of an item's first paragraph.
    /// </summary>
    private static bool TryExtractTask(ParagraphBlock paragraph, out bool isChecked)
    {
        isChecked = false;
        if (paragraph.Inline == null) return false;

        foreach (var inline in paragraph.Inline)
        {
            if (inline is TaskList task)
            {
                isChecked = task.Checked;
                return true;
            }

            break;
        }

        return false;
    }

    private static Control BuildCode(StyledElement scope, string code, double fontSize)
    {
        return new Border
        {
            Background = ResolveBrush(scope, "SystemControlBackgroundBaseLowBrush", "#22808080"),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 7, 10, 7),
            Child = new TextBlock
            {
                Text = code.TrimEnd('\n'),
                FontFamily = MonoFamily,
                FontSize = Math.Round(fontSize * 0.9),
                TextWrapping = TextWrapping.Wrap,
            },
        };
    }

    private static Control BuildTable(StyledElement scope, Table table, double fontSize)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        var rows = table.OfType<TableRow>().ToList();
        var columnCount = rows.Count == 0 ? 0 : rows.Max(r => r.OfType<TableCell>().Count());
        for (var c = 0; c < columnCount; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var r = 0; r < rows.Count; r++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var r = 0; r < rows.Count; r++)
        {
            var cells = rows[r].OfType<TableCell>().ToList();
            for (var c = 0; c < cells.Count && c < columnCount; c++)
            {
                var content = string.Join(" ", cells[c]
                    .OfType<ParagraphBlock>()
                    .Select(p => InlineToText(p.Inline)));
                var text = NewTextBlock(fontSize * 0.92);
                text.Text = content;
                text.Margin = new Thickness(0, 1, 14, 1);
                if (r == 0) text.FontWeight = FontWeight.SemiBold;
                Grid.SetRow(text, r);
                Grid.SetColumn(text, c);
                grid.Children.Add(text);
            }
        }

        return grid;
    }

    private static string InlineToText(Markdig.Syntax.Inlines.Inline? inline) => inline switch
    {
        null => "",
        LiteralInline literal => literal.Content.ToString(),
        CodeInline code => code.Content,
        AutolinkInline link => link.Url,
        ContainerInline container => string.Concat(container.Select(InlineToText)),
        _ => "",
    };

    private static Control BuildRule()
    {
        return new Border
        {
            Height = 12,
            Child = new Path
            {
                Height = 1,
                Stretch = Stretch.Fill,
                Opacity = 0.25,
                StrokeThickness = 1.5,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 1, 1 },
                StrokeDashOffset = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Data = new LineGeometry(new Point(0, 0), new Point(1, 0)),
            },
        };
    }

    private static TextBlock NewTextBlock(double fontSize) => new()
    {
        FontSize = Math.Round(fontSize * 10) / 10,
        TextWrapping = TextWrapping.Wrap,
    };

    private record InlineStyle
    {
        public static readonly InlineStyle Plain = new();

        public bool Bold { get; init; }
        public bool Italic { get; init; }
        public bool Strike { get; init; }
        public bool Link { get; init; }
        public bool Code { get; init; }
    }

    private static void AppendInline(TextBlock host, Markdig.Syntax.Inlines.Inline? inline, InlineStyle style)
    {
        switch (inline)
        {
            case null:
                break;
            case EmphasisInline emphasis:
            {
                var next = emphasis.DelimiterChar switch
                {
                    '~' => style with { Strike = true },
                    _ when emphasis.DelimiterCount >= 3 => style with { Bold = true, Italic = true },
                    _ when emphasis.DelimiterCount == 2 => style with { Bold = true },
                    _ => style with { Italic = true },
                };
                foreach (var child in emphasis)
                    AppendInline(host, child, next);
                break;
            }
            case LinkInline link:
            {
                var next = link.IsImage ? style : style with { Link = true };
                foreach (var child in link)
                    AppendInline(host, child, next);
                break;
            }
            case ContainerInline container:
            {
                foreach (var child in container)
                    AppendInline(host, child, style);
                break;
            }
            case LiteralInline literal:
            {
                host.Inlines!.Add(BuildRun(literal.Content.ToString(), style, host));
                break;
            }
            case CodeInline code:
            {
                host.Inlines!.Add(BuildRun(code.Content, style with { Code = true }, host));
                break;
            }
            case AutolinkInline link:
            {
                host.Inlines!.Add(BuildRun(link.Url, style with { Link = true }, host));
                break;
            }
            case HtmlEntityInline entity:
            {
                var transcoded = entity.Transcoded;
                host.Inlines!.Add(BuildRun(
                    transcoded.Length > 0 ? transcoded.ToString() : entity.Original.ToString(),
                    style, host));
                break;
            }
            case LineBreakInline:
            {
                host.Inlines!.Add(new Run("\n"));
                break;
            }
            case TaskList:
                break; // surfaced as the item marker
        }
    }

    private static Run BuildRun(string text, InlineStyle style, TextBlock host)
    {
        var run = new Run(text);

        if (style.Bold) run.FontWeight = FontWeight.Bold;
        if (style.Italic) run.FontStyle = FontStyle.Italic;

        if (style.Strike && style.Link)
            run.TextDecorations = new TextDecorationCollection
            {
                new TextDecoration { Location = TextDecorationLocation.Underline },
                new TextDecoration { Location = TextDecorationLocation.Strikethrough },
            };
        else if (style.Strike)
            run.TextDecorations = TextDecorations.Strikethrough;
        else if (style.Link)
            run.TextDecorations = TextDecorations.Underline;

        if (style.Link)
            run.Foreground = AccentBrush(host, 1);

        if (style.Code)
        {
            run.FontFamily = MonoFamily;
            run.FontSize = Math.Round(host.FontSize * 0.92 * 10) / 10;
            run.Background = ResolveBrush(host, "SystemControlBackgroundBaseLowBrush", "#33808080");
        }

        return run;
    }

    private static IBrush? AccentBrush(StyledElement scope, double opacity)
    {
        var color = scope.TryFindResource("SystemAccentColor", out var accent) && accent is Color c
            ? c
            : Color.Parse("#0078D4");
        return opacity >= 1 ? new SolidColorBrush(color) : new SolidColorBrush(color, opacity);
    }

    private static IBrush ResolveBrush(StyledElement scope, string key, string fallback)
    {
        if (scope.TryFindResource(key, out var value) && value is IBrush brush) return brush;
        return new SolidColorBrush(Color.Parse(fallback));
    }
}
