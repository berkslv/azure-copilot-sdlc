using MediatR;

namespace Copiloter.CLI.Models;

/// <summary>
/// Base interface for work item requests
/// </summary>
public interface IWorkItemRequest<out TResponse> : IRequest<TResponse>
{
    int WorkItemId { get; }

    string WorkingDirectory { get; }
}
