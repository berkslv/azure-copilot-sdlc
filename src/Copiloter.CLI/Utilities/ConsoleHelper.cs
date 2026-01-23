using Spectre.Console;

namespace Copiloter.CLI.Utilities;

/// <summary>
/// Helper class for consistent console UI using Spectre.Console
/// </summary>
public static class ConsoleHelper
{
    /// <summary>
    /// Display an error message
    /// </summary>
    public static void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/red] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Display a warning message
    /// </summary>
    public static void ShowWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]Warning:[/yellow] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Display an info message
    /// </summary>
    public static void ShowInfo(string message)
    {
        AnsiConsole.MarkupLine($"[blue]Info:[/blue] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Display a success message
    /// </summary>
    public static void ShowSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]{Markup.Escape(message)}[/green]");
    }

    /// <summary>
    /// Display content in a panel
    /// </summary>
    public static void ShowPanel(string title, string content)
    {
        var panel = new Panel(Markup.Escape(content))
        {
            Header = new PanelHeader(title),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Prompt user for a choice with multiple options
    /// </summary>
    public static string PromptChoice(string prompt, params string[] choices)
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(prompt)
                .AddChoices(choices)
        );
    }

    /// <summary>
    /// Prompt for yes/no confirmation
    /// </summary>
    public static bool PromptConfirm(string prompt, bool defaultValue = true)
    {
        return AnsiConsole.Confirm(prompt, defaultValue);
    }

    /// <summary>
    /// Prompt for hidden text input (passwords, tokens)
    /// </summary>
    public static string PromptSecret(string prompt)
    {
        return AnsiConsole.Prompt(
            new TextPrompt<string>(prompt)
                .Secret()
        );
    }

    /// <summary>
    /// Show a progress/status message
    /// </summary>
    public static void ShowStatus(string message)
    {
        AnsiConsole.Status()
            .Start(message, ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                ctx.SpinnerStyle(Style.Parse("green"));
            });
    }

    /// <summary>
    /// Display a countdown timer
    /// </summary>
    public static async Task ShowCountdown(string message, int seconds, CancellationToken cancellationToken = default)
    {
        for (int i = seconds; i > 0; i--)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)} in {i} seconds... (Press Ctrl+C to cancel)[/yellow]");
            await Task.Delay(1000, cancellationToken);
            
            if (i > 1)
                AnsiConsole.Cursor.MoveUp();
        }
    }

    /// <summary>
    /// Execute an action with a progress indicator
    /// </summary>
    public static async Task<T> WithProgress<T>(string description, Func<Task<T>> action)
    {
        return await AnsiConsole.Status()
            .StartAsync(description, async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                ctx.SpinnerStyle(Style.Parse("green"));
                return await action();
            });
    }

    /// <summary>
    /// Execute an action with a progress indicator (void return)
    /// </summary>
    public static async Task WithProgress(string description, Func<Task> action)
    {
        await AnsiConsole.Status()
            .StartAsync(description, async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                ctx.SpinnerStyle(Style.Parse("green"));
                await action();
            });
    }
}
