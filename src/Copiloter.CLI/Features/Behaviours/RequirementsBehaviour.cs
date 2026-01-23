using Copiloter.CLI.Models;
using Copiloter.CLI.Services.Interfaces;
using Copiloter.CLI.Utilities;
using MediatR;
using System.Diagnostics;

namespace Copiloter.CLI.Features.Behaviours;

public class RequirementsBehaviour<TRequest, TResponse>(IMcpConfigurationService mcpConfig)
    : IPipelineBehavior<TRequest, TResponse> 
    where TRequest : IWorkItemRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!mcpConfig.ValidateNpxAvailable())
        {
            throw new InvalidOperationException("npx is not available. Please install Node.js.");
        }

        if (mcpConfig.GetEnvironmentVariables(request.WorkingDirectory) == null)
        {
            throw new InvalidOperationException("Enviroments are not ready.");
        }

        if (!ValidateGitRepository(request.WorkingDirectory))
        {
            throw new InvalidOperationException("git is not available in working directory. Initilize git.");
        }

        return await next();
    }

    /// <summary>
    /// Validate that working directory is a git repository
    /// </summary>
    private bool ValidateGitRepository(string workingDirectory)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --git-dir",
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                ConsoleHelper.ShowError("Directory is not a git repository. Please run this command from within a git repository or specify a valid git directory with -d.");
                return false;
            }

            return true;
        }
        catch
        {
            ConsoleHelper.ShowError("Failed to execute git command. Is git installed?");
            return false;
        }
    }
}
