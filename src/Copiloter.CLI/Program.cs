using Copiloter.CLI;
using Copiloter.CLI.Features.Behaviours;
using Copiloter.CLI.Services;
using Copiloter.CLI.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

var services = new ServiceCollection();

services.AddMediatR(cfg => 
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly())
        .AddBehavior(typeof(IPipelineBehavior<,>), typeof(RequirementsBehaviour<,>));
});

services.AddScoped<IAgentDiscoveryService, AgentDiscoveryService>();

services.AddSingleton<IMcpConfigurationService, McpConfigurationService>();

services.AddScoped<ICopilotAgentService, CopilotAgentService>();

services.AddSingleton<Application>();

var provider = services.BuildServiceProvider();

var app = provider.GetRequiredService<Application>();

return await app.RunAsync(args);
