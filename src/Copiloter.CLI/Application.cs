using Copiloter.CLI.Features.Commands;
using Copiloter.CLI.Utilities;
using MediatR;
using System.CommandLine;

namespace Copiloter.CLI;

public class Application(ISender mediator)
{
    public async Task<int> RunAsync(string[] args)
    {
        // Root command
        var rootCommand = new RootCommand("Azure DevOps Copilot SDLC - AI-powered work item lifecycle automation");

        // Common options
        var directoryOption = new Option<string>(
            aliases: new[] { "-d", "--directory" },
            description: "Working directory (defaults to current directory)",
            getDefaultValue: () => Directory.GetCurrentDirectory()
        );

        var workItemOption = new Argument<int>(
            name: "work-item-id",
            description: "Azure DevOps work item ID"
        );

        // Plan command
        var planCommand = new Command("plan", "Generate AI implementation plan for a work item")
        {
            workItemOption,
            directoryOption
        };

        var yesOption = new Option<bool>(
            aliases: new[] { "-y", "--yes" },
            description: "Skip confirmation prompts",
            getDefaultValue: () => false
        );
        planCommand.AddOption(yesOption);

        planCommand.SetHandler(async (int workItemId, string directory, bool yes) =>
        {
            try
            {
                var exitCode = await mediator.Send(new PlanCommand
                {
                    WorkItemId = workItemId,
                    WorkingDirectory = directory,
                    SkipConfirmation = yes
                });
                Environment.Exit(exitCode);
            }
            catch (Exception ex)
            {
                ConsoleHelper.ShowError($"Unexpected error: {ex.Message}");
                Environment.Exit(1);
            }
        }, workItemOption, directoryOption, yesOption);

        // Develop command
        var developCommand = new Command("develop", "Implement feature based on AI plan")
        {
            workItemOption,
            directoryOption
        };

        var withReviewOption = new Option<bool>(
            aliases: new[] { "-r", "--with-review" },
            description: "Automatically proceed to review stage after development",
            getDefaultValue: () => false
        );
        developCommand.AddOption(withReviewOption);

        developCommand.SetHandler(async (int workItemId, string directory, bool withReview) =>
        {
            try
            {
                var exitCode = await mediator.Send(new DevelopCommand
                {
                    WorkItemId = workItemId,
                    WorkingDirectory = directory,
                    WithReview = withReview
                });
                Environment.Exit(exitCode);
            }
            catch (Exception ex)
            {
                ConsoleHelper.ShowError($"Unexpected error: {ex.Message}");
                Environment.Exit(1);
            }
        }, workItemOption, directoryOption, withReviewOption);

        // Review command
        var reviewCommand = new Command("review", "AI-powered code review before PR merge")
        {
            workItemOption,
            directoryOption
        };

        reviewCommand.SetHandler(async (int workItemId, string directory) =>
        {
            try
            {
                var exitCode = await mediator.Send(new ReviewCommand
                {
                    WorkItemId = workItemId,
                    WorkingDirectory = directory
                });
                Environment.Exit(exitCode);
            }
            catch (Exception ex)
            {
                ConsoleHelper.ShowError($"Unexpected error: {ex.Message}");
                Environment.Exit(1);
            }
        }, workItemOption, directoryOption);

        // Add commands to root
        rootCommand.AddCommand(planCommand);
        rootCommand.AddCommand(developCommand);
        rootCommand.AddCommand(reviewCommand);

        // Execute
        return await rootCommand.InvokeAsync(args);
    }
}
